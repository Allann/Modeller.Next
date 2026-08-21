using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
    private const string ContextId = "0191f6d4-4ea0-7000-8000-000000000001";
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

    private static string WithoutEmbeddedIdentities(string source) => string.Join('\n',
        source.Split('\n').Where(line => !line.TrimStart().StartsWith("# @id=", StringComparison.Ordinal)));

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
    public async Task Analyze_returns_effective_identities_that_keep_discovered_roots_valid()
    {
        using var client = _factory.CreateClient();
        var documents = new List<WorkspaceDocumentDto>
        {
            new("model/context.rml", WithoutEmbeddedIdentities(ContextAndEntityDocument)),
            new("model/rules.rml", WithoutEmbeddedIdentities(FactsAndRuleDocument)),
        };
        var discoveryRequest = new WorkspaceAnalyzeRequest(
            documents, new EphemeralIdentityDto(), new ConfigurationDto("1.0", "generated/"), []);

        using var discoveryResponse = await client.PostAsJsonAsync(
            "/v1/workspace/analyze", discoveryRequest, ApiJson.Options, TestContext.Current.CancellationToken);
        var discovery = await discoveryResponse.Content.ReadFromJsonAsync<WorkspaceAnalyzeResponse>(
            ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.NotNull(discovery);
        var identity = Assert.IsType<DurableIdentityDto>(discovery.Identity);
        var root = Assert.Single(discovery.Roots, candidate => candidate.Kind == ViewKind.Lifecycle);
        var projectionRequest = new WorkspaceAnalyzeRequest(
            documents, identity, new ConfigurationDto("1.0", "generated/"),
            [new("lifecycle:root", ViewKind.Lifecycle, [root.Id])]);

        using var projectionResponse = await client.PostAsJsonAsync(
            "/v1/workspace/analyze", projectionRequest, ApiJson.Options, TestContext.Current.CancellationToken);
        var projected = await projectionResponse.Content.ReadFromJsonAsync<WorkspaceAnalyzeResponse>(
            ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.NotNull(projected);
        Assert.True(Assert.Single(projected.Projections).Succeeded);
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
    public async Task Analyze_reports_an_unknown_Rml_statement_at_its_source_line()
    {
        using var client = _factory.CreateClient();
        var request = new WorkspaceAnalyzeRequest(
            [new("model/context.modeller", "rml 1.0\ncontext Ordering\n  version 1.0.0\n  asdf\nend\n")],
            new EphemeralIdentityDto(), new ConfigurationDto("1.0", "generated/"), null);

        using var response = await client.PostAsJsonAsync(
            "/v1/workspace/analyze", request, ApiJson.Options, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<WorkspaceAnalyzeResponse>(
            ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.NotNull(body);
        var diagnostic = Assert.Single(body.Diagnostics);
        Assert.Equal("rml.statement.unexpected", diagnostic.Code);
        Assert.Equal("model/context.modeller", diagnostic.Location!.Document);
        Assert.Equal(4, diagnostic.Location.Line);
    }

    [Fact]
    public async Task Analyze_rejects_a_projection_with_a_missing_root_as_a_per_projection_diagnostic()
    {
        using var client = _factory.CreateClient();
        var request = ReferenceWorkspaceRequest([new("v", ViewKind.ContextMap, [])]);

        using var response = await client.PostAsJsonAsync("/v1/workspace/analyze", request, ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<WorkspaceAnalyzeResponse>(ApiJson.Options, TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        var projection = Assert.Single(body.Projections);
        Assert.False(projection.Succeeded);
        Assert.Contains(projection.Diagnostics, d => d.Code == "projection.root.invalid");
    }

    [Fact]
    public async Task Analyze_projects_a_context_map_rooted_at_the_context()
    {
        using var client = _factory.CreateClient();
        var request = ReferenceWorkspaceRequest([new("context-map:root", ViewKind.ContextMap, [ContextId])]);

        using var response = await client.PostAsJsonAsync("/v1/workspace/analyze", request, ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<WorkspaceAnalyzeResponse>(ApiJson.Options, TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        var projection = Assert.Single(body.Projections);
        Assert.True(projection.Succeeded);
        Assert.Contains(projection.Graph!.Nodes, node => node.Role == "context");
    }

    [Fact]
    public async Task SupportedViews_lists_exactly_the_view_kinds_DiagramProjector_implements()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/v1/workspace/supported-views", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SupportedViewsResponse>(ApiJson.Options, TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Equal(Enum.GetValues<ViewKind>().ToHashSet(), body.Views.ToHashSet());
    }

    [Fact]
    public async Task Analyze_returns_a_semantic_outline_summary_and_structural_context_root()
    {
        using var client = _factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/v1/workspace/analyze", ReferenceWorkspaceRequest(), ApiJson.Options, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<WorkspaceAnalyzeResponse>(ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.NotNull(body);
        Assert.Contains(body.Outline, item => item.Kind == "Entity" && item.Name == "ACCS determination application");
        Assert.Contains(body.Summary, item => item.Kind == "Entity" && item.Count == 1);
        Assert.Contains(body.Roots, item => item.Kind == ViewKind.Structural && item.Name == "Child Care");
    }

    [Fact]
    public async Task Analyze_discovers_a_root_for_every_supported_view_kind()
    {
        // Regression test for 2026-08-20: ComputeRoots (the analyze endpoint's root-discovery,
        // mirrored from the CLI's `project --view <kind>`) was never updated when BehaviourMap,
        // CausalityAndEventFlow, and ContextMap shipped, so those three view kinds never had a
        // discoverable root even though DiagramProjector and SupportedViewKinds both supported
        // them — the playground's root dropdown stayed permanently empty for them in production.
        using var client = _factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/v1/workspace/analyze", ReferenceWorkspaceRequest(), ApiJson.Options, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<WorkspaceAnalyzeResponse>(ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.NotNull(body);
        foreach (var kind in Enum.GetValues<ViewKind>())
            Assert.Contains(body.Roots, root => root.Kind == kind);
    }

    [Fact]
    public async Task Analyze_returns_a_deterministic_owned_outline_without_echoing_source_content()
    {
        const string source = """
            rml 1.0
            # SECRET-SOURCE-COMMENT
            # @id=0191f6d4-4ea0-7000-8000-000000000101
            context Ordering
              version 1.0.0
            end
            # @id=0191f6d4-4ea0-7000-8000-000000000102
            entity Order
              # @id=0191f6d4-4ea0-7000-8000-000000000103
              lifecycle Order lifecycle
                # @id=0191f6d4-4ea0-7000-8000-000000000104
                stage Draft
                # @id=0191f6d4-4ea0-7000-8000-000000000105
                stage Placed
              end
            end
            # @id=0191f6d4-4ea0-7000-8000-000000000106
            entity Orderline
            end
            """;
        var request = new WorkspaceAnalyzeRequest(
            [new("model/entities/order.modeller", source)], new EphemeralIdentityDto(), new ConfigurationDto("1.0", "generated/"), []);
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/v1/workspace/analyze", request, ApiJson.Options, TestContext.Current.CancellationToken);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var body = JsonSerializer.Deserialize<WorkspaceAnalyzeResponse>(json, ApiJson.Options);

        Assert.NotNull(body);
        Assert.Equal(body.Outline.OrderBy(item => item.Kind, StringComparer.Ordinal).ThenBy(item => item.Name, StringComparer.Ordinal).ThenBy(item => item.Id, StringComparer.Ordinal), body.Outline);
        var order = Assert.Single(body.Outline, item => item.Kind == "Entity" && item.Name == "Order");
        var orderline = Assert.Single(body.Outline, item => item.Kind == "Entity" && item.Name == "Orderline");
        var lifecycle = Assert.Single(body.Outline, item => item.Kind == "Lifecycle");
        Assert.Null(order.OwnerId);
        Assert.Null(orderline.OwnerId);
        Assert.NotEqual(order.Id, orderline.Id);
        Assert.Equal(order.Id, lifecycle.OwnerId);
        Assert.All(body.Outline.Where(item => item.Kind == "LifecycleStage"), stage => Assert.Equal(lifecycle.Id, stage.OwnerId));
        Assert.Equal("model/entities/order.modeller", orderline.Location.Document);
        Assert.Equal(18, orderline.Location.Line);
        Assert.Equal(1, orderline.Location.Column);
        Assert.True(orderline.Location.Length > 0);
        Assert.DoesNotContain("SECRET-SOURCE-COMMENT", json, StringComparison.Ordinal);
        Assert.True(body.Outline.Count <= RequestLimits.MaximumOutlineItems);
    }

    [Fact]
    public async Task Complete_returns_only_context_valid_keywords_and_workspace_symbols()
    {
        using var client = _factory.CreateClient();
        var request = new WorkspaceCompletionRequest(ReferenceWorkspaceRequest(), "model/context.rml", 8, 3);
        using var response = await client.PostAsJsonAsync("/v1/workspace/complete", request, ApiJson.Options, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<WorkspaceCompletionResponse>(ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.NotNull(body);
        Assert.Contains(body.Items, item => item.Label == "lifecycle" && item.Kind == "keyword");
        Assert.DoesNotContain(body.Items, item => item.Label == "entity" && item.Kind == "keyword");
        Assert.DoesNotContain(body.Items, item => item.Kind != "keyword");
    }

    [Fact]
    public async Task Complete_filters_semantic_references_and_works_with_an_incomplete_current_document()
    {
        var request = ReferenceWorkspaceRequest();
        request.Documents.Add(new("model/behaviour.modeller", "rml 1.0\nbehaviour Place order\n  for \"ACCS determination application\"\n  requires \""));
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/v1/workspace/complete",
            new WorkspaceCompletionRequest(request, "model/behaviour.modeller", 4, 13), ApiJson.Options, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<WorkspaceCompletionResponse>(ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.NotNull(body);
        var rule = Assert.Single(body.Items);
        Assert.Equal("Determine ACCS eligibility", rule.Label);
        Assert.Equal("Rule", rule.Kind);
        Assert.Equal("Determine ACCS eligibility", rule.InsertText);
        Assert.Equal(13, rule.ReplacementStartColumn);
        Assert.Empty(body.Diagnostics);
    }

    [Fact]
    public async Task Complete_returns_a_structured_error_for_malformed_json()
    {
        using var client = _factory.CreateClient();
        using var content = new StringContent("{not-json", System.Text.Encoding.UTF8, "application/json");

        using var response = await client.PostAsync("/v1/workspace/complete", content, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<WorkspaceCompletionResponse>(ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(body);
        Assert.Contains(body.Diagnostics, item => item.Code == "api.request.malformed");
        Assert.Empty(body.Items);
    }

    [Fact]
    public async Task Complete_applies_workspace_request_limits_before_scanning_source()
    {
        var request = ReferenceWorkspaceRequest();
        request.Documents[0] = request.Documents[0] with { Content = new string('x', RequestLimits.MaximumDocumentCharacters + 1) };
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/v1/workspace/complete",
            new WorkspaceCompletionRequest(request, request.Documents[0].Path, 1, 1), ApiJson.Options, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<WorkspaceCompletionResponse>(ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(body);
        Assert.NotEmpty(body.Diagnostics);
        Assert.Empty(body.Items);
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
