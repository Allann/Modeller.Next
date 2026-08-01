using System.Collections.Immutable;
using Modeller.Contexts;
using Modeller.Generation;
using Xunit;

namespace Modeller.Generation.Tests;

public sealed class GenerationPlannerTests
{
    [Fact]
    public void Same_child_care_inputs_produce_the_same_ordered_plan_and_digest()
    {
        var request = ChildCareRequest();

        var first = GenerationPlanner.Plan(request, TestContext.Current.CancellationToken);
        var second = GenerationPlanner.Plan(request, TestContext.Current.CancellationToken);

        Assert.True(first.IsSuccess);
        Assert.Equal(first.Plan!.Digest, second.Plan!.Digest);
        Assert.Equal(
            first.Plan.Artifacts.Select(ArtifactObservation),
            second.Plan.Artifacts.Select(ArtifactObservation));
        Assert.Equal(
            ["application/submit-application.cs", "domain/accs-eligibility.cs"],
            first.Plan.Artifacts.Select(artifact => artifact.LogicalPath));
        Assert.Equal("sha256:556c2eaacda7bfa01ee332be7f4288db2da5d9054ddf8a098f8bde89990e218d", first.Plan.Digest);
        Assert.All(first.Plan.Artifacts, artifact => Assert.Equal("child-care", artifact.Ownership.Owner));
    }

    private static string ArtifactObservation(ProposedArtifact artifact) =>
        $"{artifact.Ordinal}|{artifact.ArtifactId}|{artifact.LogicalPath}|{artifact.Ownership}|{artifact.InputDigest}";

    [Fact]
    public void Changed_semantic_input_changes_only_artifacts_that_declare_it()
    {
        var original = ChildCareRequest();
        var changed = original with
        {
            Snapshot = original.Snapshot with
            {
                SemanticInputs = original.Snapshot.SemanticInputs
                    .Select(input => input.Id == "accs-eligibility"
                        ? input with { SemanticDigest = "sha256:changed-eligibility" }
                        : input)
                    .ToImmutableArray()
            }
        };

        var before = GenerationPlanner.Plan(original, TestContext.Current.CancellationToken).Plan!;
        var after = GenerationPlanner.Plan(changed, TestContext.Current.CancellationToken).Plan!;

        Assert.NotEqual(before.Digest, after.Digest);
        Assert.Equal(before.Artifacts[0].InputDigest, after.Artifacts[0].InputDigest);
        Assert.NotEqual(before.Artifacts[1].InputDigest, after.Artifacts[1].InputDigest);
    }

    [Fact]
    public void Missing_semantic_input_fails_before_rendering()
    {
        var request = ChildCareRequest();
        request = request with
        {
            Snapshot = request.Snapshot with { SemanticInputs = request.Snapshot.SemanticInputs.RemoveAt(0) }
        };

        var result = GenerationPlanner.Plan(request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Plan);
        Assert.Equal("generation.artifact.input-unresolved", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Incompatible_pack_and_configuration_fail_before_rendering()
    {
        var request = ChildCareRequest();
        request = request with
        {
            TemplatePack = request.TemplatePack with { GenerationContractVersion = "2.0" }
        };

        var result = GenerationPlanner.Plan(request, TestContext.Current.CancellationToken);

        Assert.Equal("generation.pack.incompatible", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Artifact_paths_cannot_escape_the_logical_output_root()
    {
        var request = ChildCareRequest();
        request = request with
        {
            TemplatePack = request.TemplatePack with
            {
                Artifacts = [request.TemplatePack.Artifacts[0] with { LogicalPath = "../secrets.cs" }]
            }
        };

        var result = GenerationPlanner.Plan(request, TestContext.Current.CancellationToken);

        Assert.Equal("generation.artifact.path-invalid", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Conflicting_artifact_ownership_is_rejected()
    {
        var request = ChildCareRequest();
        request = request with
        {
            TemplatePack = request.TemplatePack with
            {
                Artifacts =
                [
                    request.TemplatePack.Artifacts[0] with { LogicalPath = "shared/output.cs" },
                    request.TemplatePack.Artifacts[1] with { LogicalPath = "shared/output.cs", Owner = "subsidy" }
                ]
            }
        };

        var result = GenerationPlanner.Plan(request, TestContext.Current.CancellationToken);

        Assert.Equal("generation.artifact.ownership-conflict", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Cancellation_returns_no_partial_plan()
    {
        var result = GenerationPlanner.Plan(ChildCareRequest(), new CancellationToken(canceled: true));

        Assert.Null(result.Plan);
        Assert.Equal("generation.plan.cancelled", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Artifact_budget_is_enforced_before_planning()
    {
        var request = ChildCareRequest();
        request = request with
        {
            TemplatePack = request.TemplatePack with
            {
                Artifacts = Enumerable.Range(0, 10_001)
                    .Select(index => request.TemplatePack.Artifacts[0] with
                    {
                        ArtifactId = $"artifact-{index}",
                        LogicalPath = $"generated/artifact-{index}.cs"
                    })
                    .ToImmutableArray()
            }
        };

        var result = GenerationPlanner.Plan(request, TestContext.Current.CancellationToken);

        Assert.Equal("generation.plan.limit.artifacts", Assert.Single(result.Diagnostics).Code);
    }

    private static GenerationPlanningRequest ChildCareRequest()
    {
        var contextId = "0191f6d4-4ea0-7000-8000-000000000001";
        var snapshot = new ResolvedGenerationSnapshot(
            new FederationSnapshot([
                new FederationPackageLock(
                    contextId,
                    "child-care",
                    "1.0.0",
                    "sha256:package",
                    "sha256:context")
            ]),
            [
                new GenerationSemanticInput("accs-eligibility", contextId, "sha256:eligibility"),
                new GenerationSemanticInput("submit-application", contextId, "sha256:submit")
            ]);
        var configuration = new ValidatedGenerationConfiguration(
            "child-care-csharp",
            "1.0",
            "generated",
            "sha256:configuration");
        var pack = new ValidatedTemplatePackDescriptor(
            "csharp-child-care",
            "1.0.0",
            "1.0",
            "sha256:pack",
            [
                new TemplateArtifactDescriptor(
                    "submit-application",
                    "behaviour.cs",
                    "application/submit-application.cs",
                    "child-care",
                    "sha256:behaviour-template",
                    ["submit-application"]),
                new TemplateArtifactDescriptor(
                    "accs-eligibility",
                    "rule.cs",
                    "domain/accs-eligibility.cs",
                    "child-care",
                    "sha256:rule-template",
                    ["accs-eligibility"])
            ]);
        return new GenerationPlanningRequest(snapshot, configuration, pack);
    }
}
