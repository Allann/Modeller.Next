using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Modeller.Api.Analytics;

public static class ProductEvents
{
    public const string InitiativeCreated = "initiative_created";
    public const string InitiativeViewed = "initiative_viewed";
    public const string QuestionProposed = "question_proposed";
    public const string QuestionSent = "question_sent";
    public const string ResponseSubmitted = "response_submitted";
    public const string ResponseAccepted = "response_accepted";
    public const string GateEvaluated = "gate_evaluated";
    public const string InterventionSelected = "intervention_selected";
    public const string InitiativeFinalized = "initiative_finalized";
    public const string InitiativeReopened = "initiative_reopened";
    public const string InitiativePhaseReached = "initiative_phase_reached";
}

public interface IProductAnalytics
{
    Task CaptureAsync(string eventName, Guid initiativeId, IReadOnlyDictionary<string, object?>? properties = null,
        CancellationToken cancellationToken = default);
}

public sealed class DisabledProductAnalytics : IProductAnalytics
{
    public Task CaptureAsync(string eventName, Guid initiativeId, IReadOnlyDictionary<string, object?>? properties = null,
        CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public sealed partial class PostHogProductAnalytics(
    HttpClient client,
    IHttpContextAccessor contextAccessor,
    IConfiguration configuration,
    ILogger<PostHogProductAnalytics> logger) : IProductAnalytics
{
    private const string AnalyticsHeader = "X-Analytics-Id";
    private static readonly HashSet<string> AllowedEvents =
    [
        ProductEvents.InitiativeCreated, ProductEvents.InitiativeViewed, ProductEvents.QuestionProposed,
        ProductEvents.QuestionSent, ProductEvents.ResponseSubmitted, ProductEvents.ResponseAccepted,
        ProductEvents.GateEvaluated, ProductEvents.InterventionSelected, ProductEvents.InitiativeFinalized,
        ProductEvents.InitiativeReopened, ProductEvents.InitiativePhaseReached,
    ];
    private static readonly HashSet<string> AllowedProperties = ["viewer_role", "phase"];

    public async Task CaptureAsync(string eventName, Guid initiativeId,
        IReadOnlyDictionary<string, object?>? properties = null, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!AllowedEvents.Contains(eventName)) return;
            var rawId = contextAccessor.HttpContext?.Request.Headers[AnalyticsHeader].ToString();
            var distinctId = IsValidAnalyticsId(rawId) ? rawId! : $"server:{Hash(initiativeId)}";
            var values = new Dictionary<string, object?>(properties?.Where(pair => AllowedProperties.Contains(pair.Key))
                .ToDictionary() ?? new Dictionary<string, object?>())
            {
                ["distinct_id"] = distinctId,
                ["initiative_key"] = Hash(initiativeId),
                ["site"] = "initiative-api",
                ["environment"] = configuration["VERCEL_ENV"] ?? configuration["ASPNETCORE_ENVIRONMENT"] ?? "local",
                ["release"] = configuration["VERCEL_GIT_COMMIT_SHA"] ?? "local",
                ["internal"] = string.Equals(contextAccessor.HttpContext?.Request.Headers["X-Modeller-Internal"], "1", StringComparison.Ordinal),
                ["contract_version"] = 1,
                ["$process_person_profile"] = false,
            };
            await client.PostAsJsonAsync("capture/", new { api_key = configuration["ProductAnalytics:ProjectKey"], @event = eventName, properties = values }, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Product analytics capture failed for {EventName}", eventName);
        }
    }

    private static bool IsValidAnalyticsId(string? value) =>
        value is { Length: >= 16 and <= 128 } && AnalyticsIdPattern().IsMatch(value);

    private static string Hash(Guid id) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(id.ToString("D"))))[..24];

    [GeneratedRegex("^[A-Za-z0-9._:-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex AnalyticsIdPattern();
}
