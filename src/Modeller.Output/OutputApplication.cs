using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Modeller.Rendering;

namespace Modeller.Output;

public enum OutputMode { Preview, Apply }
public enum StaleOutputPolicy { Report, Remove }
public enum OutputStatus { Create, Change, Unchanged, Conflict, Stale, Remove }
public enum FileOperationKind { Write, Delete }
public sealed record OwnedArtifact(string ArtifactId, string ContentDigest);
public sealed record OwnershipManifest(string Version, ImmutableDictionary<string, OwnedArtifact> Artifacts)
{ public static OwnershipManifest Empty { get; } = new("1.0", ImmutableDictionary<string, OwnedArtifact>.Empty); }
public sealed record OutputRequest(string TargetRoot, OutputMode Mode, StaleOutputPolicy StalePolicy,
    ImmutableArray<ProposedOutputArtifact> Artifacts, OwnershipManifest Manifest);
public sealed record FileObservation(bool Exists, string? Content, bool IsSymbolicLink);
public sealed record OutputChange(string Path, OutputStatus Status, string? ArtifactId = null);
public sealed record FileOperation(FileOperationKind Kind, string Path, string? Content = null);
public sealed record OutputDiagnostic(string Code, string Message, string? Path = null);
public sealed record OutputReport(ImmutableArray<OutputChange> Changes, ImmutableArray<FileOperation> Operations,
    OwnershipManifest Manifest, ImmutableArray<OutputDiagnostic> Diagnostics, string? RecoveryToken)
{ public bool IsSuccess => Diagnostics.IsEmpty && Changes.All(x => x.Status != OutputStatus.Conflict); }
public interface IOutputFileSystem
{
    ValueTask<FileObservation> InspectAsync(string path, CancellationToken cancellationToken);
    ValueTask ApplyAtomicallyAsync(ImmutableArray<FileOperation> operations, string recoveryToken, CancellationToken cancellationToken);
}

public static class OutputApplication
{
    public static async ValueTask<OutputReport> ExecuteAsync(OutputRequest request, IOutputFileSystem fileSystem,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request); ArgumentNullException.ThrowIfNull(fileSystem);
        if (!Safe(request.TargetRoot) || request.Artifacts.Any(x => !Safe(x.LogicalPath))) return Failure("output.path.invalid", "Output paths must remain relative and confined.");
        if (cancellationToken.IsCancellationRequested) return Failure("output.cancelled", "Output application was cancelled.");
        var changes = ImmutableArray.CreateBuilder<OutputChange>();
        var operations = ImmutableArray.CreateBuilder<FileOperation>();
        var next = ImmutableDictionary.CreateBuilder<string, OwnedArtifact>(StringComparer.OrdinalIgnoreCase);
        foreach (var artifact in request.Artifacts.OrderBy(x => x.LogicalPath, StringComparer.Ordinal))
        {
            if (Digest(artifact.Content) != artifact.ContentDigest)
                return Failure("output.artifact.digest-mismatch", "A proposed artifact does not match its declared content digest.");
            var observation = await fileSystem.InspectAsync(artifact.LogicalPath, cancellationToken);
            if (observation.IsSymbolicLink) { changes.Add(new(artifact.LogicalPath, OutputStatus.Conflict, artifact.ArtifactId)); continue; }
            if (!observation.Exists)
            {
                changes.Add(new(artifact.LogicalPath, OutputStatus.Create, artifact.ArtifactId));
                operations.Add(new(FileOperationKind.Write, artifact.LogicalPath, artifact.Content));
            }
            else
            {
                var currentDigest = Digest(observation.Content ?? string.Empty);
                if (!request.Manifest.Artifacts.TryGetValue(artifact.LogicalPath, out var owned) || owned.ContentDigest != currentDigest)
                { changes.Add(new(artifact.LogicalPath, OutputStatus.Conflict, artifact.ArtifactId)); continue; }
                if (currentDigest == artifact.ContentDigest) changes.Add(new(artifact.LogicalPath, OutputStatus.Unchanged, artifact.ArtifactId));
                else { changes.Add(new(artifact.LogicalPath, OutputStatus.Change, artifact.ArtifactId)); operations.Add(new(FileOperationKind.Write, artifact.LogicalPath, artifact.Content)); }
            }
            next[artifact.LogicalPath] = new(artifact.ArtifactId, artifact.ContentDigest);
        }
        var proposedPaths = request.Artifacts.Select(x => x.LogicalPath).ToImmutableHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var stale in request.Manifest.Artifacts.Where(x => !proposedPaths.Contains(x.Key)).OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            changes.Add(new(stale.Key, request.StalePolicy == StaleOutputPolicy.Remove ? OutputStatus.Remove : OutputStatus.Stale, stale.Value.ArtifactId));
            if (request.StalePolicy == StaleOutputPolicy.Remove) operations.Add(new(FileOperationKind.Delete, stale.Key)); else next[stale.Key] = stale.Value;
        }
        var manifest = new OwnershipManifest("1.0", next.ToImmutable());
        var recovery = operations.Count == 0 ? null : $"output:{Digest(string.Join('|', operations.Select(x => $"{x.Kind}:{x.Path}")))[7..19]}";
        if (request.Mode == OutputMode.Apply && changes.All(x => x.Status != OutputStatus.Conflict) && operations.Count > 0)
            await fileSystem.ApplyAtomicallyAsync(operations.ToImmutable(), recovery!, cancellationToken);
        return new(changes.ToImmutable(), operations.ToImmutable(), manifest, [], recovery);
    }
    private static bool Safe(string path) => !string.IsNullOrWhiteSpace(path) && !Path.IsPathRooted(path) &&
        !path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries).Contains("..", StringComparer.Ordinal);
    private static string Digest(string content) => $"sha256:{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content)))}";
    private static OutputReport Failure(string code, string message) => new([], [], OwnershipManifest.Empty, [new(code, message)], null);
}
