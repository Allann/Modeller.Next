namespace Modeller.Api;

/// <summary>
/// Chooses the address Kestrel binds to. The hosting platform's <c>$PORT</c> wins; otherwise a
/// launch profile's <c>ASPNETCORE_URLS</c> is kept, so F5 opens the address Kestrel actually
/// binds; otherwise the API falls back to port 8080 for a plain local run.
/// </summary>
public static class ListenUrls
{
    public const string FallbackUrl = "http://0.0.0.0:8080";

    /// <summary>Returns the URL to pass to <c>UseUrls</c>, or <c>null</c> to keep the configured
    /// <c>urls</c> unchanged.</summary>
    public static string? Resolve(string? port, string? configuredUrls)
    {
        if (!string.IsNullOrEmpty(port)) return $"http://0.0.0.0:{port}";
        return string.IsNullOrEmpty(configuredUrls) ? FallbackUrl : null;
    }
}
