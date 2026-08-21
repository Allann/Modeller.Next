using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Modeller.Api.Contracts;
using Modeller.Projections;
using Xunit;

namespace Modeller.Api.Tests;

/// <summary>
/// Executes tests/Modeller.Parsing.Acceptance/Features/ContextMapCrossContextDependencies.qa.md
/// against the real product surface (the hosted API's /v1/workspace/analyze endpoint) rather than
/// against the library directly, proving the multi-context RML capability is actually reachable —
/// not just implemented and unit-tested in isolation. One test per numbered QA part; part 7
/// (a single-context workspace still renders) is already covered by
/// <see cref="WorkspaceAnalyzeEndpointTests.Analyze_projects_a_context_map_rooted_at_the_context"/>.
/// </summary>
public sealed class ContextMapCrossContextDependenciesQaTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ContextMapCrossContextDependenciesQaTests(WebApplicationFactory<Program> factory) => _factory = factory;

    // Fixed UUIDv7 "# @id=" comments keep context roots stable across requests under the
    // Ephemeral identity strategy — EnsureIdentities only mints an id when one is missing, so a
    // single request can both declare a projection root and analyze in one round trip. See the
    // identical technique in WorkspaceAnalyzeEndpointTests.
    private const string ChildCareContextId = "0191f6d4-4ea0-7000-8000-0000000000a1";

    private const string ChildCareDocument = """
        rml 1.0
        # @id=0191f6d4-4ea0-7000-8000-0000000000a1
        context Child Care
          version 1.0.0
        end
        # @id=0191f6d4-4ea0-7000-8000-0000000000a2
        fact Active enrolment exists
          type truth
          export
        end
        """;

    private const string ChildCareDocumentFactNotExported = """
        rml 1.0
        # @id=0191f6d4-4ea0-7000-8000-0000000000a1
        context Child Care
          version 1.0.0
        end
        # @id=0191f6d4-4ea0-7000-8000-0000000000a2
        fact Active enrolment exists
          type truth
        end
        """;

    private const string CentreOperationsDocument = """
        rml 1.0
        # @id=0191f6d4-4ea0-7000-8000-0000000000b1
        context Centre Operations
          version 1.0.0
          import "Active enrolment exists"
            from "Child Care"
          end
        end
        """;

    private static WorkspaceAnalyzeRequest TwoContextWorkspaceRequest(
        string childCareDocument, List<ProjectionRequestDto>? projections = null) => new(
        [
            new("model/child-care.rml", childCareDocument),
            new("model/centre-operations.rml", CentreOperationsDocument),
        ],
        new EphemeralIdentityDto(),
        new ConfigurationDto("1.0", "generated/"),
        projections);

    /// <summary>QA Part 1: a workspace may declare more than one bounded context, and each one
    /// is independently discoverable as a context-map root.</summary>
    [Fact]
    public async Task Analyze_a_workspace_declaring_two_bounded_contexts_succeeds()
    {
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/v1/workspace/analyze", TwoContextWorkspaceRequest(ChildCareDocument), ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<WorkspaceAnalyzeResponse>(ApiJson.Options, TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Empty(body.Diagnostics);
        Assert.Contains(body.Roots, root => root.Kind == ViewKind.ContextMap && root.Name == "Child Care");
        Assert.Contains(body.Roots, root => root.Kind == ViewKind.ContextMap && root.Name == "Centre Operations");
    }

    /// <summary>QA Part 3: importing a fact that its owning context has not exported is rejected
    /// with a diagnostic explaining why, not a silent or partial success.</summary>
    [Fact]
    public async Task Analyze_rejects_an_import_of_a_fact_that_is_not_exported()
    {
        using var client = _factory.CreateClient();
        var request = TwoContextWorkspaceRequest(ChildCareDocumentFactNotExported);

        using var response = await client.PostAsJsonAsync("/v1/workspace/analyze", request, ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<WorkspaceAnalyzeResponse>(ApiJson.Options, TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Contains(body.Diagnostics, diagnostic => diagnostic.Code == "rml.import.not-exported");
    }

    /// <summary>QA Part 4: importing from a bounded context that was never declared is rejected
    /// with a diagnostic explaining the context cannot be resolved.</summary>
    [Fact]
    public async Task Analyze_rejects_an_import_naming_an_undeclared_bounded_context()
    {
        using var client = _factory.CreateClient();
        var request = new WorkspaceAnalyzeRequest(
            [new("model/centre-operations.rml", CentreOperationsDocument)],
            new EphemeralIdentityDto(), new ConfigurationDto("1.0", "generated/"), null);

        using var response = await client.PostAsJsonAsync("/v1/workspace/analyze", request, ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<WorkspaceAnalyzeResponse>(ApiJson.Options, TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Contains(body.Diagnostics, diagnostic => diagnostic.Code == "rml.import.context-unresolved");
    }

    /// <summary>QA Part 5: two bounded contexts cannot share a name.</summary>
    [Fact]
    public async Task Analyze_rejects_two_bounded_contexts_sharing_a_name()
    {
        const string duplicateContextDocument = """
            rml 1.0
            context Child Care
              version 1.0.0
            end
            """;
        using var client = _factory.CreateClient();
        var request = new WorkspaceAnalyzeRequest(
            [
                new("model/a.rml", duplicateContextDocument),
                new("model/b.rml", duplicateContextDocument),
            ],
            new EphemeralIdentityDto(), new ConfigurationDto("1.0", "generated/"), null);

        using var response = await client.PostAsJsonAsync("/v1/workspace/analyze", request, ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<WorkspaceAnalyzeResponse>(ApiJson.Options, TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Contains(body.Diagnostics, diagnostic => diagnostic.Code == "rml.name.duplicate");
    }

    /// <summary>QA Part 6 — the payoff: the context map shows a real dependency edge for a
    /// declared import, not a single disconnected node.</summary>
    [Fact]
    public async Task Analyze_projects_a_context_map_showing_the_cross_context_dependency_edge()
    {
        using var client = _factory.CreateClient();
        var request = TwoContextWorkspaceRequest(
            ChildCareDocument, [new("context-map:root", ViewKind.ContextMap, [ChildCareContextId])]);

        using var response = await client.PostAsJsonAsync("/v1/workspace/analyze", request, ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<WorkspaceAnalyzeResponse>(ApiJson.Options, TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Empty(body.Diagnostics);
        var projection = Assert.Single(body.Projections);
        Assert.True(projection.Succeeded, string.Join(",", projection.Diagnostics.Select(diagnostic => diagnostic.Code)));
        Assert.Equal(2, projection.Graph!.Nodes.Count);
        Assert.Contains(projection.Graph.Nodes, node => node.Role == "context" && node.Label == "Child Care");
        Assert.Contains(projection.Graph.Nodes, node => node.Role == "context" && node.Label == "Centre Operations");
        var edge = Assert.Single(projection.Graph.Edges);
        Assert.Equal("Active enrolment exists", edge.Label);
        var childCareNode = projection.Graph.Nodes.Single(node => node.Label == "Child Care");
        var centreOperationsNode = projection.Graph.Nodes.Single(node => node.Label == "Centre Operations");
        Assert.Equal(centreOperationsNode.Id, edge.SourceId);
        Assert.Equal(childCareNode.Id, edge.TargetId);
    }
}
