using System.Collections.Immutable;
using Modeller.Generation;
using Xunit;

namespace Modeller.Rendering.Tests;

public sealed class TemplateRendererTests
{
    [Fact]
    public async Task Accepted_child_care_plan_renders_deterministic_artifacts_with_provenance()
    {
        var plan = ChildCarePlan();
        var adapter = new ChildCareRendererAdapter();

        var first = await TemplateRenderer.RenderAsync(new RenderingRequest(plan), adapter, TestContext.Current.CancellationToken);
        var second = await TemplateRenderer.RenderAsync(new RenderingRequest(plan), adapter, TestContext.Current.CancellationToken);

        Assert.True(first.IsSuccess);
        Assert.Equal(first.Diagnostics.ToArray(), second.Diagnostics.ToArray());
        Assert.Equal(first.Artifacts.ToArray(), second.Artifacts.ToArray());
        Assert.Equal(2, first.Artifacts.Length);
        var eligibility = first.Artifacts[0];
        Assert.Equal("domain/accs-eligibility.cs", eligibility.LogicalPath);
        Assert.Equal("namespace ChildCare.Generated;\n\npublic static class AccsEligibility;\n", eligibility.Content);
        Assert.StartsWith("sha256:", eligibility.ContentDigest, StringComparison.Ordinal);
        Assert.Equal(plan.Digest, eligibility.Provenance.PlanDigest);
        Assert.Equal("csharp-child-care", eligibility.Provenance.PackId);
        Assert.Equal("reference-test", eligibility.Provenance.RendererId);
        Assert.Equal("1.0", eligibility.Provenance.RendererContractVersion);
    }

    [Fact]
    public async Task Template_failure_returns_a_bounded_diagnostic_and_no_partial_artifacts()
    {
        var result = await TemplateRenderer.RenderAsync(
            new RenderingRequest(ChildCarePlan()),
            new FailingSecondArtifactAdapter(),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Empty(result.Artifacts);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("rendering.template.failed", diagnostic.Code);
        Assert.Equal("Template rendering failed for the proposed artifact.", diagnostic.Message);
    }

    [Fact]
    public async Task Adapter_exception_is_redacted_and_returns_no_partial_artifacts()
    {
        var result = await TemplateRenderer.RenderAsync(
            new RenderingRequest(ChildCarePlan()),
            new ThrowingAdapter(),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Empty(result.Artifacts);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("rendering.adapter.failed", diagnostic.Code);
        Assert.Equal("The renderer adapter failed.", diagnostic.Message);
        Assert.DoesNotContain("super-secret", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Pinned_scriban_templates_render_the_documented_context()
    {
        var templates = ImmutableDictionary.CreateRange<string, ScribanTemplateSource>(StringComparer.Ordinal, [
            KeyValuePair.Create("rule.cs", new ScribanTemplateSource(
                "sha256:rule",
                "namespace ChildCare.Generated;\\n\\n// Semantic input: {{ semantic_inputs[0].id }}\\npublic static class AccsEligibility;\\n")),
            KeyValuePair.Create("behaviour.cs", new ScribanTemplateSource(
                "sha256:behaviour",
                "namespace ChildCare.Generated;\\n\\n// Semantic input: {{ semantic_inputs[0].id }}\\npublic static class SubmitApplication;\\n"))
        ]);

        var result = await TemplateRenderer.RenderAsync(
            new RenderingRequest(ChildCarePlan()),
            new ScribanRendererAdapter("scriban/reference", "1.0", templates),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Contains("Semantic input: accs-eligibility", result.Artifacts[0].Content, StringComparison.Ordinal);
        Assert.Contains("public static class SubmitApplication", result.Artifacts[1].Content, StringComparison.Ordinal);
        Assert.Equal("scriban/reference", result.Artifacts[0].Provenance.RendererId);
    }

    [Fact]
    public async Task Incompatible_renderer_contract_fails_before_rendering()
    {
        var adapter = new ChildCareRendererAdapter("2.0");

        var result = await TemplateRenderer.RenderAsync(
            new RenderingRequest(ChildCarePlan()), adapter, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("rendering.renderer.incompatible", Assert.Single(result.Diagnostics).Code);
        Assert.Equal(0, adapter.RenderCount);
    }

    [Fact]
    public async Task Pre_cancelled_render_returns_no_partial_artifacts()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var result = await TemplateRenderer.RenderAsync(new RenderingRequest(ChildCarePlan()), new ChildCareRendererAdapter(), cancellation.Token);

        Assert.False(result.IsSuccess);
        Assert.Empty(result.Artifacts);
        Assert.Equal("rendering.cancelled", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public async Task Scriban_output_limit_returns_no_partial_artifacts()
    {
        var templates = ImmutableDictionary<string, ScribanTemplateSource>.Empty
            .Add("rule.cs", new("sha256:rule", "123456"))
            .Add("behaviour.cs", new("sha256:behaviour", "123456"));
        var adapter = new ScribanRendererAdapter("scriban/reference", "1.0", templates,
            new ScribanRendererLimits(MaximumOutputBytes: 5));

        var result = await TemplateRenderer.RenderAsync(new RenderingRequest(ChildCarePlan()), adapter, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Empty(result.Artifacts);
        Assert.Equal("rendering.limit.output-size", Assert.Single(result.Diagnostics).Code);
    }

    private static GenerationPlan ChildCarePlan() => new(
        "1.0",
        "generated",
        [
            Artifact(0, "accs-eligibility", "domain/accs-eligibility.cs", "rule.cs", "sha256:rule", "sha256:eligibility"),
            Artifact(1, "submit-application", "application/submit-application.cs", "behaviour.cs", "sha256:behaviour", "sha256:submit")
        ],
        "sha256:child-care-plan");

    private static ProposedArtifact Artifact(
        int ordinal,
        string id,
        string path,
        string template,
        string templateDigest,
        string inputDigest) => new(
            ordinal,
            id,
            path,
            new ArtifactOwnership("child-care", "csharp-child-care", "1.0.0", template),
            [new PlannedSemanticInput(id, "child-care", $"sha256:{id}")],
            templateDigest,
            inputDigest);

    private sealed class ChildCareRendererAdapter(string contractVersion = "1.0") : IRendererAdapter
    {
        public string RendererId => "reference-test";
        public string ContractVersion => contractVersion;
        public int RenderCount { get; private set; }

        public ValueTask<AdapterRenderResult> RenderAsync(
            ArtifactRenderingContext context,
            CancellationToken cancellationToken = default)
        {
            RenderCount++;
            var name = context.Artifact.ArtifactId == "accs-eligibility" ? "AccsEligibility" : "SubmitApplication";
            return ValueTask.FromResult(new AdapterRenderResult(
                $"namespace ChildCare.Generated;\n\npublic static class {name};\n",
                []));
        }
    }

    private sealed class FailingSecondArtifactAdapter : IRendererAdapter
    {
        public string RendererId => "failing-test";
        public string ContractVersion => "1.0";

        public ValueTask<AdapterRenderResult> RenderAsync(
            ArtifactRenderingContext context,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(context.Artifact.Ordinal == 0
                ? new AdapterRenderResult("first", [])
                : new AdapterRenderResult(null, [new RenderingDiagnostic(
                    "rendering.template.failed",
                    "Template rendering failed for the proposed artifact.")]));
    }

    private sealed class ThrowingAdapter : IRendererAdapter
    {
        public string RendererId => "throwing-test";
        public string ContractVersion => "1.0";

        public ValueTask<AdapterRenderResult> RenderAsync(
            ArtifactRenderingContext context,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("super-secret token");
    }
}
