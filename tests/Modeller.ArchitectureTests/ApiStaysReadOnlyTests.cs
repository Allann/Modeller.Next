using NetArchTest.Rules;
using Xunit;

namespace Modeller.ArchitectureTests;

/// <summary>
/// Formalizes, as a compiled assertion the fast <c>Test-DependencyRules.ps1</c> pre-check cannot
/// express, the invariant behind letting <c>Modeller.Api</c> reference <c>Modeller.Generation</c>,
/// <c>Modeller.Templates</c>, and <c>Modeller.Rendering</c> directly (issue #134,
/// docs/architecture/decisions/generation-preview-panel.mdx): the hosted API's generation preview
/// is "plan + render, no write". <c>Modeller.Output</c> is the module that performs the filesystem
/// write step, and <c>Modeller.GenerationWorkflow</c>'s only entry point
/// (<c>GenerationExecution.ExecuteAsync</c>) always calls it - so <c>Modeller.Api</c> must never
/// gain either dependency, even transitively, or the "no write" guarantee silently regresses.
/// </summary>
public sealed class ApiStaysReadOnlyTests
{
    private static readonly System.Reflection.Assembly ApiAssembly =
        typeof(Modeller.Api.EmbeddedTemplatePackCatalog).Assembly;

    [Fact]
    public void Api_never_references_Output()
    {
        var result = Types.InAssembly(ApiAssembly)
            .Should()
            .NotHaveDependencyOn("Modeller.Output")
            .GetResult();

        Assert.True(result.IsSuccessful,
            "Modeller.Api must stay read-only (no filesystem writes) per generation-preview-panel.mdx; " +
            $"offending types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Api_never_references_GenerationWorkflow()
    {
        var result = Types.InAssembly(ApiAssembly)
            .Should()
            .NotHaveDependencyOn("Modeller.GenerationWorkflow")
            .GetResult();

        Assert.True(result.IsSuccessful,
            "Modeller.Api must plan+render directly (Modeller.Generation/Modeller.Templates/Modeller.Rendering) rather than " +
            "through Modeller.GenerationWorkflow, whose only entry point always writes output; " +
            $"offending types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }
}
