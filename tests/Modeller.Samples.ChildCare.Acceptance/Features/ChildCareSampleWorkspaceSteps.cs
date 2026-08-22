using Reqnroll;

namespace Modeller.Samples.ChildCare.Acceptance.Features;

/// <summary>Binds the shared Background step every feature in this project opens with. Each
/// story's own Given steps build the small workspace draft that exercises its content-porting
/// change, so this step itself has nothing to do beyond existing as the scenario's stated
/// starting point.</summary>
[Binding]
public sealed class ChildCareSampleWorkspaceSteps
{
    [Given("the child-care sample workspace")]
    public void GivenTheChildCareSampleWorkspace()
    {
    }
}
