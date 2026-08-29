using CsCheck;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Modeller.Api.Initiative;
using Xunit;

namespace Modeller.Api.Tests.Initiative;

/// <summary>
/// Property-based coverage for <see cref="HmacInitiativeCredentialService"/> (issue #146). The
/// example-based tests in <see cref="InitiativeEndpointsTests"/> exercise one credential/session pair
/// at a time through the HTTP pipeline; these instead assert the invariants the whole authorization
/// scheme depends on hold for arbitrary session IDs, roles, and byte-level tampering - not just the
/// handful of examples a unit test happens to pick.
/// </summary>
public sealed class InitiativeCredentialsPropertyTests
{
    private static readonly Gen<InitiativeCredentialRole> RoleGen =
        Gen.Int[0, 1].Select(i => i == 0 ? InitiativeCredentialRole.Facilitator : InitiativeCredentialRole.DomainExpert);

    // CsCheck has no built-in Guid generator; adapting Gen.Int's iteration/shrink-driving harness to
    // produce plain random GUIDs is enough here since we only need many distinct session IDs, not to
    // shrink toward a particular one.
    private static readonly Gen<Guid> GuidGen = Gen.Int[0, int.MaxValue].Select(_ => Guid.NewGuid());

    private static HmacInitiativeCredentialService NewService() =>
        new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Initiative:CredentialSigningKey"] = "unit-test-signing-key-do-not-use-in-prod",
        }).Build(), TimeProvider.System, DevelopmentEnvironment);

    // No configured signing key, Development environment: per HmacInitiativeCredentialService's doc
    // comment, each instance then generates its own random per-process key - exactly the "two
    // cold-started processes, or a rotated key" scenario the cross-key test below asserts against.
    // Outside Development, the same construction is required to throw instead (see
    // HmacInitiativeCredentialServiceFailFastTests).
    private static HmacInitiativeCredentialService NewServiceWithRandomKey() =>
        new(new ConfigurationBuilder().Build(), TimeProvider.System, DevelopmentEnvironment);

    private static HmacInitiativeCredentialService NewServiceAt(DateTimeOffset now) =>
        new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Initiative:CredentialSigningKey"] = "unit-test-signing-key-do-not-use-in-prod",
        }).Build(), new FixedTimeProvider(now), DevelopmentEnvironment);

    // Same signing key as NewService(), but a different issuer/audience — models a credential minted
    // by some other deployment (or for some other purpose entirely) that happens to share the signing
    // secret, which is exactly the gap a mismatched-issuer/audience check is meant to close.
    private static HmacInitiativeCredentialService NewServiceWithForeignIssuerAndAudience() =>
        new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Initiative:CredentialSigningKey"] = "unit-test-signing-key-do-not-use-in-prod",
            ["Initiative:CredentialIssuer"] = "some-other-issuer",
            ["Initiative:CredentialAudience"] = "some-other-audience",
        }).Build(), TimeProvider.System, DevelopmentEnvironment);

    private static readonly IHostEnvironment DevelopmentEnvironment = new FakeHostEnvironment(Environments.Development);

    /// <summary>A <see cref="TimeProvider"/> pinned to one instant, so a test can put "now" exactly
    /// on a credential's expiry boundary - something <see cref="TimeProvider.System"/> can never do
    /// reliably.</summary>
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    /// <summary>Minimal <see cref="IHostEnvironment"/> stand-in so these unit tests can pin the
    /// environment name without spinning up a real host — <see cref="HmacInitiativeCredentialService"/>
    /// only ever reads <see cref="IHostEnvironment.EnvironmentName"/> via <c>IsDevelopment()</c>.</summary>
    internal sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Modeller.Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    /// <summary>Round-trip invariant: a credential minted for a given session/role must validate
    /// against that same session and report back the exact role it was minted for, for any session ID
    /// and role and any non-expired TTL.</summary>
    [Fact]
    public void Mint_then_Validate_round_trips_session_and_role_for_arbitrary_input()
    {
        var gen =
            from sessionId in GuidGen
            from role in RoleGen
            from ttlSeconds in Gen.Int[1, 1_000_000]
            select (sessionId, role, ttlSeconds);

        gen.Sample(t =>
        {
            var service = NewService();
            var credential = service.Mint(t.sessionId, t.role, TimeSpan.FromSeconds(t.ttlSeconds));

            var result = service.Validate(credential, t.sessionId);

            Assert.True(result.Succeeded);
            Assert.Equal(t.role, result.Role);
            Assert.Null(result.Failure);
        });
    }

    /// <summary>A credential minted for one session must never validate against a different session -
    /// the whole point of stamping the session ID into the signed payload (issue #146) is that a
    /// Facilitator/Domain Expert link for session A can't be replayed against session B.</summary>
    [Fact]
    public void Credential_minted_for_one_session_never_validates_for_another()
    {
        var gen =
            from mintedFor in GuidGen
            from presentedFor in GuidGen
            where mintedFor != presentedFor
            from role in RoleGen
            select (mintedFor, presentedFor, role);

        gen.Sample(t =>
        {
            var service = NewService();
            var credential = service.Mint(t.mintedFor, t.role);

            var result = service.Validate(credential, t.presentedFor);

            Assert.False(result.Succeeded);
            Assert.Equal(InitiativeCredentialFailure.WrongSession, result.Failure);
        });
    }

    /// <summary>An already-expired TTL (including negative, per <see
    /// cref="IInitiativeCredentialService.Mint"/>'s doc comment on testing) must always be rejected as
    /// expired, never accepted - for arbitrary session/role.</summary>
    [Fact]
    public void Credential_minted_already_expired_is_always_rejected_as_expired()
    {
        var gen =
            from sessionId in GuidGen
            from role in RoleGen
            from negativeTtlSeconds in Gen.Int[1, 1_000_000]
            select (sessionId, role, negativeTtlSeconds);

        gen.Sample(t =>
        {
            var service = NewService();
            var credential = service.Mint(t.sessionId, t.role, TimeSpan.FromSeconds(-t.negativeTtlSeconds));

            var result = service.Validate(credential, t.sessionId);

            Assert.False(result.Succeeded);
            Assert.Equal(InitiativeCredentialFailure.Expired, result.Failure);
        });
    }

    /// <summary>Boundary case for expiry: a credential whose expiry lands on exactly the current
    /// instant (not a moment before or after it) must still be rejected as expired. <see
    /// cref="TimeProvider.System"/> can never reliably land "now" on that exact boundary, so this
    /// pins time with <see cref="FixedTimeProvider"/> instead - guarding against the expiry check
    /// ever regressing from "&lt;=" to a strict "&lt;" that would treat the boundary instant as still
    /// valid.</summary>
    [Fact]
    public void Credential_expiring_at_exactly_the_current_instant_is_rejected_as_expired()
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var service = NewServiceAt(now);
        var sessionId = Guid.NewGuid();

        var credential = service.Mint(sessionId, InitiativeCredentialRole.Facilitator, TimeSpan.Zero);
        var result = service.Validate(credential, sessionId);

        Assert.False(result.Succeeded);
        Assert.Equal(InitiativeCredentialFailure.Expired, result.Failure);
    }

    /// <summary>Tamper-resistance invariant: flipping any single character anywhere in a minted
    /// credential - whether inside the payload segment, the signature segment, or the "." separator -
    /// must reliably be rejected rather than silently validating with a mutated payload. This is what
    /// makes the HMAC signature meaningful: without it, a client could edit the base64url session/role
    /// fields directly.</summary>
    [Fact]
    public void Tampering_with_any_character_of_a_minted_credential_is_rejected()
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_.";

        var gen =
            from sessionId in GuidGen
            from role in RoleGen
            from replacement in Gen.Int[0, alphabet.Length - 1].Select(i => alphabet[i])
            select (sessionId, role, replacement);

        gen.Sample(t =>
        {
            var service = NewService();
            var credential = service.Mint(t.sessionId, t.role);

            var index = Random.Shared.Next(credential.Length);
            if (credential[index] == t.replacement) return; // no-op tamper; not a meaningful case

            var tampered = string.Concat(credential[..index], t.replacement, credential[(index + 1)..]);

            var result = service.Validate(tampered, t.sessionId);

            Assert.False(result.Succeeded);
        });
    }

    /// <summary>A credential minted by one signing key must never validate under a different signing
    /// key - otherwise rotating <c>Initiative:CredentialSigningKey</c>, or two independently
    /// process-random keys across a restart, would silently accept stale credentials.</summary>
    [Fact]
    public void Credential_minted_under_one_signing_key_never_validates_under_another()
    {
        var gen =
            from sessionId in GuidGen
            from role in RoleGen
            select (sessionId, role);

        gen.Sample(t =>
        {
            var mintingService = NewServiceWithRandomKey();
            var credential = mintingService.Mint(t.sessionId, t.role);

            var validatingService = NewServiceWithRandomKey();
            var result = validatingService.Validate(credential, t.sessionId);

            Assert.False(result.Succeeded);
        });
    }

    /// <summary>A credential minted with a foreign issuer/audience (correctly signed with the same
    /// key, but stamped for a different <c>iss</c>/<c>aud</c> — e.g. a different deployment sharing
    /// the same signing secret, or a token minted for an unrelated purpose entirely) must never
    /// validate. Before issuer/audience validation was wired up, any correctly-signed JWT was
    /// accepted regardless of what minted it or who it was for.</summary>
    [Fact]
    public void Credential_minted_with_foreign_issuer_and_audience_is_rejected()
    {
        var gen =
            from sessionId in GuidGen
            from role in RoleGen
            select (sessionId, role);

        gen.Sample(t =>
        {
            var foreignService = NewServiceWithForeignIssuerAndAudience();
            var credential = foreignService.Mint(t.sessionId, t.role);

            var service = NewService();
            var result = service.Validate(credential, t.sessionId);

            Assert.False(result.Succeeded);
            Assert.Equal(InitiativeCredentialFailure.Malformed, result.Failure);
        });
    }

    /// <summary>Round-trip invariant for the issuer/audience configured explicitly (not just the
    /// defaults exercised implicitly by every other test in this class): minting and validating with
    /// the same configured issuer/audience must still succeed.</summary>
    [Fact]
    public void Credential_round_trips_with_explicitly_configured_issuer_and_audience()
    {
        var sessionId = Guid.NewGuid();
        var service = new HmacInitiativeCredentialService(
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Initiative:CredentialSigningKey"] = "unit-test-signing-key-do-not-use-in-prod",
                ["Initiative:CredentialIssuer"] = "custom-issuer",
                ["Initiative:CredentialAudience"] = "custom-audience",
            }).Build(),
            TimeProvider.System,
            DevelopmentEnvironment);

        var credential = service.Mint(sessionId, InitiativeCredentialRole.Facilitator);
        var result = service.Validate(credential, sessionId);

        Assert.True(result.Succeeded);
        Assert.Equal(InitiativeCredentialRole.Facilitator, result.Role);
    }
}
