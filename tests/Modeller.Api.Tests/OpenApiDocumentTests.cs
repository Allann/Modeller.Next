using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Modeller.Api.Tests;

/// <summary>
/// Exercises <c>InitiativeCredentialSecuritySchemeTransformer</c> (issue #146) through the real
/// generated OpenAPI document rather than only inspecting it manually — <c>MapOpenApi()</c> is only
/// wired in Development (<c>DevelopmentToolingEndpoints</c>), which is exactly the environment
/// <see cref="WebApplicationFactory{TEntryPoint}"/> defaults to, so this runs against the same
/// document a developer opening Scalar would see.
/// </summary>
public sealed class OpenApiDocumentTests
{
    [Fact]
    public async Task Document_declares_the_Initiative_credential_security_scheme()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var document = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        var scheme = document.GetProperty("components").GetProperty("securitySchemes").GetProperty("InitiativeCredential");
        Assert.Equal("apiKey", scheme.GetProperty("type").GetString());
        Assert.Equal("header", scheme.GetProperty("in").GetString());
        Assert.Equal("X-Initiative-Credential", scheme.GetProperty("name").GetString());
    }

    [Theory]
    [InlineData("/v1/initiative/{id}/finalize", "post")]
    [InlineData("/v1/initiative/{id}", "get")]
    [InlineData("/v1/initiative/{id}/reopen", "post")]
    public async Task Credential_requiring_operations_carry_the_security_requirement(string path, string method)
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var document = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        var operation = document.GetProperty("paths").GetProperty(path).GetProperty(method);

        Assert.True(operation.TryGetProperty("security", out var security));
        Assert.True(security.GetArrayLength() > 0);
        Assert.True(security[0].TryGetProperty("InitiativeCredential", out _));
    }

    [Theory]
    [InlineData("/v1/initiative/agent-status", "get")]
    [InlineData("/v1/initiative", "post")]
    public async Task Credential_free_operations_carry_no_security_requirement(string path, string method)
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var document = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        var operation = document.GetProperty("paths").GetProperty(path).GetProperty(method);

        if (operation.TryGetProperty("security", out var security))
            Assert.Equal(0, security.GetArrayLength());
    }
}
