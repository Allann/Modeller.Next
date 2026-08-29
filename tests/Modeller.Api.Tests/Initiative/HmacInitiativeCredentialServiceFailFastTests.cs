using Microsoft.Extensions.Configuration;
using Modeller.Api.Initiative;
using Xunit;

namespace Modeller.Api.Tests.Initiative;

/// <summary>
/// Covers the fail-fast requirement for <see cref="HmacInitiativeCredentialService"/> (issue #146,
/// P1 hardening finding, later scoped after a real container smoke-test failure): a missing
/// <c>Initiative:CredentialSigningKey</c> must not silently fall back to a random per-process key
/// outside Development, because every credential minted under such a key is invalidated the moment
/// the process restarts or cold-starts (e.g. on Vercel) — well before any credential's 30-day TTL,
/// with no warning to whoever is holding the link. That check is deliberately deferred to first
/// <see cref="HmacInitiativeCredentialService.Mint"/>/<see cref="HmacInitiativeCredentialService.Validate"/>
/// call rather than done at construction: construction throwing would let one missing Initiative-only
/// setting take the entire API down (workspace/document endpoints included), which is exactly what
/// broke the container smoke test in CI when it ran with no Initiative config at all.
/// </summary>
public sealed class HmacInitiativeCredentialServiceFailFastTests
{
    private static IConfiguration ConfigurationWithSigningKey() =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Initiative:CredentialSigningKey"] = "unit-test-signing-key-do-not-use-in-prod",
        }).Build();

    private static IConfiguration ConfigurationWithoutSigningKey() =>
        new ConfigurationBuilder().Build();

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("SomeOtherNonDevelopmentName")]
    public void Construction_never_throws_even_when_signing_key_is_not_configured(string environmentName)
    {
        var environment = new InitiativeCredentialsPropertyTests.FakeHostEnvironment(environmentName);

        // Construction must succeed unconditionally — DI resolution/startup must never fail over
        // this, so that a missing key only breaks Initiative endpoints, not the whole API.
        var service = new HmacInitiativeCredentialService(ConfigurationWithoutSigningKey(), TimeProvider.System, environment);

        Assert.NotNull(service);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("SomeOtherNonDevelopmentName")]
    public void Mint_throws_outside_development_when_signing_key_is_not_configured(string environmentName)
    {
        var environment = new InitiativeCredentialsPropertyTests.FakeHostEnvironment(environmentName);
        var service = new HmacInitiativeCredentialService(ConfigurationWithoutSigningKey(), TimeProvider.System, environment);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            service.Mint(Guid.NewGuid(), InitiativeCredentialRole.Facilitator));

        Assert.Contains("Initiative:CredentialSigningKey", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("SomeOtherNonDevelopmentName")]
    public void Validate_throws_outside_development_when_signing_key_is_not_configured_and_a_credential_is_presented(string environmentName)
    {
        var environment = new InitiativeCredentialsPropertyTests.FakeHostEnvironment(environmentName);
        var service = new HmacInitiativeCredentialService(ConfigurationWithoutSigningKey(), TimeProvider.System, environment);

        Assert.Throws<InvalidOperationException>(() => service.Validate("not-empty", Guid.NewGuid()));
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("SomeOtherNonDevelopmentName")]
    public void Validate_still_reports_Missing_when_signing_key_is_not_configured_and_no_credential_is_presented(string environmentName)
    {
        var environment = new InitiativeCredentialsPropertyTests.FakeHostEnvironment(environmentName);
        var service = new HmacInitiativeCredentialService(ConfigurationWithoutSigningKey(), TimeProvider.System, environment);

        var result = service.Validate(null, Guid.NewGuid());

        Assert.False(result.Succeeded);
        Assert.Equal(InitiativeCredentialFailure.Missing, result.Failure);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("Development")]
    public void Mint_and_Validate_succeed_in_any_environment_when_signing_key_is_configured(string environmentName)
    {
        var environment = new InitiativeCredentialsPropertyTests.FakeHostEnvironment(environmentName);

        var service = new HmacInitiativeCredentialService(ConfigurationWithSigningKey(), TimeProvider.System, environment);

        var sessionId = Guid.NewGuid();
        var credential = service.Mint(sessionId, InitiativeCredentialRole.Facilitator);
        var result = service.Validate(credential, sessionId);
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Mint_and_Validate_succeed_in_development_when_signing_key_is_not_configured()
    {
        var environment = new InitiativeCredentialsPropertyTests.FakeHostEnvironment("Development");

        var service = new HmacInitiativeCredentialService(ConfigurationWithoutSigningKey(), TimeProvider.System, environment);

        var sessionId = Guid.NewGuid();
        var credential = service.Mint(sessionId, InitiativeCredentialRole.DomainExpert);
        var result = service.Validate(credential, sessionId);
        Assert.True(result.Succeeded);
    }
}
