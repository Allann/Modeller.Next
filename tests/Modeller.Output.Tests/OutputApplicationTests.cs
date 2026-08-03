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

    [Fact]
    public async Task Changed_content_for_an_owned_artifact_is_written_and_reported_as_change()
    {
        var manifest = new OwnershipManifest("1.0", ImmutableDictionary<string, OwnedArtifact>.Empty
            .Add("domain/eligibility.cs", new("eligibility", "sha256:e0cb800a5ccda4cb1b2ad7990de082aaa1e40e771898c0bcb28fcb23c261e422")));
        var fs = new MemoryFileSystem(ImmutableDictionary<string, string>.Empty.Add("domain/eligibility.cs", "generated"));

        var report = await OutputApplication.ExecuteAsync(Request(OutputMode.Apply, manifest, "changed content"), fs, TestContext.Current.CancellationToken);

        Assert.Equal(OutputStatus.Change, Assert.Single(report.Changes).Status);
        Assert.Equal(FileOperationKind.Write, Assert.Single(report.Operations).Kind);
        Assert.Equal("changed content", fs.Files["domain/eligibility.cs"]);
    }

    [Fact]
    public async Task Symbolic_link_at_the_target_path_is_reported_as_a_conflict_without_writing()
    {
        var fs = new SymlinkFileSystem();

        var report = await OutputApplication.ExecuteAsync(Request(OutputMode.Apply), fs, TestContext.Current.CancellationToken);

        Assert.Equal(OutputStatus.Conflict, Assert.Single(report.Changes).Status);
        Assert.Empty(report.Operations);
    }

    [Fact]
    public async Task Stale_output_policy_remove_deletes_the_file_and_drops_it_from_the_manifest()
    {
        var staleManifest = new OwnershipManifest("1.0", ImmutableDictionary<string, OwnedArtifact>.Empty
            .Add("old.g.cs", new("old", "sha256:old")));
        var fs = new MemoryFileSystem(ImmutableDictionary<string, string>.Empty.Add("old.g.cs", "old"));

        var report = await OutputApplication.ExecuteAsync(
            new OutputRequest("generated", OutputMode.Apply, StaleOutputPolicy.Remove,
                [Artifact("generated")], staleManifest),
            fs,
            TestContext.Current.CancellationToken);

        Assert.Contains(report.Changes, change => change.Path == "old.g.cs" && change.Status == OutputStatus.Remove);
        Assert.Contains(report.Operations, operation => operation.Kind == FileOperationKind.Delete && operation.Path == "old.g.cs");
        Assert.False(fs.Files.ContainsKey("old.g.cs"));
        Assert.False(report.Manifest.Artifacts.ContainsKey("old.g.cs"));
    }

    [Fact]
    public async Task Unsafe_target_root_is_rejected_with_a_path_invalid_diagnostic()
    {
        var report = await OutputApplication.ExecuteAsync(
            new OutputRequest("../escape", OutputMode.Apply, StaleOutputPolicy.Report, [Artifact("generated")], OwnershipManifest.Empty),
            new MemoryFileSystem(),
            TestContext.Current.CancellationToken);

        Assert.False(report.IsSuccess);
        Assert.Empty(report.Changes);
        Assert.Equal("output.path.invalid", Assert.Single(report.Diagnostics).Code);
    }

    [Fact]
    public async Task Unsafe_artifact_logical_path_is_rejected_with_a_path_invalid_diagnostic()
    {
        var artifact = new ProposedOutputArtifact(0, "eligibility", "../escape.cs", "generated",
            "sha256:e0cb800a5ccda4cb1b2ad7990de082aaa1e40e771898c0bcb28fcb23c261e422",
            new("plan", "pack", "1.0", "rule.cs", "template", "input", "scriban", "1.0"));

        var report = await OutputApplication.ExecuteAsync(
            new OutputRequest("generated", OutputMode.Apply, StaleOutputPolicy.Report, [artifact], OwnershipManifest.Empty),
            new MemoryFileSystem(),
            TestContext.Current.CancellationToken);

        Assert.False(report.IsSuccess);
        Assert.Equal("output.path.invalid", Assert.Single(report.Diagnostics).Code);
    }

    [Fact]
    public async Task Cancellation_is_reported_as_a_cancelled_diagnostic()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var report = await OutputApplication.ExecuteAsync(Request(OutputMode.Apply), new MemoryFileSystem(), cts.Token);

        Assert.False(report.IsSuccess);
        Assert.Equal("output.cancelled", Assert.Single(report.Diagnostics).Code);
    }

    [Fact]
    public async Task Artifact_whose_content_does_not_match_its_declared_digest_is_rejected()
    {
        var artifact = new ProposedOutputArtifact(0, "eligibility", "domain/eligibility.cs", "generated",
            "sha256:0000000000000000000000000000000000000000000000000000000000000000",
            new("plan", "pack", "1.0", "rule.cs", "template", "input", "scriban", "1.0"));

        var report = await OutputApplication.ExecuteAsync(
            new OutputRequest("generated", OutputMode.Apply, StaleOutputPolicy.Report, [artifact], OwnershipManifest.Empty),
            new MemoryFileSystem(),
            TestContext.Current.CancellationToken);

        Assert.False(report.IsSuccess);
        Assert.Equal("output.artifact.digest-mismatch", Assert.Single(report.Diagnostics).Code);
    }

    private static ProposedOutputArtifact Artifact(string content) => new(0, "eligibility", "domain/eligibility.cs", content,
        $"sha256:{Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(content)))}",
        new("plan", "pack", "1.0", "rule.cs", "template", "input", "scriban", "1.0"));

    private static OutputRequest Request(OutputMode mode, OwnershipManifest? manifest = null, string content = "generated") => new("generated", mode, StaleOutputPolicy.Report,
        [Artifact(content)], manifest ?? OwnershipManifest.Empty);

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

    private sealed class SymlinkFileSystem : IOutputFileSystem
    {
        public ValueTask<FileObservation> InspectAsync(string path, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new FileObservation(true, null, true));
        public ValueTask ApplyAtomicallyAsync(ImmutableArray<FileOperation> operations, string recoveryToken, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("A conflicted artifact must not be applied.");
    }
}
