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
}
