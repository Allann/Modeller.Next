using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Modeller.Api.Contracts;
using Xunit;

namespace Modeller.Api.Tests;

/// <summary>
/// A malformed request must never reach an unhandled 500, and must always return the API's own
/// structured WorkspaceAnalyzeResponse envelope — never the framework's default empty-bodied 400
/// — so a client can always parse the response the same way regardless of what was malformed.
/// Some malformations fail at JSON-parse time (explicit null for a top-level required property,
/// invalid JSON syntax) and get the generic "api.request.malformed" diagnostic from
/// WorkspaceEndpoints; others (a null entry inside an otherwise-valid array, a missing-but-
/// optional-shaped property) deserialize successfully and are caught downstream by
/// RequestLimits.Validate with a more specific diagnostic code — both are correct, just at
/// different layers, so these tests assert the shared guarantee (400 + a non-empty structured
/// diagnostic list) rather than pinning every case to one exact code.
/// </summary>
public sealed class MalformedRequestTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public MalformedRequestTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Theory]
    [InlineData("""{"documents":null,"identity":{"kind":"ephemeral"},"configuration":{"generationContractVersion":"1.0","logicalOutputRoot":"generated/"}}""")]
    [InlineData("""{"documents":[{"path":null,"content":"rml 1.0\n"}],"identity":{"kind":"ephemeral"},"configuration":{"generationContractVersion":"1.0","logicalOutputRoot":"generated/"}}""")]
    [InlineData("""{"documents":[{"path":"model/context.rml","content":"rml 1.0\n"}],"identity":null,"configuration":{"generationContractVersion":"1.0","logicalOutputRoot":"generated/"}}""")]
    [InlineData("""{"documents":[{"path":"model/context.rml","content":"rml 1.0\n"}],"identity":{"kind":"ephemeral"},"configuration":null}""")]
    [InlineData("""not json at all""")]
    public async Task A_JSON_level_malformation_returns_the_malformed_request_diagnostic_with_a_400(string json)
    {
        var body = await PostAndReadEnvelopeAsync(json);

        Assert.Contains(body.Diagnostics, d => d.Code == "api.request.malformed");
    }

    [Fact]
    public async Task An_empty_body_returns_the_malformed_request_diagnostic_with_a_400()
    {
        var body = await PostAndReadEnvelopeAsync(string.Empty);

        Assert.Contains(body.Diagnostics, d => d.Code == "api.request.malformed");
    }

    [Fact]
    public async Task Documents_missing_entirely_from_the_JSON_body_still_returns_a_structured_400()
    {
        const string json = """{"identity":{"kind":"ephemeral"},"configuration":{"generationContractVersion":"1.0","logicalOutputRoot":"generated/"}}""";

        var body = await PostAndReadEnvelopeAsync(json);

        Assert.NotEmpty(body.Diagnostics);
    }

    [Fact]
    public async Task A_null_entry_inside_the_documents_array_still_returns_a_structured_400()
    {
        const string json = """{"documents":[null],"identity":{"kind":"ephemeral"},"configuration":{"generationContractVersion":"1.0","logicalOutputRoot":"generated/"}}""";

        var body = await PostAndReadEnvelopeAsync(json);

        Assert.Contains(body.Diagnostics, d => d.Code == "api.request.document.malformed");
    }

    private async Task<WorkspaceAnalyzeResponse> PostAndReadEnvelopeAsync(string json)
    {
        using var client = _factory.CreateClient();
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await client.PostAsync("/v1/workspace/analyze", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<WorkspaceAnalyzeResponse>(ApiJson.Options, TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        return body;
    }
}
