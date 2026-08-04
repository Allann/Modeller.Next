using System.Collections.Immutable;
using Modeller.Api.Contracts;
using Modeller.Model;
using Modeller.Projections;
using Xunit;

namespace Modeller.Api.Tests;

public sealed class WorkspaceContractMappingsTests
{
    [Fact]
    public void ToProjectionResponse_rejects_a_graph_over_the_configured_element_limit()
    {
        var nodes = Enumerable.Range(0, RequestLimits.MaximumGraphElements + 1)
            .Select(index => new ProjectionNode($"node-{index}", "entity", $"Node {index}", ImmutableArray<SemanticId>.Empty))
            .ToImmutableArray();
        var graph = new ProjectionGraph(1, ViewKind.Lifecycle, nodes, []);
        var result = new ProjectionResult(graph, []);

        var response = result.ToProjectionResponse("view", RequestLimits.MaximumGraphElements);

        Assert.False(response.Succeeded);
        Assert.Null(response.Graph);
        Assert.Contains(response.Diagnostics, d => d.Code == "api.projection.graph-too-large");
    }

    [Fact]
    public void ToProjectionResponse_accepts_a_graph_within_the_configured_element_limit()
    {
        var nodes = ImmutableArray.Create(new ProjectionNode("node-1", "entity", "Node", ImmutableArray<SemanticId>.Empty));
        var graph = new ProjectionGraph(1, ViewKind.Lifecycle, nodes, []);
        var result = new ProjectionResult(graph, []);

        var response = result.ToProjectionResponse("view", RequestLimits.MaximumGraphElements);

        Assert.True(response.Succeeded);
        Assert.NotNull(response.Graph);
    }

    [Fact]
    public void GraphElementCount_returns_the_combined_node_and_edge_count()
    {
        var nodes = ImmutableArray.Create(
            new ProjectionNode("n1", "entity", "N1", ImmutableArray<SemanticId>.Empty),
            new ProjectionNode("n2", "entity", "N2", ImmutableArray<SemanticId>.Empty));
        var edges = ImmutableArray.Create(new ProjectionEdge("e1", "transition", "E1", "n1", "n2", ImmutableArray<SemanticId>.Empty));
        var response = new ProjectionResult(new ProjectionGraph(1, ViewKind.Lifecycle, nodes, edges), []).ToProjectionResponse("view", 100);

        Assert.Equal(3, response.GraphElementCount());
    }

    [Fact]
    public void GraphElementCount_is_zero_for_a_projection_with_no_graph()
    {
        var response = new ProjectionResponseDto("view", false, null, [new ApiDiagnostic("some.code", "message")]);

        Assert.Equal(0, response.GraphElementCount());
    }

    [Fact]
    public void ExceedsAggregateGraphElementLimit_is_false_below_the_configured_ceiling()
    {
        Assert.False(WorkspaceContractMappings.ExceedsAggregateGraphElementLimit(RequestLimits.MaximumAggregateGraphElements - 1));
    }

    [Fact]
    public void ExceedsAggregateGraphElementLimit_is_false_exactly_at_the_configured_ceiling()
    {
        // Reaching the ceiling exactly is allowed; only going past it is rejected — otherwise a
        // workspace whose combined projections land exactly on the configured limit would be
        // punished for no reason.
        Assert.False(WorkspaceContractMappings.ExceedsAggregateGraphElementLimit(RequestLimits.MaximumAggregateGraphElements));
    }

    [Fact]
    public void ExceedsAggregateGraphElementLimit_is_true_just_past_the_configured_ceiling()
    {
        Assert.True(WorkspaceContractMappings.ExceedsAggregateGraphElementLimit(RequestLimits.MaximumAggregateGraphElements + 1));
    }

    [Fact]
    public void AggregateLimitExceededResponse_carries_the_expected_diagnostic_code()
    {
        var response = WorkspaceContractMappings.AggregateLimitExceededResponse("view");

        Assert.False(response.Succeeded);
        Assert.Contains(response.Diagnostics, d => d.Code == "api.response.aggregate-limit-exceeded");
    }

    [Fact]
    public void WorkspaceTooLargeResponse_carries_the_expected_diagnostic_code()
    {
        var response = WorkspaceContractMappings.WorkspaceTooLargeResponse("view");

        Assert.False(response.Succeeded);
        Assert.Contains(response.Diagnostics, d => d.Code == "api.response.workspace-too-large");
    }

    [Fact]
    public void ExceedsDefinitionLimit_is_false_at_or_below_the_configured_ceiling()
    {
        Assert.False(WorkspaceContractMappings.ExceedsDefinitionLimit(RequestLimits.MaximumDefinitions));
        Assert.False(WorkspaceContractMappings.ExceedsDefinitionLimit(RequestLimits.MaximumDefinitions - 1));
    }

    [Fact]
    public void ExceedsDefinitionLimit_is_true_just_past_the_configured_ceiling()
    {
        Assert.True(WorkspaceContractMappings.ExceedsDefinitionLimit(RequestLimits.MaximumDefinitions + 1));
    }
}
