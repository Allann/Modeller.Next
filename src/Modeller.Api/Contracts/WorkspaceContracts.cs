using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Modeller.Projections;
using Modeller.Workspace;

namespace Modeller.Api.Contracts;

public sealed record WorkspaceDocumentDto(string Path, string Content);

/// <summary>Which identity strategy a request declares — mirrors <c>Modeller.Workspace.IdentityStrategy</c>.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(EphemeralIdentityDto), "ephemeral")]
[JsonDerivedType(typeof(DurableIdentityDto), "durable")]
public abstract record IdentityDto;

public sealed record EphemeralIdentityDto : IdentityDto;

public sealed record DurableIdentityDto(string Version, Dictionary<string, List<string>> Documents) : IdentityDto;

public sealed record ConfigurationDto(string GenerationContractVersion, string LogicalOutputRoot, string? Profile = null);

public sealed record ProjectionRequestDto(string Id, ViewKind Kind, List<string> Roots);

public sealed record WorkspaceAnalyzeRequest(
    List<WorkspaceDocumentDto> Documents,
    IdentityDto Identity,
    ConfigurationDto Configuration,
    List<ProjectionRequestDto>? Projections);

public sealed record ApiSourceSpan(string Document, int Line, int Column, int Length);

public sealed record ApiDiagnostic(string Code, string Message, ApiSourceSpan? Location = null);

public sealed record ApiProjectionNode(string Id, string Role, string Label, IReadOnlyList<string> SemanticIds);

public sealed record ApiProjectionEdge(string Id, string Role, string Label, string SourceId, string TargetId, IReadOnlyList<string> SemanticIds);

public sealed record ApiProjectionGraph(long SourceRevision, ViewKind Kind, IReadOnlyList<ApiProjectionNode> Nodes, IReadOnlyList<ApiProjectionEdge> Edges);

public sealed record ProjectionResponseDto(string Id, bool Succeeded, ApiProjectionGraph? Graph, IReadOnlyList<ApiDiagnostic> Diagnostics);

/// <summary>A root a supported view kind can be projected from — mirrors the CLI's
/// <c>project --view &lt;kind&gt;</c> (no root) root-listing behavior, so a client can discover
/// valid <see cref="ProjectionRequestDto.Roots"/> values without the response echoing full source.</summary>
public sealed record RootSummaryDto(string Id, ViewKind Kind, string Name, string Slug);

public sealed record SemanticOutlineItemDto(string Id, string Kind, string Name, string? OwnerId, ApiSourceSpan Location);

public sealed record SemanticCountDto(string Kind, int Count);

public sealed record WorkspaceAnalyzeResponse(
    string ApiVersion,
    IReadOnlyList<ApiDiagnostic> Diagnostics,
    IReadOnlyList<RootSummaryDto> Roots,
    IReadOnlyList<SemanticOutlineItemDto> Outline,
    IReadOnlyList<SemanticCountDto> Summary,
    IReadOnlyList<ProjectionResponseDto> Projections,
    IdentityDto? Identity);

public sealed record SupportedViewsResponse(string ApiVersion, IReadOnlyList<ViewKind> Views);

public sealed record WorkspaceCompletionRequest(WorkspaceAnalyzeRequest Workspace, string Path, int Line, string Prefix);
public sealed record CompletionItemDto(string Label, string Kind, string Detail);
public sealed record WorkspaceCompletionResponse(string ApiVersion, IReadOnlyList<CompletionItemDto> Items);

/// <summary>Response for <c>POST /v1/workspace/export</c> (issue #73): the post-identity-application
/// document text and the durable registry harvested from it, so a caller (the playground) can turn
/// an ephemeral draft into a stable local workspace download. <see cref="Identity"/> is <c>null</c>
/// only when <see cref="Diagnostics"/> is non-empty (analysis/harvest failed). Declared as the
/// polymorphic <see cref="IdentityDto"/> base — not the concrete <see cref="DurableIdentityDto"/> —
/// specifically so serialization still emits the <c>"kind":"durable"</c> discriminator: System.Text.Json
/// only applies <c>[JsonPolymorphic]</c>/<c>[JsonDerivedType]</c> when the declared property type is
/// the attributed base type, not when it's the concrete derived type. Without this, a caller could
/// not feed this field straight back into a later request's own <c>Identity</c> property, which
/// does expect the discriminator.</summary>
public sealed record WorkspaceExportResponse(
    string ApiVersion,
    IReadOnlyList<ApiDiagnostic> Diagnostics,
    IReadOnlyList<WorkspaceDocumentDto> Documents,
    IdentityDto? Identity);

