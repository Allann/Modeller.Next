using Modeller.Api.Analytics;
using Modeller.Api.Initiative;
using Modeller.Initiative;
using Modeller.Initiative.OpenAICompatible;

namespace Modeller.Api;

/// <summary>
/// Registers the API's configuration-selected external dependencies — product analytics, the Agent
/// Advisor, and the Initiative session store — each as its real adapter when configured, otherwise
/// as its local/disabled fallback. <see cref="Health.DependencyHealthChecks"/> reads the same
/// configuration (via <see cref="ProductAnalyticsHost"/>/<see cref="AgentBaseUrl"/>) so a readiness
/// check exists for exactly the dependencies that are actually in use.
/// </summary>
public static class ExternalDependencyRegistration
{
    public static Uri? ProductAnalyticsHost(IConfiguration configuration) =>
        string.IsNullOrWhiteSpace(configuration["ProductAnalytics:ProjectKey"])
            ? null
            : new Uri((configuration["ProductAnalytics:Host"] ?? "https://us.i.posthog.com").TrimEnd('/') + "/");

    public static Uri? AgentBaseUrl(IConfiguration configuration) =>
        configuration["Agent:BaseUrl"] is { } baseUrl && !string.IsNullOrWhiteSpace(baseUrl) ? new Uri(baseUrl) : null;

    public static IServiceCollection AddProductAnalytics(this IServiceCollection services, IConfiguration configuration)
    {
        if (ProductAnalyticsHost(configuration) is { } postHogHost)
            services.AddHttpClient<IProductAnalytics, PostHogProductAnalytics>(client => client.BaseAddress = postHogHost);
        else
            services.AddSingleton<IProductAnalytics, DisabledProductAnalytics>();
        return services;
    }

    /// <summary>
    /// Initiative (issue #90): the Agent Advisor is an add-on, never a hard dependency, per #83/#86 —
    /// only registered as the real OpenAI-compatible adapter when an endpoint is actually configured;
    /// otherwise every Discover/Frame/Shape action runs fully human-only via HumanOnlyAgentAdvisor.
    /// </summary>
    public static IServiceCollection AddAgentAdvisor(this IServiceCollection services, IConfiguration configuration)
    {
        if (AgentBaseUrl(configuration) is not { } agentBaseUrl)
        {
            services.AddSingleton<IAgentAdvisor, HumanOnlyAgentAdvisor>();
            services.AddSingleton(new AgentAdvisorStatusResponse(false, null, true, null));
            return services;
        }

        var timeoutSeconds = configuration.GetValue("Agent:TimeoutSeconds", 30);
        var maxOutputTokens = configuration.GetValue("Agent:MaxOutputTokens", 1200);
        var maxPromptCharacters = configuration.GetValue("Agent:MaxPromptCharacters", 24_000);
        var freeModel = configuration["Agent:FreeModel"];
        if (timeoutSeconds is < 1 or > 120) throw new InvalidOperationException("Agent:TimeoutSeconds must be from 1 to 120.");
        if (maxOutputTokens is < 100 or > 8_000) throw new InvalidOperationException("Agent:MaxOutputTokens must be from 100 to 8000.");
        if (maxPromptCharacters is < 1_000 or > 200_000) throw new InvalidOperationException("Agent:MaxPromptCharacters must be from 1000 to 200000.");

        var agentModel = configuration["Agent:Model"]
            ?? throw new InvalidOperationException("Agent:Model is required when Agent:BaseUrl is set.");
        services.AddSingleton(serviceProvider =>
        {
            var httpContextAccessor = serviceProvider.GetRequiredService<IHttpContextAccessor>();
            return new AgentAdvisorOptions(
                agentBaseUrl,
                agentModel,
                configuration["Agent:ApiKey"] ?? configuration["AI_GATEWAY_API_KEY"])
            {
                Timeout = TimeSpan.FromSeconds(timeoutSeconds),
                MaxOutputTokens = maxOutputTokens,
                MaxPromptCharacters = maxPromptCharacters,
                RequireApiKey = true,
                FreeModel = freeModel,
                RequestApiKeyProvider = () => httpContextAccessor.HttpContext?.Request.Headers["X-Agent-Api-Key"].FirstOrDefault(),
                HostApiKeyProvider = () =>
                    httpContextAccessor.HttpContext?.Request.Headers["X-Vercel-Oidc-Token"].FirstOrDefault()
                    ?? configuration["VERCEL_OIDC_TOKEN"],
            };
        });
        services.AddSingleton(new AgentAdvisorStatusResponse(true, agentModel, freeModel is null, freeModel));
        services.AddHttpClient<IAgentAdvisor, OpenAiCompatibleAgentAdvisor>(client =>
            client.Timeout = Timeout.InfiniteTimeSpan);
        return services;
    }

    public static IServiceCollection AddInitiativeSessionRepository(this IServiceCollection services, IConfiguration configuration)
    {
        var useUpstash = string.Equals(configuration["Initiative:Repository"], "Upstash", StringComparison.OrdinalIgnoreCase);
        var upstashUrl = configuration["KV_REST_API_URL"]
            ?? configuration["UPSTASH_REDIS_REST_URL"];
        var upstashToken = configuration["KV_REST_API_TOKEN"]
            ?? configuration["UPSTASH_REDIS_REST_TOKEN"];
        if (useUpstash && !string.IsNullOrWhiteSpace(upstashUrl) && !string.IsNullOrWhiteSpace(upstashToken))
        {
            services.AddHttpClient<IInitiativeSessionRepository, UpstashInitiativeSessionRepository>(client =>
            {
                client.BaseAddress = new Uri(upstashUrl.TrimEnd('/') + "/");
                client.Timeout = TimeSpan.FromSeconds(10);
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", upstashToken);
            });
            return services;
        }

        if (useUpstash && string.Equals(configuration["VERCEL"], "1", StringComparison.Ordinal))
            throw new InvalidOperationException(
                "KV_REST_API_URL and KV_REST_API_TOKEN are required when Initiative:Repository is Upstash on Vercel.");

        var initiativeStorageRoot = configuration["Initiative:StorageRoot"]
            ?? Path.Combine(AppContext.BaseDirectory, "data", "initiative");
        services.AddSingleton<IInitiativeSessionRepository>(new JsonFileInitiativeSessionRepository(initiativeStorageRoot));
        return services;
    }
}
