using System.Collections.Immutable;
using Modeller.Parsing;

namespace Modeller.Workspace;

/// <summary>A workspace-native diagnostic — a stable code, a message, and an optional source location.</summary>
public sealed record WorkspaceDiagnostic(string Code, string Message, SourceSpan? Location = null);

/// <summary>
/// The closed result of a <see cref="ModellerWorkspace"/> operation: exactly one of a produced
/// value, a set of diagnostics explaining failure, or cancellation. Unlike a
/// <c>(ErrorCode? Error, T Value)</c> pair, this shape makes it impossible to construct a result
/// that is both an error and a value, and makes cancellation a distinct case rather than folding it
/// into "failed."
/// </summary>
public abstract record WorkspaceOutcome<T>
{
    private WorkspaceOutcome() { }

    public sealed record Success(T Value) : WorkspaceOutcome<T>;
    public sealed record Failed(ImmutableArray<WorkspaceDiagnostic> Diagnostics) : WorkspaceOutcome<T>;
    public sealed record Cancelled : WorkspaceOutcome<T>;
}

public static class WorkspaceOutcome
{
    public static WorkspaceOutcome<T> Success<T>(T value) => new WorkspaceOutcome<T>.Success(value);
    public static WorkspaceOutcome<T> Failed<T>(ImmutableArray<WorkspaceDiagnostic> diagnostics) => new WorkspaceOutcome<T>.Failed(diagnostics);
    public static WorkspaceOutcome<T> Failed<T>(string code, string message, SourceSpan? location = null) =>
        new WorkspaceOutcome<T>.Failed([new(code, message, location)]);
    public static WorkspaceOutcome<T> Cancelled<T>() => new WorkspaceOutcome<T>.Cancelled();
}
