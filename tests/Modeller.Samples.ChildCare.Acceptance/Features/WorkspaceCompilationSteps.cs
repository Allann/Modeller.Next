using Reqnroll;
using Xunit;

namespace Modeller.Samples.ChildCare.Acceptance.Features;

[Binding]
public sealed class WorkspaceCompilationSteps(WorkspaceCompilationContext context)
{
    [When("the workspace is compiled")]
    public void WhenTheWorkspaceIsCompiled() => context.Compile!();

    [Then("compilation succeeds")]
    public void ThenCompilationSucceeds() => Assert.True(context.IsSuccess, context.FailureSummary);
}
