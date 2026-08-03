using Modeller.Workspace;
using Xunit;

namespace Modeller.Workspace.Tests;

public sealed class WorkspaceOutcomeTests
{
    [Fact]
    public void Success_carries_the_produced_value()
    {
        var outcome = WorkspaceOutcome.Success(42);

        var value = Assert.IsType<WorkspaceOutcome<int>.Success>(outcome).Value;
        Assert.Equal(42, value);
    }

    [Fact]
    public void Failed_carries_diagnostics()
    {
        var outcome = WorkspaceOutcome.Failed<int>("workspace.test", "boom");

        var diagnostics = Assert.IsType<WorkspaceOutcome<int>.Failed>(outcome).Diagnostics;
        Assert.Single(diagnostics);
        Assert.Equal("workspace.test", diagnostics[0].Code);
    }

    [Fact]
    public void Cancelled_is_a_distinct_case_from_failed()
    {
        WorkspaceOutcome<int> outcome = WorkspaceOutcome.Cancelled<int>();

        Assert.IsType<WorkspaceOutcome<int>.Cancelled>(outcome);
        Assert.IsNotType<WorkspaceOutcome<int>.Failed>(outcome);
    }

    [Fact]
    public void Outcome_is_exhaustively_matchable()
    {
        WorkspaceOutcome<int> outcome = WorkspaceOutcome.Success(1);

        var description = outcome switch
        {
            WorkspaceOutcome<int>.Success success => $"success:{success.Value}",
            WorkspaceOutcome<int>.Failed failed => $"failed:{failed.Diagnostics.Length}",
            WorkspaceOutcome<int>.Cancelled => "cancelled",
            _ => throw new InvalidOperationException("unreachable"),
        };

        Assert.Equal("success:1", description);
    }
}
