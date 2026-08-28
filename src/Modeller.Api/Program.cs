using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Modeller.Api;
using Modeller.Api.Endpoints;
using Modeller.Api.Initiative;
using Modeller.Api.Analytics;
using Modeller.Api.OpenApi;
using Modeller.Initiative;
using Modeller.Initiative.OpenAICompatible;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// docs/coding-standards/web-apis-and-services/seq-tracing.md: every service MUST configure
// Serilog from IConfiguration and call UseSerilogRequestLogging(). No message content — request
// logging here is metadata only (method/path/status/duration), never a submitted document's text.
builder.Host.UseSerilog((context, loggerConfig) => loggerConfig.ReadFrom.Configuration(context.Configuration));

builder.Services.AddOpenApi(options => options.AddSchemaTransformer<ExampleSchemaTransformer>());
builder.Services.AddHealthChecks();
builder.Services.AddSingleton<WorkspaceAnalysisPipeline>();
builder.Services.AddSingleton<WorkspaceGenerationPreviewPipeline>();
builder.Services.AddHttpContextAccessor();
var postHogKey = builder.Configuration["ProductAnalytics:ProjectKey"];
if (!string.IsNullOrWhiteSpace(postHogKey))
{
    builder.Services.AddHttpClient<IProductAnalytics, PostHogProductAnalytics>(client =>
        client.BaseAddress = new Uri((builder.Configuration["ProductAnalytics:Host"] ?? "https://us.i.posthog.com").TrimEnd('/') + "/"));
}
else
{
    builder.Services.AddSingleton<IProductAnalytics, DisabledProductAnalytics>();
}

// Initiative (issue #90): the Agent Advisor is an add-on, never a hard dependency, per #83/#86 —
// only registered as the real OpenAI-compatible adapter when an endpoint is actually configured;
// otherwise every Discover/Frame/Shape action runs fully human-only via HumanOnlyAgentAdvisor.
var agentBaseUrl = builder.Configuration["Agent:BaseUrl"];
if (!string.IsNullOrWhiteSpace(agentBaseUrl))
{
    var timeoutSeconds = builder.Configuration.GetValue("Agent:TimeoutSeconds", 30);
    var maxOutputTokens = builder.Configuration.GetValue("Agent:MaxOutputTokens", 1200);
    var maxPromptCharacters = builder.Configuration.GetValue("Agent:MaxPromptCharacters", 24_000);
    var freeModel = builder.Configuration["Agent:FreeModel"];
    if (timeoutSeconds is < 1 or > 120) throw new InvalidOperationException("Agent:TimeoutSeconds must be from 1 to 120.");
    if (maxOutputTokens is < 100 or > 8_000) throw new InvalidOperationException("Agent:MaxOutputTokens must be from 100 to 8000.");
    if (maxPromptCharacters is < 1_000 or > 200_000) throw new InvalidOperationException("Agent:MaxPromptCharacters must be from 1000 to 200000.");

    var agentModel = builder.Configuration["Agent:Model"]
        ?? throw new InvalidOperationException("Agent:Model is required when Agent:BaseUrl is set.");
    builder.Services.AddSingleton(serviceProvider =>
    {
        var httpContextAccessor = serviceProvider.GetRequiredService<IHttpContextAccessor>();
        return new AgentAdvisorOptions(
            new Uri(agentBaseUrl),
            agentModel,
            builder.Configuration["Agent:ApiKey"] ?? builder.Configuration["AI_GATEWAY_API_KEY"])
        {
            Timeout = TimeSpan.FromSeconds(timeoutSeconds),
            MaxOutputTokens = maxOutputTokens,
            MaxPromptCharacters = maxPromptCharacters,
            RequireApiKey = true,
            FreeModel = freeModel,
            RequestApiKeyProvider = () => httpContextAccessor.HttpContext?.Request.Headers["X-Agent-Api-Key"].FirstOrDefault(),
            HostApiKeyProvider = () =>
                httpContextAccessor.HttpContext?.Request.Headers["X-Vercel-Oidc-Token"].FirstOrDefault()
                ?? builder.Configuration["VERCEL_OIDC_TOKEN"],
        };
    });
    builder.Services.AddSingleton(new AgentAdvisorStatusResponse(true, agentModel, freeModel is null, freeModel));
    builder.Services.AddHttpClient<IAgentAdvisor, OpenAiCompatibleAgentAdvisor>(client =>
        client.Timeout = Timeout.InfiniteTimeSpan);
}
else
{
    builder.Services.AddSingleton<IAgentAdvisor, HumanOnlyAgentAdvisor>();
    builder.Services.AddSingleton(new AgentAdvisorStatusResponse(false, null, true, null));
}

