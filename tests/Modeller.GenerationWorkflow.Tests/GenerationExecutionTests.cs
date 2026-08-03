using System.Collections.Immutable;
using Modeller.Contexts;
using Modeller.Generation;
using Modeller.Output;
using Modeller.Rendering;
using Xunit;

namespace Modeller.GenerationWorkflow.Tests;

public sealed class GenerationExecutionTests
{
    private const string ContextId = "0191f6d4-4ea0-7000-8000-000000000001";

    private static GenerationPlanningRequest ValidPlanningRequest() =>
        new(
            new ResolvedGenerationSnapshot(
                new FederationSnapshot([new FederationPackageLock(ContextId, "child-care", "1.0.0", "sha256:package", "sha256:context")]),
                [new GenerationSemanticInput("accs-eligibility", ContextId, "sha256:eligibility")]),
            new ValidatedGenerationConfiguration("child-care-csharp", "1.0", "generated", "sha256:configuration"),
            new ValidatedTemplatePackDescriptor(
                "csharp-child-care",
                "1.0.0",
                "1.0",
                "sha256:pack",
                [new TemplateArtifactDescriptor("accs-eligibility", "rule.cs", "domain/accs-eligibility.cs", "child-care", "sha256:rule-template", ["accs-eligibility"])]));

    private static GenerationExecutionRequest ExecutionRequest(GenerationPlanningRequest? planning = null) =>
        new(planning ?? ValidPlanningRequest(), OwnershipManifest.Empty, OutputMode.Apply);

    private sealed class SucceedingRendererAdapter : IRendererAdapter
    {
        public string RendererId => "test-renderer";
        public string ContractVersion => "1.0";
        public ValueTask<AdapterRenderResult> RenderAsync(ArtifactRenderingContext context, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new AdapterRenderResult($"namespace Generated;\npublic static class {context.Artifact.ArtifactId};\n", []));
    }

    private sealed class MemoryFileSystem(ImmutableDictionary<string, string>? initial = null) : IOutputFileSystem
    {
        public ImmutableDictionary<string, string> Files { get; private set; } = initial ?? ImmutableDictionary<string, string>.Empty;

        public ValueTask<FileObservation> InspectAsync(string path, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Files.TryGetValue(path, out var content)
                ? new FileObservation(true, content, false)
                : new FileObservation(false, null, false));

        public ValueTask ApplyAtomicallyAsync(ImmutableArray<FileOperation> operations, string recoveryToken, CancellationToken cancellationToken)
        {
            foreach (var operation in operations)
                Files = operation.Kind == FileOperationKind.Delete ? Files.Remove(operation.Path) : Files.SetItem(operation.Path, operation.Content!);
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task ExecuteAsync_short_circuits_with_only_diagnostics_when_planning_fails()
    {
        var planning = ValidPlanningRequest();
        planning = planning with { TemplatePack = planning.TemplatePack with { GenerationContractVersion = "2.0" } };
        var request = ExecutionRequest(planning);

        var result = await GenerationExecution.ExecuteAsync(request, new SucceedingRendererAdapter(), new MemoryFileSystem(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Plan);
        Assert.Null(result.Rendering);
        Assert.Null(result.Output);
        Assert.Equal("generation.pack.incompatible", Assert.Single(result.Diagnostics));
    }

    [Fact]
    public async Task ExecuteAsync_carries_the_plan_and_stops_when_rendering_fails()
    {
        var request = ExecutionRequest();
        var incompatibleAdapter = new IncompatibleRendererAdapter();

        var result = await GenerationExecution.ExecuteAsync(request, incompatibleAdapter, new MemoryFileSystem(), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Plan);
        Assert.NotNull(result.Rendering);
        Assert.False(result.Rendering!.IsSuccess);
        Assert.Null(result.Output);
        Assert.Equal("rendering.renderer.incompatible", Assert.Single(result.Diagnostics));
    }

    private sealed class IncompatibleRendererAdapter : IRendererAdapter
    {
        public string RendererId => "incompatible-renderer";
        public string ContractVersion => "2.0";
        public ValueTask<AdapterRenderResult> RenderAsync(ArtifactRenderingContext context, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Should not be reached when the contract version is incompatible.");
    }

    [Fact]
    public async Task ExecuteAsync_carries_plan_and_rendering_and_reports_an_output_conflict()
    {
        var request = ExecutionRequest();
        var fileSystem = new MemoryFileSystem(ImmutableDictionary<string, string>.Empty.Add("domain/accs-eligibility.cs", "handwritten content"));

        var result = await GenerationExecution.ExecuteAsync(request, new SucceedingRendererAdapter(), fileSystem, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Plan);
        Assert.NotNull(result.Rendering);
        Assert.True(result.Rendering!.IsSuccess);
        Assert.NotNull(result.Output);
        Assert.Equal(OutputStatus.Conflict, Assert.Single(result.Output!.Changes).Status);
        Assert.True(result.Diagnostics.IsEmpty);
    }

    [Fact]
    public async Task ExecuteAsync_returns_success_with_all_stages_populated_on_the_happy_path()
    {
        var request = ExecutionRequest();

        var result = await GenerationExecution.ExecuteAsync(request, new SucceedingRendererAdapter(), new MemoryFileSystem(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Plan);
        Assert.NotNull(result.Rendering);
        Assert.True(result.Rendering!.IsSuccess);
        Assert.NotNull(result.Output);
        Assert.True(result.Output!.IsSuccess);
        Assert.True(result.Diagnostics.IsEmpty);
        Assert.Equal(OutputStatus.Create, Assert.Single(result.Output.Changes).Status);
    }
}
