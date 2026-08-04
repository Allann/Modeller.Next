using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Modeller.Api.Contracts;
using Modeller.Projections;
using Xunit;

namespace Modeller.Api.Tests;

public sealed class WorkspaceAnalyzeEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public WorkspaceAnalyzeEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    // The reference (child-care) workspace, split across two documents — same fixture used
    // throughout the Modeller.* test suites (see tests/Fixtures/child-care-accs.modeller). The
    // "# @id=" comments are pre-embedded with well-known UUIDv7s so entity/rule ids stay stable
    // across separate requests even under an Ephemeral identity strategy: EnsureIdentities only
    // mints a fresh id when one isn't already present, so a client that already knows a root id
    // (from a prior discovery call against the exact same source) can rely on it here — a
    // genuinely fresh-every-time ephemeral draft cannot do this two-call discovery dance, since
    // each Analyze call would otherwise mint different random ids.
    private const string EntityId = "0191f6d4-4ea0-7000-8000-000000000002";
    private const string RuleId = "0191f6d4-4ea0-7000-8000-000000000008";

    private const string ContextAndEntityDocument = """
        rml 1.0
        # @id=0191f6d4-4ea0-7000-8000-000000000001
        context Child Care
          version 1.0.0
        end
        # @id=0191f6d4-4ea0-7000-8000-000000000002
        entity ACCS determination application
          # @id=0191f6d4-4ea0-7000-8000-000000000003
          lifecycle ACCS determination application lifecycle
            # @id=0191f6d4-4ea0-7000-8000-000000000004
            stage Draft
            # @id=0191f6d4-4ea0-7000-8000-000000000005
            stage Submitted
          end
        end
        """;

    private const string FactsAndRuleDocument = """
        # @id=0191f6d4-4ea0-7000-8000-000000000006
        fact Active enrolment exists
          type truth
          export
        end
        # @id=0191f6d4-4ea0-7000-8000-000000000007
        fact Supporting evidence is held
          type truth
          export
        end
        # @id=0191f6d4-4ea0-7000-8000-000000000008
        rule Determine ACCS eligibility
          input "Active enrolment exists"
          input "Supporting evidence is held"
          when all
            fact "Active enrolment exists"
            fact "Supporting evidence is held"
          end
          # @id=0191f6d4-4ea0-7000-8000-000000000009
          conclusion Eligible
          end
          finding "Active enrolment exists" true accs.active-enrolment-confirmed
          finding "Supporting evidence is held" true accs.supporting-evidence-confirmed
          export
        end
        """;

    private static WorkspaceAnalyzeRequest ReferenceWorkspaceRequest(List<ProjectionRequestDto>? projections = null) => new(
        [
            new("model/context.rml", ContextAndEntityDocument),
            new("model/rules.rml", FactsAndRuleDocument),
        ],
        new EphemeralIdentityDto(),
        new ConfigurationDto("1.0", "generated/"),
        projections);

    [Fact]
    public async Task Analyze_a_valid_multi_document_workspace_returns_no_diagnostics()
    {
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/v1/workspace/analyze", ReferenceWorkspaceRequest(), ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<WorkspaceAnalyzeResponse>(ApiJson.Options, TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Empty(body.Diagnostics);
    }

    [Fact]
    public async Task Analyze_returns_both_supported_projection_kinds_for_the_reference_workspace_in_one_request()
    {
        using var client = _factory.CreateClient();
        var request = ReferenceWorkspaceRequest([
            new("lifecycle:root", ViewKind.Lifecycle, [EntityId]),
            new("rule-decision:root", ViewKind.RuleDecision, [RuleId]),
        ]);

        using var response = await client.PostAsJsonAsync("/v1/workspace/analyze", request, ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<WorkspaceAnalyzeResponse>(ApiJson.Options, TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Empty(body.Diagnostics);
        Assert.NotEmpty(body.Roots);
        Assert.Equal(2, body.Projections.Count);
        foreach (var projection in body.Projections)
        {
            Assert.True(projection.Succeeded, string.Join(",", projection.Diagnostics.Select(d => d.Code)));
            Assert.NotEmpty(projection.Graph!.Nodes);
        }
    }

    [Fact]
    public async Task Analyze_rejects_a_request_over_the_document_count_limit_with_400()
    {
        using var client = _factory.CreateClient();
        var documents = Enumerable.Range(0, Modeller.Api.RequestLimits.MaximumDocuments + 1)
            .Select(index => new WorkspaceDocumentDto($"model/doc{index}.rml", "rml 1.0\n"))
            .ToList();
        var request = new WorkspaceAnalyzeRequest(documents, new EphemeralIdentityDto(), new ConfigurationDto("1.0", "generated/"), null);

        using var response = await client.PostAsJsonAsync("/v1/workspace/analyze", request, ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<WorkspaceAnalyzeResponse>(ApiJson.Options, TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Contains(body.Diagnostics, d => d.Code == "api.request.documents.too-many");
    }

    [Fact]
    public async Task Analyze_rejects_a_path_escaping_document_with_400()
    {
        using var client = _factory.CreateClient();
        var request = new WorkspaceAnalyzeRequest(
            [new("../../etc/passwd", "rml 1.0\n")], new EphemeralIdentityDto(), new ConfigurationDto("1.0", "generated/"), null);

        using var response = await client.PostAsJsonAsync("/v1/workspace/analyze", request, ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<WorkspaceAnalyzeResponse>(ApiJson.Options, TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Contains(body.Diagnostics, d => d.Code == "api.request.document.path-invalid");
    }

    [Fact]
    public async Task Analyze_returns_200_with_diagnostics_for_a_structurally_valid_but_malformed_workspace()
    {
        using var client = _factory.CreateClient();
        var request = new WorkspaceAnalyzeRequest(
            [new("model/context.rml", "rml 1.0\ncontext Child Care\n  version 1.0.0\n")], // missing 'end'
            new EphemeralIdentityDto(), new ConfigurationDto("1.0", "generated/"), null);

        using var response = await client.PostAsJsonAsync("/v1/workspace/analyze", request, ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<WorkspaceAnalyzeResponse>(ApiJson.Options, TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.NotEmpty(body.Diagnostics);
    }

    [Fact]
    public async Task Analyze_rejects_an_unsupported_view_kind_as_a_per_projection_diagnostic()
    {
        using var client = _factory.CreateClient();
        var request = ReferenceWorkspaceRequest([new("v", ViewKind.Structural, [])]);

        using var response = await client.PostAsJsonAsync("/v1/workspace/analyze", request, ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<WorkspaceAnalyzeResponse>(ApiJson.Options, TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        var projection = Assert.Single(body.Projections);
        Assert.False(projection.Succeeded);
        Assert.Contains(projection.Diagnostics, d => d.Code == "project.view.unsupported");
    }

    [Fact]
    public async Task SupportedViews_lists_exactly_the_view_kinds_DiagramProjector_implements()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/v1/workspace/supported-views", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SupportedViewsResponse>(ApiJson.Options, TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Equal([ViewKind.Lifecycle, ViewKind.RuleDecision], body.Views);
    }

    [Theory]
    [InlineData("/healthz/live")]
    [InlineData("/healthz/ready")]
    public async Task Health_checks_are_reachable(string path)
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
