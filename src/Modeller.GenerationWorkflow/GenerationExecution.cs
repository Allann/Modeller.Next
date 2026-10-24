using System.Collections.Immutable;
using Modeller.Generation;
using Modeller.Output;
using Modeller.Rendering;

namespace Modeller.GenerationWorkflow;

public sealed record GenerationExecutionRequest(GenerationPlanningRequest Planning, OwnershipManifest Manifest,
    OutputMode Mode, StaleOutputPolicy StalePolicy = StaleOutputPolicy.Report);

// Unifies GenerationDiagnostic/RenderingDiagnostic/OutputDiagnostic — the three per-stage
// diagnostic shapes GenerationExecutionResult.Diagnostics can be built from — into one type that
// keeps each diagnostic's actual Message, not just its Code. A caller with no successful Output
// (planning or rendering failed) has only these to report; collapsing to bare code strings, as this
// used to do, silently discarded the one piece of information that says *why* — see
// WorkspaceGeneration.cs's early-failure path, the sole consumer that reports on this before an
// Output exists.
public sealed record WorkflowDiagnostic(string Code, string Message);

public sealed record GenerationExecutionResult(GenerationPlan? Plan, RenderingResult? Rendering, OutputReport? Output,
    ImmutableArray<WorkflowDiagnostic> Diagnostics)
{ public bool IsSuccess => Output?.IsSuccess == true && Diagnostics.IsEmpty; }

public static class GenerationExecution
{
    public static async ValueTask<GenerationExecutionResult> ExecuteAsync(GenerationExecutionRequest request,
        IRendererAdapter renderer, IOutputFileSystem fileSystem, CancellationToken cancellationToken = default)
    {
        var planned = GenerationPlanner.Plan(request.Planning, cancellationToken);
        if (!planned.IsSuccess) return new(null, null, null, ToWorkflowDiagnostics(planned.Diagnostics));
        var rendered = await TemplateRenderer.RenderAsync(new(planned.Plan!), renderer, cancellationToken);
        if (!rendered.IsSuccess) return new(planned.Plan, rendered, null, ToWorkflowDiagnostics(rendered.Diagnostics));
        var output = await OutputApplication.ExecuteAsync(new(request.Planning.Configuration.LogicalOutputRoot,
            request.Mode, request.StalePolicy, rendered.Artifacts, request.Manifest), fileSystem, cancellationToken);
        return new(planned.Plan, rendered, output, ToWorkflowDiagnostics(output.Diagnostics));
    }

    private static ImmutableArray<WorkflowDiagnostic> ToWorkflowDiagnostics(IEnumerable<GenerationDiagnostic> diagnostics) =>
        diagnostics.Select(item => new WorkflowDiagnostic(item.Code, item.Message)).ToImmutableArray();
    private static ImmutableArray<WorkflowDiagnostic> ToWorkflowDiagnostics(IEnumerable<RenderingDiagnostic> diagnostics) =>
        diagnostics.Select(item => new WorkflowDiagnostic(item.Code, item.Message)).ToImmutableArray();
    private static ImmutableArray<WorkflowDiagnostic> ToWorkflowDiagnostics(IEnumerable<OutputDiagnostic> diagnostics) =>
        diagnostics.Select(item => new WorkflowDiagnostic(item.Code, item.Message)).ToImmutableArray();
}
