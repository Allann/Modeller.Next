namespace Modeller.Initiative.OpenAICompatible;

/// <summary>
/// Configuration for any OpenAI-compatible chat-completions endpoint — LM Studio, Ollama's OpenAI
/// shim, or a real OpenAI-compatible cloud provider. Deliberately a plain record, not bound to
/// <c>Microsoft.Extensions.Options</c>, so this project stays dependency-light; a host (issue #90)
/// can bind it from configuration however it prefers.
/// </summary>
public sealed record AgentAdvisorOptions(Uri BaseUrl, string Model, string? ApiKey = null)
{
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Hard ceiling for one model response. Initiative advice is short structured JSON.</summary>
    public int MaxOutputTokens { get; init; } = 1200;

    /// <summary>Reject unexpectedly large prompts before they can create unbounded provider cost.</summary>
    public int MaxPromptCharacters { get; init; } = 24_000;

    /// <summary>
    /// Resolves a short-lived or request-scoped key. Public hosts use this to require the caller's
    /// own Gateway key instead of spending the host's credits. The value must never be logged.
    /// </summary>
    public Func<string?>? RequestApiKeyProvider { get; init; }

    /// <summary>Host authentication used only with <see cref="FreeModel"/> when no caller key exists.</summary>
    public Func<string?>? HostApiKeyProvider { get; init; }

    /// <summary>A Gateway model verified as free for anonymous requests. Null requires a caller key.</summary>
    public string? FreeModel { get; init; }

    public bool RequireApiKey { get; init; } = true;
}
