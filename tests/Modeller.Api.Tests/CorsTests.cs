using System.Net;
using Microsoft.AspNetCore.Hosting;
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

    /// <summary>
    /// The deployed site canonicalizes to the <c>www</c> host (the apex redirects to it), so that is
    /// the Origin every real browser sends. Listing only the apex is what took the deployed
    /// playground and Initiative form down with an opaque "Failed to fetch", so the shipped
    /// Production configuration — not a test-injected origin — is what this asserts.
    /// </summary>
    [Theory]
    [InlineData("https://www.modeller.website")]
    [InlineData("https://modeller.website")]
    public async Task Production_configuration_allows_every_host_the_site_is_served_from(string origin)
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("Production"));
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/v1/workspace/analyze");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "Content-Type");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var allowedOrigins));
        Assert.Contains(origin, allowedOrigins!);
    }

    /// <summary>
    /// The SignalR JavaScript client sends <c>withCredentials</c> and its own <c>x-*</c> headers on
    /// the negotiate request. Without matching allowances the hub connection fails its handshake and
    /// the Initiative pages quietly stop updating live, since a failed connection is non-fatal by
    /// design (apps/website/src/lib/useInitiativeSession.ts).
    /// </summary>
    [Fact]
    public async Task Preflight_for_the_Initiative_hub_allows_the_SignalR_client_handshake()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("Production"));
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/hubs/initiative/negotiate");
        request.Headers.Add("Origin", "https://www.modeller.website");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "x-requested-with,x-signalr-user-agent");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Headers", out var allowedHeaders));
        var allowed = string.Join(',', allowedHeaders!);
        Assert.Contains("x-requested-with", allowed, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("x-signalr-user-agent", allowed, StringComparison.OrdinalIgnoreCase);
        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Credentials", out var credentials));
        Assert.Contains("true", credentials!);
    }
}
