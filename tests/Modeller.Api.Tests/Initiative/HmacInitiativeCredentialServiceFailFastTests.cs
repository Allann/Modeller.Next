using Microsoft.Extensions.Configuration;
using Modeller.Api.Initiative;
using Xunit;

namespace Modeller.Api.Tests.Initiative;

/// <summary>
/// Covers the fail-fast requirement for <see cref="HmacInitiativeCredentialService"/> (issue #146,
/// P1 hardening finding): a missing <c>Initiative:CredentialSigningKey</c> must not silently fall
/// back to a random per-process key outside Development, because every credential minted under such
/// a key is invalidated the moment the process restarts or cold-starts (e.g. on Vercel) — well
/// before any credential's 30-day TTL, with no warning to whoever is holding the link.
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
    public void Construction_throws_outside_development_when_signing_key_is_not_configured(string environmentName)
    {
        var environment = new InitiativeCredentialsPropertyTests.FakeHostEnvironment(environmentName);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new HmacInitiativeCredentialService(ConfigurationWithoutSigningKey(), TimeProvider.System, environment));

        Assert.Contains("Initiative:CredentialSigningKey", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("Development")]
    public void Construction_succeeds_in_any_environment_when_signing_key_is_configured(string environmentName)
    {
        var environment = new InitiativeCredentialsPropertyTests.FakeHostEnvironment(environmentName);

        var service = new HmacInitiativeCredentialService(ConfigurationWithSigningKey(), TimeProvider.System, environment);

        // Round-trips a credential to prove the constructed instance is actually usable, not merely
        // that construction didn't throw.
        var sessionId = Guid.NewGuid();
        var credential = service.Mint(sessionId, InitiativeCredentialRole.Facilitator);
        var result = service.Validate(credential, sessionId);
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Construction_does_not_throw_in_development_when_signing_key_is_not_configured()
    {
        var environment = new InitiativeCredentialsPropertyTests.FakeHostEnvironment("Development");

        var service = new HmacInitiativeCredentialService(ConfigurationWithoutSigningKey(), TimeProvider.System, environment);

        var sessionId = Guid.NewGuid();
        var credential = service.Mint(sessionId, InitiativeCredentialRole.DomainExpert);
        var result = service.Validate(credential, sessionId);
        Assert.True(result.Succeeded);
    }
}
