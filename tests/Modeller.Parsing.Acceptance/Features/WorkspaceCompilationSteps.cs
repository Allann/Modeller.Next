using Reqnroll;
using Xunit;

namespace Modeller.Parsing.Acceptance.Features;

/// <summary>The "the workspace is compiled" / "compilation succeeds" steps shared across every
/// story's feature file in this project. See <see cref="WorkspaceCompilationContext"/>.</summary>
[Binding]
public sealed class WorkspaceCompilationSteps(WorkspaceCompilationContext context)
{
    [When("the workspace is compiled")]
    public void WhenTheWorkspaceIsCompiled() => context.Compile!.Invoke();

    [Then("compilation succeeds")]
    public void ThenCompilationSucceeds() => Assert.True(context.IsSuccess, context.FailureSummary);
}
