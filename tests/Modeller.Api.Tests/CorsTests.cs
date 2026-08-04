using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Modeller.Api.Tests;

/// <summary>
/// A browser's CORS preflight for a JSON POST sends
/// <c>Access-Control-Request-Headers: Content-Type</c>; the response must echo that header back
/// as allowed, or the browser blocks the real request even though the origin itself is permitted.
/// </summary>
public sealed class CorsTests
{
    [Fact]
    public async Task Preflight_for_a_configured_origin_allows_the_Content_Type_header()
    {
        // Set before the factory boots: Program.cs reads Cors:AllowedOrigins from IConfiguration
        // during WebApplicationBuilder construction, and environment variables are one of its
        // default configuration sources — this is the most reliable way to inject an allowed
        // origin for a minimal-hosting-model app under WebApplicationFactory.
        Environment.SetEnvironmentVariable("Cors__AllowedOrigins__0", "https://modeller.website");
        try
        {
            using var factory = new WebApplicationFactory<Program>();
            using var client = factory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Options, "/v1/workspace/analyze");
            request.Headers.Add("Origin", "https://modeller.website");
            request.Headers.Add("Access-Control-Request-Method", "POST");
            request.Headers.Add("Access-Control-Request-Headers", "Content-Type");

            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

            Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Headers", out var allowedHeaders));
            Assert.Contains(allowedHeaders!, value => value.Contains("Content-Type", StringComparison.OrdinalIgnoreCase));
            Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var allowedOrigins));
            Assert.Contains("https://modeller.website", allowedOrigins!);
        }
        finally
        {
            Environment.SetEnvironmentVariable("Cors__AllowedOrigins__0", null);
        }
    }
}
