namespace Modeller.Parsing.Acceptance.Features;

/// <summary>Per-scenario shared state behind the "the workspace is compiled" / "compilation
/// succeeds" steps: every acceptance story in this project compiles a workspace, so those two
/// steps are defined once (in <see cref="WorkspaceCompilationSteps"/>) rather than per feature.
/// A story's own Given-step binding class registers <see cref="Compile"/> once its workspace
/// draft is built, and reports the outcome back here so the shared When/Then steps stay
/// story-agnostic — Reqnroll shares one instance of this class across all binding classes
/// constructed for the same scenario.</summary>
public sealed class WorkspaceCompilationContext
{
    public Action? Compile { get; set; }
    public bool IsSuccess { get; set; }
    public string FailureSummary { get; set; } = "";
}
