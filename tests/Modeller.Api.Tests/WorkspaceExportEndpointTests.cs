using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Modeller.Api.Contracts;
using Xunit;

namespace Modeller.Api.Tests;

public sealed class WorkspaceExportEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public WorkspaceExportEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    private const string ContextDocument = """
        rml 1.0
        context Ordering
          version 1.0.0
        end
        """;

    private static WorkspaceAnalyzeRequest EphemeralRequest(string content = ContextDocument) => new(
        [new("model/context.rml", content)], new EphemeralIdentityDto(), new ConfigurationDto("1.0", "generated/"), null);

    [Fact]
    public async Task Export_an_ephemeral_draft_returns_identified_documents_and_a_durable_registry()
    {
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/v1/workspace/export", EphemeralRequest(), ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<WorkspaceExportResponse>(ApiJson.Options, TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Empty(body.Diagnostics);
        Assert.NotNull(body.Identity);
        var document = Assert.Single(body.Documents);
        Assert.Equal("model/context.rml", document.Path);
        // EnsureIdentities embeds an "# @id=" comment for the minted context identity.
        Assert.Contains("# @id=", document.Content);
        var durable = Assert.IsType<DurableIdentityDto>(body.Identity);
        var identities = Assert.Single(durable.Documents);
        Assert.Equal("model/context.rml", identities.Key);
        Assert.Single(identities.Value);
    }

    [Fact]
    public async Task Export_response_JSON_carries_the_kind_discriminator_on_identity()
    {
        // Guards against declaring WorkspaceExportResponse.Identity as the concrete
        // DurableIdentityDto instead of the polymorphic IdentityDto base — System.Text.Json only
        // emits "kind" when the declared property type is the [JsonPolymorphic]-attributed base,
        // and a caller (the playground) needs that discriminator to feed this straight back into a
        // later request's own Identity property.
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/v1/workspace/export", EphemeralRequest(), ApiJson.Options, TestContext.Current.CancellationToken);

        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("\"kind\":\"durable\"", json);
    }

    [Fact]
    public async Task Export_reproduces_the_same_registry_when_re_exporting_an_already_durable_workspace()
    {
        using var client = _factory.CreateClient();
        using var first = await client.PostAsJsonAsync("/v1/workspace/export", EphemeralRequest(), ApiJson.Options, TestContext.Current.CancellationToken);
        var firstBody = await first.Content.ReadFromJsonAsync<WorkspaceExportResponse>(ApiJson.Options, TestContext.Current.CancellationToken);
        Assert.NotNull(firstBody?.Identity);
        var firstIdentity = Assert.IsType<DurableIdentityDto>(firstBody.Identity);

        var durableRequest = new WorkspaceAnalyzeRequest(
            [.. firstBody.Documents], new DurableIdentityDto(firstIdentity.Version, firstIdentity.Documents),
            new ConfigurationDto("1.0", "generated/"), null);

        using var second = await client.PostAsJsonAsync("/v1/workspace/export", durableRequest, ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var secondBody = await second.Content.ReadFromJsonAsync<WorkspaceExportResponse>(ApiJson.Options, TestContext.Current.CancellationToken);
        Assert.NotNull(secondBody?.Identity);
        var secondIdentity = Assert.IsType<DurableIdentityDto>(secondBody.Identity);
        Assert.Equal(firstIdentity.Documents.Single().Value, secondIdentity.Documents.Single().Value);
        Assert.Equal(firstBody.Documents.Single().Content, secondBody.Documents.Single().Content);
    }

    [Fact]
    public async Task Export_returns_diagnostics_without_an_identity_for_a_malformed_workspace()
    {
        using var client = _factory.CreateClient();
        var request = EphemeralRequest("rml 1.0\ncontext Ordering\n  version 1.0.0\n"); // missing 'end'

        using var response = await client.PostAsJsonAsync("/v1/workspace/export", request, ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<WorkspaceExportResponse>(ApiJson.Options, TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.NotEmpty(body.Diagnostics);
        Assert.Null(body.Identity);
        Assert.Empty(body.Documents);
    }

    [Fact]
    public async Task Export_rejects_a_request_over_the_document_count_limit_with_400()
    {
        using var client = _factory.CreateClient();
        var documents = Enumerable.Range(0, Modeller.Api.RequestLimits.MaximumDocuments + 1)
            .Select(index => new WorkspaceDocumentDto($"model/doc{index}.rml", "rml 1.0\n"))
            .ToList();
        var request = new WorkspaceAnalyzeRequest(documents, new EphemeralIdentityDto(), new ConfigurationDto("1.0", "generated/"), null);

        using var response = await client.PostAsJsonAsync("/v1/workspace/export", request, ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<WorkspaceExportResponse>(ApiJson.Options, TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Contains(body.Diagnostics, d => d.Code == "api.request.documents.too-many");
    }

    [Fact]
    public async Task Export_rejects_a_malformed_body_with_400()
    {
        using var client = _factory.CreateClient();
        using var content = new StringContent("{not valid json", System.Text.Encoding.UTF8, "application/json");

        using var response = await client.PostAsync("/v1/workspace/export", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<WorkspaceExportResponse>(ApiJson.Options, TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Contains(body.Diagnostics, d => d.Code == "api.request.malformed");
    }
}
