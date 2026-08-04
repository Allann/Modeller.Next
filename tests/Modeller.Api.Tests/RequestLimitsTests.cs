using Modeller.Api;
using Modeller.Api.Contracts;
using Modeller.Projections;
using Xunit;

namespace Modeller.Api.Tests;

public sealed class RequestLimitsTests
{
    private static WorkspaceAnalyzeRequest ValidRequest() => new(
        [new("model/context.rml", "rml 1.0\ncontext Child Care\n  version 1.0.0\nend\n")],
        new EphemeralIdentityDto(),
        new ConfigurationDto("1.0", "generated/"),
        null);

    [Fact]
    public void Validate_accepts_a_well_formed_request()
    {
        Assert.Empty(RequestLimits.Validate(ValidRequest()));
    }

    [Fact]
    public void Validate_rejects_a_request_with_no_documents()
    {
        var request = ValidRequest() with { Documents = [] };

        var diagnostics = RequestLimits.Validate(request);

        Assert.Contains(diagnostics, d => d.Code == "api.request.documents.required");
    }

    [Fact]
    public void Validate_rejects_more_documents_than_the_configured_maximum()
    {
        var documents = Enumerable.Range(0, RequestLimits.MaximumDocuments + 1)
            .Select(index => new WorkspaceDocumentDto($"model/doc{index}.rml", "rml 1.0\n"))
            .ToList();
        var request = ValidRequest() with { Documents = documents };

        var diagnostics = RequestLimits.Validate(request);

        Assert.Contains(diagnostics, d => d.Code == "api.request.documents.too-many");
    }

    [Fact]
    public void Validate_rejects_a_document_over_the_per_document_character_limit()
    {
        var request = ValidRequest() with
        {
            Documents = [new("model/big.rml", new string('a', RequestLimits.MaximumDocumentCharacters + 1))],
        };

        var diagnostics = RequestLimits.Validate(request);

        Assert.Contains(diagnostics, d => d.Code == "api.request.document.too-large");
    }

    [Theory]
    [InlineData("../escape.rml")]
    [InlineData("/absolute.rml")]
    public void Validate_rejects_a_document_path_that_escapes_the_workspace(string path)
    {
        var request = ValidRequest() with { Documents = [new(path, "rml 1.0\n")] };

        var diagnostics = RequestLimits.Validate(request);

        Assert.Contains(diagnostics, d => d.Code == "api.request.document.path-invalid");
    }

    [Fact]
    public void Validate_rejects_a_document_path_deeper_than_the_configured_maximum()
    {
        var deepPath = string.Join('/', Enumerable.Range(0, RequestLimits.MaximumPathSegments + 1).Select(i => $"segment{i}")) + ".rml";
        var request = ValidRequest() with { Documents = [new(deepPath, "rml 1.0\n")] };

        var diagnostics = RequestLimits.Validate(request);

        Assert.Contains(diagnostics, d => d.Code == "api.request.document.path-too-deep");
    }

    [Fact]
    public void Validate_rejects_an_unconfined_path_in_a_durable_identity_registry()
    {
        var request = ValidRequest() with
        {
            Identity = new DurableIdentityDto("1.0", new() { ["../escape.rml"] = ["0191f6d4-4ea0-7000-8000-000000000001"] }),
        };

        var diagnostics = RequestLimits.Validate(request);

        Assert.Contains(diagnostics, d => d.Code == "api.request.identity-registry.path-invalid");
    }

    [Fact]
    public void Validate_rejects_more_projection_requests_than_the_configured_maximum()
    {
        var projections = Enumerable.Range(0, RequestLimits.MaximumProjectionRequests + 1)
            .Select(index => new ProjectionRequestDto($"view{index}", ViewKind.Lifecycle, []))
            .ToList();
        var request = ValidRequest() with { Projections = projections };

        var diagnostics = RequestLimits.Validate(request);

        Assert.Contains(diagnostics, d => d.Code == "api.request.projections.too-many");
    }

