using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Modeller.Api.Initiative;

namespace Modeller.Api.Health;

/// <summary>
/// Readiness checks for every external dependency this API talks to. <c>/healthz/live</c> runs none
/// of them (it only proves the process serves HTTP); <c>/healthz/ready</c> runs all of them, so a
/// dependency that has gone away — e.g. a deleted Upstash database whose hostname no longer
/// resolves — shows up by name there instead of only as an empty 500 from the first Initiative
/// request that happens to need it.
/// </summary>
public static class DependencyHealthChecks
{
    public const string ReadyTag = "ready";

    /// <summary>Upper bound per check, so one hung dependency can't stall the whole readiness probe.</summary>
    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(5);

    internal const string AgentProbeClient = "health-agent-advisor";
    internal const string AnalyticsProbeClient = "health-product-analytics";

    public static IServiceCollection AddDependencyHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        var agentBaseUrl = ExternalDependencyRegistration.AgentBaseUrl(configuration);
        var analyticsHost = ExternalDependencyRegistration.ProductAnalyticsHost(configuration);
        var checks = services.AddHealthChecks()
            .AddCheck<InitiativeStorageHealthCheck>("initiative-storage", HealthStatus.Unhealthy, [ReadyTag], CheckTimeout)
            .AddCheck<InitiativeCredentialsHealthCheck>("initiative-credentials", HealthStatus.Unhealthy, [ReadyTag], CheckTimeout);

        // The Agent Advisor and product analytics are optional add-ons (#83/#86): when configured but
        // unreachable they degrade the API, they never make it unready.
        if (agentBaseUrl is not null)
        {
            services.AddHttpClient(AgentProbeClient, client => client.BaseAddress = WithTrailingSlash(agentBaseUrl));
            checks.AddCheck<AgentAdvisorHealthCheck>("agent-advisor", HealthStatus.Degraded, [ReadyTag], CheckTimeout);
        }

        if (analyticsHost is not null)
        {
            services.AddHttpClient(AnalyticsProbeClient, client => client.BaseAddress = WithTrailingSlash(analyticsHost));
            checks.AddCheck<ProductAnalyticsHealthCheck>("product-analytics", HealthStatus.Degraded, [ReadyTag], CheckTimeout);
        }

        return services;
    }

    /// <summary>
    /// Writes each check's name, status and duration. Exception details are deliberately left out of
    /// the response (the health check service already logs them) — they can carry hostnames and other
    /// infrastructure detail that an anonymous caller has no need to see.
    /// </summary>
    public static Task WriteResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        var body = new
        {
            status = report.Status.ToString(),
            durationMs = Math.Round(report.TotalDuration.TotalMilliseconds),
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description,
                durationMs = Math.Round(entry.Value.Duration.TotalMilliseconds),
            }),
        };
        return context.Response.WriteAsync(JsonSerializer.Serialize(body, JsonSerializerOptions.Web), context.RequestAborted);
    }

    private static Uri WithTrailingSlash(Uri uri) =>
        uri.AbsoluteUri.EndsWith('/') ? uri : new Uri(uri.AbsoluteUri + "/");
}

/// <summary>Whichever session store is configured (Upstash on Vercel, JSON files locally) must answer.</summary>
internal sealed class InitiativeStorageHealthCheck(IInitiativeSessionRepository repository) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await repository.ProbeAsync(cancellationToken);
            return HealthCheckResult.Healthy("Initiative session storage is reachable.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, "Initiative session storage is unreachable.", exception);
        }
    }
}

/// <summary>
/// Mints a throwaway credential, which exercises the whole signing path — a missing
/// <c>Initiative:CredentialSigningKey</c> and one too short for HS256 both fail here rather than on
/// the first real "start an Initiative" request.
/// </summary>
internal sealed class InitiativeCredentialsHealthCheck(IInitiativeCredentialService credentials) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            credentials.Mint(Guid.Empty, InitiativeCredentialRole.Facilitator, TimeSpan.FromMinutes(1));
            return Task.FromResult(HealthCheckResult.Healthy("Initiative credentials can be signed."));
        }
        catch (Exception exception)
        {
            return Task.FromResult(new HealthCheckResult(
                context.Registration.FailureStatus, "Initiative credentials cannot be signed — check Initiative:CredentialSigningKey.", exception));
        }
    }
}

/// <summary>
/// <c>GET {base}/models</c> is the cheapest call every OpenAI-compatible endpoint (Vercel AI Gateway,
/// LM Studio, Ollama) answers — it spends no tokens and needs no caller key.
/// </summary>
internal sealed class AgentAdvisorHealthCheck(IHttpClientFactory httpClientFactory) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        HttpProbe.CheckAsync(httpClientFactory.CreateClient(DependencyHealthChecks.AgentProbeClient), "models",
            response => response.IsSuccessStatusCode, "Agent Advisor endpoint", context, cancellationToken);
}

/// <summary>
/// PostHog has no stable unauthenticated health route, so any non-5xx answer from the ingestion host
/// counts as reachable — the point is to catch DNS/TLS/outage failures, not to validate the key.
/// </summary>
internal sealed class ProductAnalyticsHealthCheck(IHttpClientFactory httpClientFactory) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        HttpProbe.CheckAsync(httpClientFactory.CreateClient(DependencyHealthChecks.AnalyticsProbeClient), "",
            response => (int)response.StatusCode < 500, "Product analytics host", context, cancellationToken);
}

internal static class HttpProbe
{
    public static async Task<HealthCheckResult> CheckAsync(
        HttpClient client, string path, Func<HttpResponseMessage, bool> isHealthy, string dependency,
        HealthCheckContext context, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.GetAsync(path, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            return isHealthy(response)
                ? HealthCheckResult.Healthy($"{dependency} is reachable.")
                : new HealthCheckResult(context.Registration.FailureStatus, $"{dependency} answered HTTP {(int)response.StatusCode}.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, $"{dependency} is unreachable.", exception);
        }
    }
}
