using System.Diagnostics;
using Modeller.Api.Contracts;
using Modeller.Model;
using Modeller.Projections;
using Modeller.Workspace;

namespace Modeller.Api;

/// <summary>The HTTP status code to send alongside a <see cref="WorkspaceAnalyzeResponse"/> body.</summary>
public sealed record PipelineResult(WorkspaceAnalyzeResponse Body, int StatusCode);

/// <summary>The HTTP status code to send alongside a <see cref="WorkspaceExportResponse"/> body.</summary>
public sealed record ExportPipelineResult(WorkspaceExportResponse Body, int StatusCode);

/// <summary>
/// The workspace-analyze request pipeline: validate, analyze, project, map to a response. Holding
/// this logic here (rather than in the endpoint handler) keeps <c>WorkspaceEndpoints</c> a thin,
/// declarative route list — per docs/coding-standards/web-apis-and-services/minimal-api-cleanup.md
/// — and makes the pipeline itself independently unit-testable without an HTTP host.
/// </summary>
public sealed class WorkspaceAnalysisPipeline(ILogger<WorkspaceAnalysisPipeline> logger)
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);

    public PipelineResult Handle(WorkspaceAnalyzeRequest request, CancellationToken cancellationToken)
    {
        var violations = RequestLimits.Validate(request);
        if (violations.Count > 0)
        {
            logger.LogInformation("Workspace analyze request rejected: {DiagnosticCodes}", string.Join(',', violations.Select(v => v.Code)));
            return new(new("1.0", violations, [], [], [], [], null), StatusCodes.Status400BadRequest);
        }

        var stopwatch = Stopwatch.StartNew();
        using var timeout = new CancellationTokenSource(RequestTimeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

        var analyzed = ModellerWorkspace.Analyze(request.ToWorkspaceInput(), linked.Token);
        var result = analyzed switch
        {
            WorkspaceOutcome<AnalyzedWorkspace>.Success success => BuildResponse(success.Value, request.Projections ?? [], linked.Token),
            WorkspaceOutcome<AnalyzedWorkspace>.Failed failed => WorkspaceOutcome.Failed<WorkspaceAnalyzeResponse>(failed.Diagnostics),
            _ => WorkspaceOutcome.Cancelled<WorkspaceAnalyzeResponse>(),
        };

        LogOutcome(result, stopwatch.ElapsedMilliseconds);
        return ToPipelineResult(result);
    }

    /// <summary>Turns an ephemeral (or durable) draft into a stable local-workspace snapshot
    /// (issue #73): analyze, then <see cref="ModellerWorkspace.Export"/> to harvest the
    /// post-identity-application document text and the registry it was harvested from. Reuses
    /// <see cref="WorkspaceAnalyzeRequest"/>'s shape and <see cref="RequestLimits.Validate"/> rather
    /// than a dedicated request DTO — <c>Projections</c> is simply ignored — so this endpoint's
    /// request-shape ceilings can never drift from <c>/analyze</c>'s.</summary>
    public ExportPipelineResult HandleExport(WorkspaceAnalyzeRequest request, CancellationToken cancellationToken)
    {
        var violations = RequestLimits.Validate(request);
        if (violations.Count > 0)
        {
            logger.LogInformation("Workspace export request rejected: {DiagnosticCodes}", string.Join(',', violations.Select(v => v.Code)));
            return new(new("1.0", violations, [], null), StatusCodes.Status400BadRequest);
        }

        var stopwatch = Stopwatch.StartNew();
        using var timeout = new CancellationTokenSource(RequestTimeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

        var analyzed = ModellerWorkspace.Analyze(request.ToWorkspaceInput(), linked.Token);
        var result = analyzed switch
        {
            WorkspaceOutcome<AnalyzedWorkspace>.Success success => BuildExportResponse(success.Value),
            WorkspaceOutcome<AnalyzedWorkspace>.Failed failed => WorkspaceOutcome.Failed<WorkspaceExportResponse>(failed.Diagnostics),
            _ => WorkspaceOutcome.Cancelled<WorkspaceExportResponse>(),
        };

        LogExportOutcome(result, stopwatch.ElapsedMilliseconds);
        return ToExportPipelineResult(result);
    }

    private static WorkspaceOutcome<WorkspaceExportResponse> BuildExportResponse(AnalyzedWorkspace analyzed) => ModellerWorkspace.Export(analyzed) switch
    {
        WorkspaceOutcome<WorkspaceIdentityRegistry>.Success success => WorkspaceOutcome.Success(new WorkspaceExportResponse(
            "1.0", [],
            [.. analyzed.IdentifiedDocuments.Select(document => new WorkspaceDocumentDto(document.Path.Value, document.Content))],
            success.Value.ToDto())),
        WorkspaceOutcome<WorkspaceIdentityRegistry>.Failed failed => WorkspaceOutcome.Failed<WorkspaceExportResponse>(failed.Diagnostics),
        _ => WorkspaceOutcome.Cancelled<WorkspaceExportResponse>(),
    };

    private void LogExportOutcome(WorkspaceOutcome<WorkspaceExportResponse> result, long elapsedMilliseconds)
    {
        switch (result)
        {
            case WorkspaceOutcome<WorkspaceExportResponse>.Success success:
                logger.LogInformation("Workspace export request succeeded with {DocumentCount} document(s) in {ElapsedMs}ms.", success.Value.Documents.Count, elapsedMilliseconds);
                break;
            case WorkspaceOutcome<WorkspaceExportResponse>.Failed failed:
                logger.LogInformation("Workspace export request failed with {DiagnosticCount} diagnostic(s) in {ElapsedMs}ms.", failed.Diagnostics.Length, elapsedMilliseconds);
                break;
            default:
                logger.LogWarning("Workspace export request timed out or was cancelled after {ElapsedMs}ms.", elapsedMilliseconds);
                break;
        }
    }

    private static ExportPipelineResult ToExportPipelineResult(WorkspaceOutcome<WorkspaceExportResponse> result) => result switch
    {
        WorkspaceOutcome<WorkspaceExportResponse>.Success success => new(success.Value, StatusCodes.Status200OK),
        WorkspaceOutcome<WorkspaceExportResponse>.Failed failed =>
            new(new("1.0", [.. failed.Diagnostics.Select(WorkspaceContractMappings.ToApiDiagnostic)], [], null), StatusCodes.Status200OK),
        _ => new(
            new("1.0", [new("api.request.timeout", "The request was cancelled or exceeded the server-side time budget.")], [], null),
            StatusCodes.Status503ServiceUnavailable),
    };

    /// <summary>Projects every requested view against an already-analyzed workspace and pairs the
    /// results with the discoverable roots for the two supported view kinds. A per-view failure
    /// (unsupported kind, invalid root, oversized graph) becomes a diagnostic on that view's own
    /// response entry rather than failing the whole request — only cancellation short-circuits
    /// early.
    ///
    /// Two independent bounds apply, addressing two different risks. First,
    /// <see cref="RequestLimits.MaximumDefinitions"/> is checked once, before any projection runs
    /// at all: <c>DiagramProjector.Project</c> observes cancellation between definitions as it
    /// walks but still has no size budget of its own, so bounding how large a revision it is ever
    /// asked to project remains the primary way to keep each individual call itself cheap —
    /// discovering after the fact that a projection produced too much (the second bound, below)
    /// does not help if producing it was itself slow or memory-heavy. Second,
    /// <see cref="RequestLimits.MaximumAggregateGraphElements"/> bounds
    /// the combined response size across every projection actually computed — checked against the
    /// running total plus each candidate's own count, not the running total alone, since checking
    /// only the total-so-far would still let one large graph push the aggregate past the ceiling
    /// (e.g. 4,500 accumulated plus a 1,000-element graph would otherwise return 5,500).</summary>
    private static WorkspaceOutcome<WorkspaceAnalyzeResponse> BuildResponse(
        AnalyzedWorkspace analyzed, IReadOnlyList<ProjectionRequestDto> requests, CancellationToken cancellationToken)
    {
        var workspaceTooLarge = WorkspaceContractMappings.ExceedsDefinitionLimit(
            analyzed.Contexts.Sum(context => context.AuthoredRevision.Definitions.Length));
        var projections = new List<ProjectionResponseDto>(requests.Count);
        var totalGraphElements = 0;
        foreach (var request in requests)
        {
            if (cancellationToken.IsCancellationRequested)
                return WorkspaceOutcome.Cancelled<WorkspaceAnalyzeResponse>();
            if (workspaceTooLarge)
            {
                projections.Add(WorkspaceContractMappings.WorkspaceTooLargeResponse(request.Id));
                continue;
            }
            var projected = ProjectOne(analyzed, request, cancellationToken);
            var candidateTotal = totalGraphElements + projected.GraphElementCount();
            if (WorkspaceContractMappings.ExceedsAggregateGraphElementLimit(candidateTotal))
            {
                projections.Add(WorkspaceContractMappings.AggregateLimitExceededResponse(request.Id));
                continue;
            }
            totalGraphElements = candidateTotal;
            projections.Add(projected);
        }
        var exported = ModellerWorkspace.Export(analyzed);
        if (exported is WorkspaceOutcome<WorkspaceIdentityRegistry>.Failed failed)
            return WorkspaceOutcome.Failed<WorkspaceAnalyzeResponse>(failed.Diagnostics);
        if (exported is WorkspaceOutcome<WorkspaceIdentityRegistry>.Cancelled)
            return WorkspaceOutcome.Cancelled<WorkspaceAnalyzeResponse>();
        var identity = ((WorkspaceOutcome<WorkspaceIdentityRegistry>.Success)exported).Value.ToDto();
        var outline = ComputeOutline(analyzed);
        var summary = outline.GroupBy(item => item.Kind, StringComparer.Ordinal)
            .Select(group => new SemanticCountDto(group.Key, group.Count()))
            .OrderBy(item => item.Kind, StringComparer.Ordinal).ToArray();
        return WorkspaceOutcome.Success(new WorkspaceAnalyzeResponse("1.0", [], ComputeRoots(analyzed), outline, summary, projections, identity));
    }

    private static IReadOnlyList<SemanticOutlineItemDto> ComputeOutline(AnalyzedWorkspace analyzed)
    {
        var provenance = analyzed.Provenance
            .Where(item => item.SemanticPath is null)
            .GroupBy(item => item.SemanticId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Span, StringComparer.Ordinal);
        return [.. SemanticNavigation.Concepts(analyzed.Package.AuthoredRevision)
            .Where(item => provenance.ContainsKey(item.Id.ToString()))
            .Select(item =>
            {
                var span = provenance[item.Id.ToString()];
                return new SemanticOutlineItemDto(item.Id.ToString(), item.Kind.ToString(), item.Name.Value,
                    item.OwnerId == analyzed.Package.AuthoredRevision.Id ? null : item.OwnerId.ToString(),
                    new ApiSourceSpan(span.Document, span.Line, span.Column, span.Length));
            })
            .OrderBy(item => item.Kind, StringComparer.Ordinal)
            .ThenBy(item => item.Name, StringComparer.Ordinal)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .Take(RequestLimits.MaximumOutlineItems)];
    }

    /// <summary>Mirrors the CLI's <c>project --view &lt;kind&gt;</c> (no root) root-listing
    /// behavior, so a client can discover valid projection roots without the response echoing
    /// full source. Bounded by <see cref="RequestLimits.MaximumRoots"/> so a workspace with an
    /// unusually large number of definitions cannot inflate every response.</summary>
    private static IReadOnlyList<RootSummaryDto> ComputeRoots(AnalyzedWorkspace analyzed)
    {
        var revision = analyzed.Package.AuthoredRevision;
        ViewKind[] contextRootedViews = [ViewKind.Structural, ViewKind.CausalityAndEventFlow];
        return [.. contextRootedViews
            .Select(kind => new RootSummaryDto(revision.Id.ToString(), kind, revision.Name.Value, revision.Slug.Value))
            .Concat(analyzed.Contexts.Select(context => new RootSummaryDto(
                context.AuthoredRevision.Id.ToString(), ViewKind.ContextMap, context.AuthoredRevision.Name.Value, context.AuthoredRevision.Slug.Value)))
            .Concat(revision.Definitions.OfType<EntityDefinition>().Where(entity => entity.Lifecycle is not null)
                .Select(entity => new RootSummaryDto(entity.Id.ToString(), ViewKind.Lifecycle, entity.Name.Value, entity.Slug.Value)))
            .Concat(revision.Definitions.OfType<EntityDefinition>()
                .Select(entity => new RootSummaryDto(entity.Id.ToString(), ViewKind.BehaviourMap, entity.Name.Value, entity.Slug.Value)))
            .Concat(revision.Definitions.OfType<RuleDefinition>()
                .Select(rule => new RootSummaryDto(rule.Id.ToString(), ViewKind.RuleDecision, rule.Name.Value, rule.Slug.Value)))
            .OrderBy(root => root.Slug, StringComparer.Ordinal)
            .Take(RequestLimits.MaximumRoots)];
    }

    private static ProjectionResponseDto ProjectOne(AnalyzedWorkspace analyzed, ProjectionRequestDto request, CancellationToken cancellationToken)
    {
        var view = new ViewDefinition(request.Id, 1, request.Kind, [.. request.Roots.Select(SemanticId.Parse)]);
        return ModellerWorkspace.Project(analyzed, view, cancellationToken: cancellationToken) switch
        {
            WorkspaceOutcome<ProjectionResult>.Success success => success.Value.ToProjectionResponse(request.Id, RequestLimits.MaximumGraphElements),
            WorkspaceOutcome<ProjectionResult>.Failed failed => new(request.Id, false, null, [.. failed.Diagnostics.Select(WorkspaceContractMappings.ToApiDiagnostic)]),
            _ => new(request.Id, false, null, [new("api.request.timeout", "The projection request exceeded the server-side time budget.")]),
        };
    }

    private void LogOutcome(WorkspaceOutcome<WorkspaceAnalyzeResponse> result, long elapsedMilliseconds)
    {
        switch (result)
        {
            case WorkspaceOutcome<WorkspaceAnalyzeResponse>.Success success:
                logger.LogInformation("Workspace analyze request succeeded with {ProjectionCount} projection(s) in {ElapsedMs}ms.", success.Value.Projections.Count, elapsedMilliseconds);
                break;
            case WorkspaceOutcome<WorkspaceAnalyzeResponse>.Failed failed:
                logger.LogInformation("Workspace analyze request failed with {DiagnosticCount} diagnostic(s) in {ElapsedMs}ms.", failed.Diagnostics.Length, elapsedMilliseconds);
                break;
            default:
                logger.LogWarning("Workspace analyze request timed out or was cancelled after {ElapsedMs}ms.", elapsedMilliseconds);
                break;
        }
    }

    private static PipelineResult ToPipelineResult(WorkspaceOutcome<WorkspaceAnalyzeResponse> result) => result switch
    {
        WorkspaceOutcome<WorkspaceAnalyzeResponse>.Success success => new(success.Value, StatusCodes.Status200OK),
        WorkspaceOutcome<WorkspaceAnalyzeResponse>.Failed failed =>
            new(new("1.0", [.. failed.Diagnostics.Select(WorkspaceContractMappings.ToApiDiagnostic)], [], [], [], [], null), StatusCodes.Status200OK),
        _ => new(
            new("1.0", [new("api.request.timeout", "The request was cancelled or exceeded the server-side time budget.")], [], [], [], [], null),
            StatusCodes.Status503ServiceUnavailable),
    };
}
