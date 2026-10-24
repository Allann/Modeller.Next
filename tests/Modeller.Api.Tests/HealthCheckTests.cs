using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Modeller.Api.Initiative;
using Modeller.Initiative;
using Xunit;

namespace Modeller.Api.Tests;

/// <summary>
/// <c>/healthz/ready</c> must name the dependency that is down. Before these checks existed, a deleted
/// Upstash database left both health endpoints answering "Healthy" while every Initiative create
/// failed with an empty 500.
/// </summary>
public sealed class HealthCheckTests
{
    private const string ValidSigningKey = "health-test-signing-key-0123456789abcdef";

    [Fact]
    public async Task Ready_reports_every_required_dependency_by_name_when_all_are_healthy()
    {
        using var factory = CreateFactory("Development");
        using var client = factory.CreateClient();

        var (status, report) = await GetReadyAsync(client);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("Healthy", report.GetProperty("status").GetString());
        Assert.Equal("Healthy", CheckStatus(report, "initiative-storage"));
        Assert.Equal("Healthy", CheckStatus(report, "initiative-credentials"));
    }

    [Fact]
    public async Task Ready_is_unhealthy_and_names_storage_when_the_session_store_is_unreachable()
    {
        using var factory = CreateFactory("Development", services =>
            services.AddSingleton<IInitiativeSessionRepository>(new UnreachableRepository()));
        using var client = factory.CreateClient();

        var (status, report) = await GetReadyAsync(client);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, status);
        Assert.Equal("Unhealthy", CheckStatus(report, "initiative-storage"));
        Assert.Equal("Healthy", CheckStatus(report, "initiative-credentials"));
        Assert.DoesNotContain(UnreachableRepository.Detail, report.GetRawText(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("too-short")]
    public async Task Ready_is_unhealthy_and_names_credentials_when_the_signing_key_is_missing_or_too_short(string? signingKey)
    {
        using var factory = CreateFactory("Production", configuration: new() { ["Initiative:CredentialSigningKey"] = signingKey });
        using var client = factory.CreateClient();

        var (status, report) = await GetReadyAsync(client);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, status);
        Assert.Equal("Unhealthy", CheckStatus(report, "initiative-credentials"));
    }

    [Fact]
    public async Task Live_stays_healthy_when_a_dependency_is_down()
    {
        using var factory = CreateFactory("Production", configuration: new() { ["Initiative:CredentialSigningKey"] = null });
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/healthz/live", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Ready_is_only_degraded_when_the_optional_Agent_Advisor_is_unreachable()
    {
        using var factory = CreateFactory("Development", configuration: new()
        {
            // Port 1 on loopback refuses the connection immediately — no real network call.
            ["Agent:BaseUrl"] = "http://127.0.0.1:1/v1",
            ["Agent:Model"] = "test-model",
        });
        using var client = factory.CreateClient();

        var (status, report) = await GetReadyAsync(client);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("Degraded", report.GetProperty("status").GetString());
        Assert.Equal("Degraded", CheckStatus(report, "agent-advisor"));
    }

    [Fact]
    public async Task Ready_omits_checks_for_optional_dependencies_that_are_not_configured()
    {
        using var factory = CreateFactory("Development");
        using var client = factory.CreateClient();

        var (_, report) = await GetReadyAsync(client);

        var names = report.GetProperty("checks").EnumerateArray().Select(check => check.GetProperty("name").GetString());
        Assert.DoesNotContain("agent-advisor", names);
        Assert.DoesNotContain("product-analytics", names);
    }

    private static WebApplicationFactory<Program> CreateFactory(
        string environment,
        Action<IServiceCollection>? services = null,
        Dictionary<string, string?>? configuration = null)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Initiative:CredentialSigningKey"] = ValidSigningKey,
            ["Initiative:StorageRoot"] = Path.Combine(Path.GetTempPath(), "modeller-health-tests"),
        };
        foreach (var (key, value) in configuration ?? []) settings[key] = value;

        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            // UseSetting, not ConfigureAppConfiguration: Program.cs reads Agent:BaseUrl and friends
            // while the WebApplicationBuilder is still being configured, and only host settings are
            // visible that early under the minimal hosting model.
            foreach (var (key, value) in settings) builder.UseSetting(key, value);
            builder.ConfigureTestServices(collection => services?.Invoke(collection));
        });
    }

    private static async Task<(HttpStatusCode Status, JsonElement Report)> GetReadyAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/healthz/ready", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return (response.StatusCode, JsonDocument.Parse(body).RootElement.Clone());
    }

    private static string? CheckStatus(JsonElement report, string name) =>
        report.GetProperty("checks").EnumerateArray()
            .Single(check => check.GetProperty("name").GetString() == name)
            .GetProperty("status").GetString();

    private sealed class UnreachableRepository : IInitiativeSessionRepository
    {
        public const string Detail = "Name or service not known (example.upstash.io:443)";

        public Task SaveAsync(InitiativeSession session, CancellationToken cancellationToken = default) =>
            throw new HttpRequestException(Detail);

        public Task<InitiativeSession?> LoadAsync(InitiativeId id, CancellationToken cancellationToken = default) =>
            throw new HttpRequestException(Detail);

        public Task ProbeAsync(CancellationToken cancellationToken = default) => throw new HttpRequestException(Detail);
    }
}
