using System.Collections.Immutable;
using Modeller.Generation;
using Modeller.Output;
using Modeller.Rendering;

namespace Modeller.GenerationWorkflow;

public sealed record GenerationExecutionRequest(GenerationPlanningRequest Planning, OwnershipManifest Manifest,
    OutputMode Mode, StaleOutputPolicy StalePolicy = StaleOutputPolicy.Report);
public sealed record GenerationExecutionResult(GenerationPlan? Plan, RenderingResult? Rendering, OutputReport? Output,
    ImmutableArray<string> Diagnostics)
{ public bool IsSuccess => Output?.IsSuccess == true && Diagnostics.IsEmpty; }

public static class GenerationExecution
{
    public static async ValueTask<GenerationExecutionResult> ExecuteAsync(GenerationExecutionRequest request,
        IRendererAdapter renderer, IOutputFileSystem fileSystem, CancellationToken cancellationToken = default)
    {
        var planned = GenerationPlanner.Plan(request.Planning, cancellationToken);
        if (!planned.IsSuccess) return new(null, null, null, planned.Diagnostics.Select(x => x.Code).ToImmutableArray());
        var rendered = await TemplateRenderer.RenderAsync(new(planned.Plan!), renderer, cancellationToken);
        if (!rendered.IsSuccess) return new(planned.Plan, rendered, null, rendered.Diagnostics.Select(x => x.Code).ToImmutableArray());
        var output = await OutputApplication.ExecuteAsync(new(request.Planning.Configuration.LogicalOutputRoot,
            request.Mode, request.StalePolicy, rendered.Artifacts, request.Manifest), fileSystem, cancellationToken);
        return new(planned.Plan, rendered, output, output.Diagnostics.Select(x => x.Code).ToImmutableArray());
    }
}
