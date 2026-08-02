using System.Collections.Immutable;
using Modeller.Output;

namespace Modeller.Conformance.Python.Tests;

/// <summary>An in-memory <see cref="IOutputFileSystem"/> so conformance tests never touch disk for generated output.</summary>
public sealed class InMemoryOutputFileSystem : IOutputFileSystem
{
    private readonly Dictionary<string, string> files = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, string> Files => files;

    public ValueTask<FileObservation> InspectAsync(string path, CancellationToken cancellationToken) =>
        ValueTask.FromResult(files.TryGetValue(path, out var content)
            ? new FileObservation(true, content, false)
            : new FileObservation(false, null, false));

    public ValueTask ApplyAtomicallyAsync(ImmutableArray<FileOperation> operations, string recoveryToken, CancellationToken cancellationToken)
    {
        foreach (var operation in operations)
        {
            if (operation.Kind == FileOperationKind.Delete) files.Remove(operation.Path);
            else files[operation.Path] = operation.Content!;
        }
        return ValueTask.CompletedTask;
    }
}
