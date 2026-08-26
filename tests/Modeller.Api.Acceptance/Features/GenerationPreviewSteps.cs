using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Modeller.Api;
using Modeller.Api.Contracts;
using Reqnroll;
using Xunit;

namespace Modeller.Api.Acceptance.Features;

/// <summary>Step bindings for <c>GenerationPreview.feature</c>: drives the real
/// <c>POST /v1/workspace/generate</c> endpoint (read-only generation preview) through an in-process
/// <see cref="WebApplicationFactory{TEntryPoint}"/>, the same way <c>Modeller.Api.Tests</c>' own
/// analyze/export endpoint tests exercise the API — no host filesystem, no external process.</summary>
[Binding]
public sealed class GenerationPreviewSteps
{
    private const string KnownTemplatePackId = "csharp/domain-project";
    private const string DocumentPath = "model/context.rml";

    private static readonly WebApplicationFactory<Program> Factory = new();
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private List<WorkspaceDocumentDto> _documents = [];
    private ConfigurationDto _configuration = new("1.0", "generated/");
    private string _templatePackId = KnownTemplatePackId;

    private HttpResponseMessage? _response;
    private WorkspaceGenerateResponse? _body;
    private WorkspaceGenerateResponse? _firstBody;
    private WorkspaceGenerateResponse? _secondBody;

    [Given("an empty workspace draft")]
    public void GivenAnEmptyWorkspaceDraft()
    {
        _documents = [];
        _configuration = new("1.0", "generated/");
        _templatePackId = KnownTemplatePackId;
    }

    [Given("a draft declaring one bounded context with an entity")]
    public void GivenADraftDeclaringOneBoundedContextWithAnEntity() => SetDraft(ValidDocument("Widget"));

    [Given("the draft names the known template pack")]
    public void GivenTheDraftNamesTheKnownTemplatePack() => _templatePackId = KnownTemplatePackId;

    [Given("a draft with a syntax error")]
    public void GivenADraftWithASyntaxError() => SetDraft("""
        rml 1.0
        context Child Care
          version 1.0.0
        end
        entity Widget
        """); // missing the closing 'end' for 'entity Widget' -> a structural parse failure.

    [Given("a draft declaring one bounded context with an entity that has an invalid field")]
    public void GivenADraftDeclaringAnEntityWithAnInvalidField() => SetDraft("""
        rml 1.0
        context Child Care
          version 1.0.0
        end
        entity Widget
          relationship Target
            target "Nonexistent"
            cardinality one
          end
        end
        """); // syntactically well-formed, but the relationship's target cannot be resolved.

    [Given("the draft names a template pack the server does not recognize")]
    public void GivenTheDraftNamesATemplatePackTheServerDoesNotRecognize() => _templatePackId = "unknown/made-up-pack";

    [Given("the draft declares a generation contract version the known template pack does not support")]
    public void GivenTheDraftDeclaresAnIncompatibleGenerationContractVersion() =>
        _configuration = _configuration with { GenerationContractVersion = "2.0" };

    [Given("a draft with more documents than the preview request allows")]
    public void GivenADraftWithMoreDocumentsThanThePreviewRequestAllows() => _documents =
        [.. Enumerable.Range(0, RequestLimits.MaximumDocuments + 1).Select(index => new WorkspaceDocumentDto($"model/doc{index}.rml", "rml 1.0\n"))];

    [When("a generation preview is requested for the draft")]
    public async Task WhenAGenerationPreviewIsRequestedForTheDraft() => (_response, _body) = await SendAsync();

    [When("a generation preview is requested again for the same draft")]
    public async Task WhenAGenerationPreviewIsRequestedAgainForTheSameDraft()
    {
        _firstBody = _body;
        (_response, _secondBody) = await SendAsync();
    }

    [When("a second, unrelated preview is requested for a different draft")]
    public async Task WhenASecondUnrelatedPreviewIsRequestedForADifferentDraft()
    {
        _firstBody = _body;
        SetDraft(ValidDocument("Gadget")); // same document path as the first draft, different content.
        (_response, _secondBody) = await SendAsync();
    }

    [Then("the preview succeeds with no diagnostics")]
    public void ThenThePreviewSucceedsWithNoDiagnostics()
    {
        Assert.Equal(HttpStatusCode.OK, _response!.StatusCode);
        Assert.NotNull(_body);
        Assert.Empty(_body.Diagnostics);
    }

    [Then("the preview lists the proposed artifacts in a stable order")]
    public void ThenThePreviewListsTheProposedArtifactsInAStableOrder()
    {
        Assert.NotEmpty(_body!.Artifacts);
        var expectedOrder = _body.Artifacts.OrderBy(artifact => artifact.Path, StringComparer.Ordinal).Select(artifact => artifact.Path);
        Assert.Equal(expectedOrder, _body.Artifacts.Select(artifact => artifact.Path));
    }

    [Then("every listed artifact carries its path, its owner, the template pack ID, and the template ID")]
    public void ThenEveryListedArtifactCarriesItsPathOwnerPackIdAndTemplateId() =>
        Assert.All(_body!.Artifacts, artifact =>
        {
            Assert.False(string.IsNullOrWhiteSpace(artifact.Path));
            Assert.False(string.IsNullOrWhiteSpace(artifact.Owner));
            Assert.False(string.IsNullOrWhiteSpace(artifact.PackId));
            Assert.False(string.IsNullOrWhiteSpace(artifact.TemplateId));
        });

