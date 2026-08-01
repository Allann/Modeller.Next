using System.Collections.Immutable;
using Modeller.Output;
using Modeller.Rendering;
using Xunit;

namespace Modeller.Output.Tests;

public sealed class OutputApplicationTests
{
    [Fact]
    public async Task Preview_reports_child_care_creates_without_writes()
    {
        var fs = new MemoryFileSystem();
        var report = await OutputApplication.ExecuteAsync(Request(OutputMode.Preview), fs, TestContext.Current.CancellationToken);
        Assert.Equal(OutputStatus.Create, Assert.Single(report.Changes).Status);
        Assert.Equal(0, fs.ApplyCount);
    }

    [Fact]
    public async Task Apply_and_reapply_are_atomic_and_idempotent()
    {
        var fs = new MemoryFileSystem();
        var applied = await OutputApplication.ExecuteAsync(Request(OutputMode.Apply), fs, TestContext.Current.CancellationToken);
        var reapplied = await OutputApplication.ExecuteAsync(Request(OutputMode.Apply, applied.Manifest), fs, TestContext.Current.CancellationToken);
        Assert.True(applied.IsSuccess);
        Assert.Equal(OutputStatus.Unchanged, Assert.Single(reapplied.Changes).Status);
        Assert.Equal(1, fs.ApplyCount);
    }

    [Fact]
    public async Task Handwritten_collision_is_preserved_and_stale_output_requires_explicit_policy()
    {
        var fs = new MemoryFileSystem(ImmutableDictionary<string, string>.Empty.Add("domain/eligibility.cs", "handwritten"));
        var collision = await OutputApplication.ExecuteAsync(Request(OutputMode.Apply), fs, TestContext.Current.CancellationToken);
        Assert.Equal(OutputStatus.Conflict, Assert.Single(collision.Changes).Status);
        Assert.Equal("handwritten", fs.Files["domain/eligibility.cs"]);

        var staleManifest = new OwnershipManifest("1.0", ImmutableDictionary<string, OwnedArtifact>.Empty
            .Add("old.g.cs", new("old", "sha256:old")));
        var stale = await OutputApplication.ExecuteAsync(Request(OutputMode.Preview, staleManifest), new MemoryFileSystem(
            ImmutableDictionary<string, string>.Empty.Add("old.g.cs", "old")), TestContext.Current.CancellationToken);
        Assert.Contains(stale.Changes, change => change.Status == OutputStatus.Stale);
        Assert.DoesNotContain(stale.Operations, operation => operation.Kind == FileOperationKind.Delete);
    }

    private static OutputRequest Request(OutputMode mode, OwnershipManifest? manifest = null) => new("generated", mode, StaleOutputPolicy.Report,
        [new ProposedOutputArtifact(0, "eligibility", "domain/eligibility.cs", "generated", "sha256:e0cb800a5ccda4cb1b2ad7990de082aaa1e40e771898c0bcb28fcb23c261e422",
            new("plan", "pack", "1.0", "rule.cs", "template", "input", "scriban", "1.0"))], manifest ?? OwnershipManifest.Empty);

    private sealed class MemoryFileSystem(ImmutableDictionary<string, string>? initial = null) : IOutputFileSystem
    {
        public ImmutableDictionary<string, string> Files { get; private set; } = initial ?? ImmutableDictionary<string, string>.Empty;
        public int ApplyCount { get; private set; }
        public ValueTask<FileObservation> InspectAsync(string path, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Files.TryGetValue(path, out var content) ? new FileObservation(true, content, false) : new FileObservation(false, null, false));
        public ValueTask ApplyAtomicallyAsync(ImmutableArray<FileOperation> operations, string recoveryToken, CancellationToken cancellationToken)
        {
            ApplyCount++;
            foreach (var operation in operations) Files = operation.Kind == FileOperationKind.Delete ? Files.Remove(operation.Path) : Files.SetItem(operation.Path, operation.Content!);
            return ValueTask.CompletedTask;
        }
    }
}