    [Fact]
    public void Validate_rejects_more_roots_in_one_projection_than_the_configured_maximum()
    {
        var roots = Enumerable.Range(0, RequestLimits.MaximumRootsPerProjection + 1)
            .Select(_ => "0191f6d4-4ea0-7000-8000-000000000001")
            .ToList();
        var request = ValidRequest() with { Projections = [new("view", ViewKind.Lifecycle, roots)] };

        var diagnostics = RequestLimits.Validate(request);

        Assert.Contains(diagnostics, d => d.Code == "api.request.projection.too-many-roots");
    }

    [Fact]
    public void Validate_rejects_an_invalid_projection_root_identifier()
    {
        var request = ValidRequest() with
        {
            Projections = [new("view", ViewKind.Lifecycle, ["not-a-guid"])],
        };

        var diagnostics = RequestLimits.Validate(request);

        Assert.Contains(diagnostics, d => d.Code == "api.request.projection.root-invalid");
    }

    [Fact]
    public void Validate_accepts_a_valid_projection_root_identifier()
    {
        var request = ValidRequest() with
        {
            Projections = [new("view", ViewKind.Lifecycle, ["0191f6d4-4ea0-7000-8000-000000000001"])],
        };

        Assert.Empty(RequestLimits.Validate(request));
    }

    // Nullable annotations on the request DTOs are not runtime-enforced for a deserialized JSON
    // payload — a client can still send an explicit null for a "required" field. These tests
    // exercise that directly (bypassing JSON) to prove Validate never lets a null reference reach
    // the pipeline as a NullReferenceException.

    [Fact]
    public void Validate_rejects_a_null_documents_list()
    {
        var request = ValidRequest() with { Documents = null! };

        var diagnostics = RequestLimits.Validate(request);

        Assert.Contains(diagnostics, d => d.Code == "api.request.documents.required");
    }

    [Fact]
    public void Validate_rejects_a_null_entry_in_the_documents_list()
    {
        var request = ValidRequest() with { Documents = [null!] };

        var diagnostics = RequestLimits.Validate(request);

        Assert.Contains(diagnostics, d => d.Code == "api.request.document.malformed");
    }

    [Fact]
    public void Validate_rejects_a_document_with_a_null_path_or_content()
    {
        var request = ValidRequest() with { Documents = [new(null!, "rml 1.0\n")] };

        var diagnostics = RequestLimits.Validate(request);

        Assert.Contains(diagnostics, d => d.Code == "api.request.document.malformed");
    }

    [Fact]
    public void Validate_rejects_a_null_identity()
    {
        var request = ValidRequest() with { Identity = null! };

        var diagnostics = RequestLimits.Validate(request);

        Assert.Contains(diagnostics, d => d.Code == "api.request.identity.required");
    }

    [Fact]
    public void Validate_rejects_a_durable_identity_with_a_null_documents_dictionary()
    {
        var request = ValidRequest() with { Identity = new DurableIdentityDto("1.0", null!) };

        var diagnostics = RequestLimits.Validate(request);

        Assert.Contains(diagnostics, d => d.Code == "api.request.identity-registry.malformed");
    }

    [Fact]
    public void Validate_rejects_a_null_configuration()
    {
        var request = ValidRequest() with { Configuration = null! };

        var diagnostics = RequestLimits.Validate(request);

        Assert.Contains(diagnostics, d => d.Code == "api.request.configuration.required");
    }

    [Fact]
    public void Validate_rejects_a_configuration_with_a_null_generation_contract_version()
    {
        var request = ValidRequest() with { Configuration = new ConfigurationDto(null!, "generated/") };

        var diagnostics = RequestLimits.Validate(request);

        Assert.Contains(diagnostics, d => d.Code == "api.request.configuration.malformed");
    }

    [Fact]
    public void Validate_rejects_a_null_entry_in_the_projections_list()
    {
        var request = ValidRequest() with { Projections = [null!] };

        var diagnostics = RequestLimits.Validate(request);

        Assert.Contains(diagnostics, d => d.Code == "api.request.projection.malformed");
    }

    [Fact]
    public void Validate_rejects_a_projection_with_a_null_roots_list()
    {
        var request = ValidRequest() with { Projections = [new("view", ViewKind.Lifecycle, null!)] };

        var diagnostics = RequestLimits.Validate(request);

        Assert.Contains(diagnostics, d => d.Code == "api.request.projection.malformed");
    }
}