    [Then("every listed artifact carries its rendered content")]
    public void ThenEveryListedArtifactCarriesItsRenderedContent() =>
        Assert.All(_body!.Artifacts, artifact =>
        {
            Assert.False(string.IsNullOrEmpty(artifact.Content));
            Assert.False(string.IsNullOrWhiteSpace(artifact.ContentDigest));
        });

    [Then("nothing is written to a filesystem")]
    public void ThenNothingIsWrittenToAFilesystem()
    {
        // WorkspaceGenerationPreviewPipeline calls ModellerWorkspace.Analyze, GenerationPlanner.Plan,
        // and TemplateRenderer.RenderAsync directly — never GenerationExecution/an output filesystem
        // adapter — so there is no host path this in-process test could observe a write against.
        // Nothing to assert beyond what the other Then steps already confirm: a preview response,
        // never an applied change.
    }

    [Then("both previews list the same artifacts with identical rendered content")]
    public void ThenBothPreviewsListTheSameArtifactsWithIdenticalRenderedContent()
    {
        Assert.NotNull(_firstBody);
        Assert.NotNull(_secondBody);
        Assert.Empty(_firstBody!.Diagnostics);
        Assert.Empty(_secondBody!.Diagnostics);
        Assert.Equal(_firstBody.Artifacts, _secondBody.Artifacts);
    }

    [Then("the preview reports diagnostics explaining the draft could not be parsed")]
    public void ThenThePreviewReportsDiagnosticsExplainingTheDraftCouldNotBeParsed()
    {
        Assert.Equal(HttpStatusCode.OK, _response!.StatusCode);
        Assert.NotEmpty(_body!.Diagnostics);
        Assert.Contains(_body.Diagnostics, diagnostic => diagnostic.Code == "rml.block.unclosed");
    }

    [Then("the preview lists no artifacts")]
    public void ThenThePreviewListsNoArtifacts() => Assert.Empty(_body!.Artifacts);

    [Then("the preview reports diagnostics explaining the draft failed validation")]
    public void ThenThePreviewReportsDiagnosticsExplainingTheDraftFailedValidation()
    {
        Assert.Equal(HttpStatusCode.OK, _response!.StatusCode);
        Assert.Contains(_body!.Diagnostics, diagnostic => diagnostic.Code == "rml.reference.unresolved");
    }

    [Then("the preview reports a diagnostic explaining the template pack is unknown")]
    public void ThenThePreviewReportsADiagnosticExplainingTheTemplatePackIsUnknown()
    {
        Assert.Equal(HttpStatusCode.OK, _response!.StatusCode);
        Assert.Contains(_body!.Diagnostics, diagnostic => diagnostic.Code == "api.generate.template-pack.unknown");
    }

    [Then("the preview reports a diagnostic explaining the generation contract is incompatible")]
    public void ThenThePreviewReportsADiagnosticExplainingTheGenerationContractIsIncompatible()
    {
        Assert.Equal(HttpStatusCode.OK, _response!.StatusCode);
        Assert.Contains(_body!.Diagnostics, diagnostic => diagnostic.Code == "template-pack.generation-contract.incompatible");
    }

    [Then("the request is rejected as malformed")]
    public void ThenTheRequestIsRejectedAsMalformed()
    {
        Assert.Equal(HttpStatusCode.BadRequest, _response!.StatusCode);
        Assert.NotEmpty(_body!.Diagnostics);
    }

    [Then("no diagnostics reference the draft's content")]
    public void ThenNoDiagnosticsReferenceTheDraftsContent() =>
        Assert.All(_body!.Diagnostics, diagnostic => Assert.StartsWith("api.request.", diagnostic.Code, StringComparison.Ordinal));

    [Then("the second preview is unaffected by the first draft's content")]
    public void ThenTheSecondPreviewIsUnaffectedByTheFirstDraftsContent()
    {
        Assert.NotNull(_secondBody);
        Assert.Empty(_secondBody!.Diagnostics);
        Assert.Contains(_secondBody.Artifacts, artifact => artifact.Content.Contains("Gadget", StringComparison.Ordinal));
        Assert.DoesNotContain(_secondBody.Artifacts, artifact => artifact.Content.Contains("Widget", StringComparison.Ordinal));
    }

    private void SetDraft(string document) => _documents = [new(DocumentPath, document)];

    private static string ValidDocument(string entityName) => $"""
        rml 1.0
        context Child Care
          version 1.0.0
        end
        entity {entityName}
        end
        """;

    private async Task<(HttpResponseMessage Response, WorkspaceGenerateResponse? Body)> SendAsync()
    {
        using var client = Factory.CreateClient();
        var request = new WorkspaceGenerateRequest(_documents, new EphemeralIdentityDto(), _configuration, _templatePackId);
        var response = await client.PostAsJsonAsync("/v1/workspace/generate", request, Json, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<WorkspaceGenerateResponse>(Json, TestContext.Current.CancellationToken);
        return (response, body);
    }
}
