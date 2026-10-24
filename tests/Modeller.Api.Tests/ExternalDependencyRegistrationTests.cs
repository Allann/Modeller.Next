using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modeller.Api.Initiative;
using Xunit;

namespace Modeller.Api.Tests;

public sealed class ExternalDependencyRegistrationTests
{
    [Theory]
    [InlineData("KV_REST_API_URL", "KV_REST_API_TOKEN")]
    [InlineData("UPSTASH_REDIS_REST_URL", "UPSTASH_REDIS_REST_TOKEN")]
    public void Upstash_repository_is_used_when_selected_and_its_credentials_are_present(string urlKey, string tokenKey)
    {
        var repository = ResolveRepository(new()
        {
            ["Initiative:Repository"] = "Upstash",
            [urlKey] = "https://example.upstash.io",
            [tokenKey] = "token",
        });

        Assert.IsType<UpstashInitiativeSessionRepository>(repository);
    }

    [Fact]
    public void Upstash_without_credentials_on_Vercel_fails_fast()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => ResolveRepository(new()
        {
            ["Initiative:Repository"] = "Upstash",
            ["VERCEL"] = "1",
        }));

        Assert.Contains("KV_REST_API_URL", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Upstash")]
    [InlineData(null)]
    public void File_repository_is_the_fallback_off_Vercel(string? repositoryKind)
    {
        var repository = ResolveRepository(new()
        {
            ["Initiative:Repository"] = repositoryKind,
            ["Initiative:StorageRoot"] = Path.Combine(Path.GetTempPath(), "modeller-registration-tests"),
        });

        Assert.IsType<JsonFileInitiativeSessionRepository>(repository);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("phc_key", "https://us.i.posthog.com/")]
    public void Product_analytics_host_is_only_set_when_a_project_key_is_configured(string? projectKey, string? expectedHost)
    {
        var host = ExternalDependencyRegistration.ProductAnalyticsHost(Configuration(new() { ["ProductAnalytics:ProjectKey"] = projectKey }));

        Assert.Equal(expectedHost, host?.AbsoluteUri);
    }

    private static IInitiativeSessionRepository ResolveRepository(Dictionary<string, string?> settings)
    {
        var services = new ServiceCollection();
        services.AddInitiativeSessionRepository(Configuration(settings));
        return services.BuildServiceProvider().GetRequiredService<IInitiativeSessionRepository>();
    }

    private static IConfiguration Configuration(Dictionary<string, string?> settings) =>
        new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
}