/// <summary>Mapping between the wire contracts above and <c>Modeller.Workspace</c>'s domain types —
/// colocated with the DTOs themselves rather than scattered across the endpoint/pipeline. Callers
/// (the pipeline) invoke these only after <c>RequestLimits.Validate</c> has confirmed the request
/// is well-formed; they assume that precondition rather than re-checking it.</summary>
public static class WorkspaceContractMappings
{
    public static WorkspaceInput ToWorkspaceInput(this WorkspaceAnalyzeRequest request)
    {
        var documents = request.Documents
            .Select(document => new WorkspaceDocument(LogicalPath.Create(document.Path), document.Content))
            .ToImmutableArray();
        IdentityStrategy identity = request.Identity is DurableIdentityDto durable
            ? new IdentityStrategy.Durable(durable.ToRegistry())
            : IdentityStrategy.Ephemeral.Instance;
        var configuration = new WorkspaceConfigurationInput(
            request.Configuration.GenerationContractVersion, request.Configuration.LogicalOutputRoot, request.Configuration.Profile);
        return new WorkspaceInput(documents, identity, configuration);
    }

    public static WorkspaceIdentityRegistry ToRegistry(this DurableIdentityDto durable) => new(
        durable.Version,
        durable.Documents.ToImmutableDictionary(entry => LogicalPath.Create(entry.Key), entry => entry.Value.ToImmutableArray()));

    public static DurableIdentityDto ToDto(this WorkspaceIdentityRegistry registry) => new(
        registry.Version,
        registry.Documents.ToDictionary(entry => entry.Key.Value, entry => entry.Value.ToList()));

    public static ApiDiagnostic ToApiDiagnostic(this WorkspaceDiagnostic diagnostic) => new(
        diagnostic.Code, diagnostic.Message,
        diagnostic.Location is { } location ? new ApiSourceSpan(location.Document, location.Line, location.Column, location.Length) : null);

    /// <summary>The node+edge count a projection response contributes toward
    /// <see cref="RequestLimits.MaximumAggregateGraphElements"/> — zero for a projection that
    /// didn't produce a graph (already failed/unsupported/too large on its own).</summary>
    public static int GraphElementCount(this ProjectionResponseDto projection) =>
        projection.Graph is { } graph ? graph.Nodes.Count + graph.Edges.Count : 0;

    /// <summary>Whether accepting a projection response into the aggregate total would push it
    /// past the configured ceiling. Takes the total <em>including</em> the candidate response
    /// being considered — checking only the running total accumulated so far would still let a
    /// single large graph push the aggregate past the limit (e.g. 4,500 already accumulated plus
    /// a 1,000-element graph would return 5,500 uncaught).</summary>
    public static bool ExceedsAggregateGraphElementLimit(int totalGraphElementsIncludingCandidate) =>
        totalGraphElementsIncludingCandidate > RequestLimits.MaximumAggregateGraphElements;

    public static ProjectionResponseDto AggregateLimitExceededResponse(string id) => new(id, false, null,
        [new("api.response.aggregate-limit-exceeded", $"The combined projections exceed the {RequestLimits.MaximumAggregateGraphElements}-element response limit.")]);

    /// <summary>Rejects a projection request without ever calling <c>DiagramProjector.Project</c>
    /// — that projector observes cancellation mid-walk but still has no size budget of its own, so
    /// bounding how large a revision it is ever asked to project, checked once per request before
    /// any projection runs, remains the primary way to keep a single projection call itself
    /// cheap.</summary>
    public static ProjectionResponseDto WorkspaceTooLargeResponse(string id) => new(id, false, null,
        [new("api.response.workspace-too-large", $"The workspace declares more than {RequestLimits.MaximumDefinitions} definitions and cannot be projected.")]);

    public static bool ExceedsDefinitionLimit(int definitionCount) => definitionCount > RequestLimits.MaximumDefinitions;

    public static ProjectionResponseDto ToProjectionResponse(this ProjectionResult result, string id, int maximumGraphElements)
    {
        if (!result.Succeeded)
            return new(id, false, null, [.. result.Diagnostics.Select(diagnostic => new ApiDiagnostic(diagnostic.Code, diagnostic.Message))]);
        var graph = result.Graph!;
        if (graph.Nodes.Length + graph.Edges.Length > maximumGraphElements)
            return new(id, false, null, [new("api.projection.graph-too-large", $"The projected graph exceeds the {maximumGraphElements}-element response limit.")]);
        return new(id, true, new ApiProjectionGraph(
            graph.SourceRevision, graph.Kind,
            [.. graph.Nodes.Select(node => new ApiProjectionNode(node.Id, node.Role, node.Label, [.. node.SemanticIds.Select(s => s.ToString())]))],
            [.. graph.Edges.Select(edge => new ApiProjectionEdge(edge.Id, edge.Role, edge.Label, edge.SourceId, edge.TargetId, [.. edge.SemanticIds.Select(s => s.ToString())]))]),
            []);
    }
}