var initiativeRepository = builder.Configuration["Initiative:Repository"];
var useUpstash = string.Equals(initiativeRepository, "Upstash", StringComparison.OrdinalIgnoreCase);
var upstashUrl = builder.Configuration["KV_REST_API_URL"]
    ?? builder.Configuration["UPSTASH_REDIS_REST_URL"];
var upstashToken = builder.Configuration["KV_REST_API_TOKEN"]
    ?? builder.Configuration["UPSTASH_REDIS_REST_TOKEN"];
if (useUpstash && !string.IsNullOrWhiteSpace(upstashUrl) && !string.IsNullOrWhiteSpace(upstashToken))
{
    builder.Services.AddHttpClient<IInitiativeSessionRepository, UpstashInitiativeSessionRepository>(client =>
    {
        client.BaseAddress = new Uri(upstashUrl.TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromSeconds(10);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", upstashToken);
    });
}
else
{
    if (useUpstash && string.Equals(builder.Configuration["VERCEL"], "1", StringComparison.Ordinal))
        throw new InvalidOperationException(
            "KV_REST_API_URL and KV_REST_API_TOKEN are required when Initiative:Repository is Upstash on Vercel.");

    var initiativeStorageRoot = builder.Configuration["Initiative:StorageRoot"]
        ?? Path.Combine(AppContext.BaseDirectory, "data", "initiative");
    builder.Services.AddSingleton<IInitiativeSessionRepository>(new JsonFileInitiativeSessionRepository(initiativeStorageRoot));
}
builder.Services.AddScoped<InitiativePipeline>();
builder.Services.AddSignalR();
// ViewKind must serialize as a stable name, not an ordinal int — the ordinal is an
// implementation-order detail that could change without the contract itself changing.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    // A malformed request (an explicit JSON null for a required field) must fail deserialization
    // — and so become the framework's automatic 400 — rather than flow a null reference into
    // RequestLimits/the pipeline and surface as an unhandled 500.
    options.SerializerOptions.RespectNullableAnnotations = true;
});

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy("Playground", policy =>
{
    // WithHeaders("Content-Type") is required for a browser's CORS preflight to approve a JSON
    // POST — without it, Access-Control-Request-Headers: Content-Type has no matching
    // Access-Control-Allow-Headers in the preflight response and the browser blocks the request.
    // The two x-* headers and AllowCredentials are what the SignalR JavaScript client needs on top
    // of that: it sends x-requested-with/x-signalr-user-agent on its negotiate POST and sets
    // withCredentials by default, and a credentialed request is rejected outright unless the
    // response carries Access-Control-Allow-Credentials. Credentials are safe to allow here only
    // because the origins are an explicit allowlist (never a wildcard).
    if (allowedOrigins.Length > 0)
        policy.WithOrigins(allowedOrigins)
            .WithMethods("GET", "POST")
            .WithHeaders("Content-Type", "x-requested-with", "x-signalr-user-agent", "x-analytics-id", "x-modeller-internal", "x-agent-api-key")
            .AllowCredentials();
}));

var maxConcurrentRequests = builder.Configuration.GetValue("Limits:MaxConcurrentRequests", 16);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(_ =>
        RateLimitPartition.GetConcurrencyLimiter("global", _ => new ConcurrencyLimiterOptions
        {
            PermitLimit = maxConcurrentRequests,
            QueueLimit = 0,
        }));
});

// docs/coding-standards/web-apis-and-services/seq-tracing.md: every service MUST register the
// OTLP exporter, unconditionally — AddOtlpExporter() itself already honors the standard
// OTEL_EXPORTER_OTLP_ENDPOINT/OTEL_EXPORTER_OTLP_* environment variables (defaulting to
// http://localhost:4317 when unset) and fails soft (retries in the background) when no collector
// is reachable, so there is nothing to gate here.
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("modeller-api"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddMeter(ApiMetrics.MeterName)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter());

ApiMetrics.RecordProcessStart();

// Request-shape limits (RequestLimits) enforce Modeller-specific document/path/projection
// ceilings; this caps the raw HTTP payload before it's even deserialized.
builder.WebHost.ConfigureKestrel(kestrel => kestrel.Limits.MaxRequestBodySize = 2 * 1024 * 1024);

var port = builder.Configuration["PORT"] ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseCors("Playground");
app.UseRateLimiter();

app.MapWorkspaceEndpoints();
app.MapInitiativeEndpoints();
app.MapHub<InitiativeHub>("/hubs/initiative");
app.MapHealthChecks("/healthz/live");
app.MapHealthChecks("/healthz/ready");

app.MapDevelopmentTooling();

app.Run();

/// <summary>Exposed so <c>WebApplicationFactory&lt;Program&gt;</c> can host this app in-process for integration tests.</summary>
public partial class Program;
