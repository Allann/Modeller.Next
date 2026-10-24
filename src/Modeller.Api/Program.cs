using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Modeller.Api;
using Modeller.Api.Endpoints;
using Modeller.Api.Health;
using Modeller.Api.Initiative;
using Modeller.Api.OpenApi;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// docs/coding-standards/web-apis-and-services/seq-tracing.md: every service MUST configure
// Serilog from IConfiguration and call UseSerilogRequestLogging(). No message content — request
// logging here is metadata only (method/path/status/duration), never a submitted document's text.
builder.Host.UseSerilog((context, loggerConfig) => loggerConfig.ReadFrom.Configuration(context.Configuration));

builder.Services.AddOpenApi(options =>
{
    options.AddSchemaTransformer<ExampleSchemaTransformer>();
    options.AddDocumentTransformer<InitiativeCredentialSecuritySchemeTransformer>();
});
builder.Services.AddSingleton<WorkspaceAnalysisPipeline>();
builder.Services.AddSingleton<WorkspaceGenerationPreviewPipeline>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);
// Issue #146: mints/validates the two role-scoped session credentials as JWTs. Signing key comes
// from Initiative:CredentialSigningKey so credentials survive process restarts in production; that
// key is required outside Development — HmacInitiativeCredentialService throws at construction
// (i.e. at first resolution here) if it's missing, rather than silently falling back to a
// per-process random key that would invalidate every outstanding link on the next restart/cold
// start. In Development only, an unset key falls back to a random per-process key (fine for local
// dev restarts and for tests, which mint and validate within the same process).
builder.Services.AddSingleton<IInitiativeCredentialService, HmacInitiativeCredentialService>();
builder.Services.AddProductAnalytics(builder.Configuration);
builder.Services.AddAgentAdvisor(builder.Configuration);
builder.Services.AddInitiativeSessionRepository(builder.Configuration);
builder.Services.AddScoped<InitiativePipeline>();
// Every external dependency above gets a readiness check (/healthz/ready) — see DependencyHealthChecks.
builder.Services.AddDependencyHealthChecks(builder.Configuration);
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
            .WithHeaders("Content-Type", "x-requested-with", "x-signalr-user-agent", "x-analytics-id", "x-modeller-internal", "x-agent-api-key", "x-initiative-credential")
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

if (ListenUrls.Resolve(builder.Configuration["PORT"], builder.Configuration["urls"]) is { } listenUrl)
{
    builder.WebHost.UseUrls(listenUrl);
}

var app = builder.Build();

// HmacInitiativeCredentialService is intentionally NOT force-constructed here at startup: its
// missing-signing-key check (outside Development) is deferred to first Mint/Validate call so a
// misconfigured Initiative:CredentialSigningKey only breaks Initiative endpoints, not the whole API
// (workspace/document endpoints have no dependency on it) — see the class doc comment.

app.UseSerilogRequestLogging();
app.UseCors("Playground");
app.UseRateLimiter();

app.MapWorkspaceEndpoints();
app.MapInitiativeEndpoints();
app.MapHub<InitiativeHub>("/hubs/initiative");
// Liveness runs no checks: it only proves the process serves HTTP, so a dependency outage never
// makes the platform restart a healthy process. Readiness runs every dependency check and reports
// each one by name; Degraded (an optional add-on is down) still answers 200, Unhealthy answers 503.
app.MapHealthChecks("/healthz/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/healthz/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains(DependencyHealthChecks.ReadyTag),
    ResponseWriter = DependencyHealthChecks.WriteResponse,
});

app.MapDevelopmentTooling();

app.Run();

/// <summary>Exposed so <c>WebApplicationFactory&lt;Program&gt;</c> can host this app in-process for integration tests.</summary>
public partial class Program;
