using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Modeller.Api;
using Modeller.Api.Endpoints;
using Modeller.Api.Initiative;
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

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddSingleton<WorkspaceAnalysisPipeline>();

// Initiative (issue #90): the Agent Advisor is an add-on, never a hard dependency, per #83/#86 —
// only registered as the real OpenAI-compatible adapter when an endpoint is actually configured;
// otherwise every Discover/Frame/Shape action runs fully human-only via HumanOnlyAgentAdvisor.
var agentBaseUrl = builder.Configuration["Agent:BaseUrl"];
if (!string.IsNullOrWhiteSpace(agentBaseUrl))
{
    builder.Services.AddSingleton(new AgentAdvisorOptions(
        new Uri(agentBaseUrl),
        builder.Configuration["Agent:Model"] ?? throw new InvalidOperationException("Agent:Model is required when Agent:BaseUrl is set."),
        builder.Configuration["Agent:ApiKey"]));
    builder.Services.AddHttpClient<IAgentAdvisor, OpenAiCompatibleAgentAdvisor>();
}
else
{
    builder.Services.AddSingleton<IAgentAdvisor, HumanOnlyAgentAdvisor>();
}

var initiativeStorageRoot = builder.Configuration["Initiative:StorageRoot"]
    ?? Path.Combine(AppContext.BaseDirectory, "data", "initiative");
builder.Services.AddSingleton<IInitiativeSessionRepository>(new JsonFileInitiativeSessionRepository(initiativeStorageRoot));
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
    if (allowedOrigins.Length > 0) policy.WithOrigins(allowedOrigins).WithMethods("GET", "POST").WithHeaders("Content-Type");
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
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter());

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

if (app.Environment.IsDevelopment()) app.MapOpenApi();

app.Run();

/// <summary>Exposed so <c>WebApplicationFactory&lt;Program&gt;</c> can host this app in-process for integration tests.</summary>
public partial class Program;
