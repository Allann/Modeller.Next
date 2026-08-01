using System.Collections.Immutable;
using Modeller.Templates;
using Xunit;

namespace Modeller.Templates.Tests;

public sealed class TemplatePackLoaderTests
{
    [Fact]
    public void Reordered_child_care_manifest_produces_the_same_validated_descriptor()
    {
        var first = TemplatePackLoader.Load(new PackLoadRequest(Source(Manifest(false)), ["1.0"], [new RendererSupport("scriban", "1.0")]), TestContext.Current.CancellationToken);
        var second = TemplatePackLoader.Load(new PackLoadRequest(Source(Manifest(true)), ["1.0"], [new RendererSupport("scriban", "1.0")]), TestContext.Current.CancellationToken);

        Assert.True(first.IsSuccess);
        Assert.Equal(first.Pack!.Digest, second.Pack!.Digest);
        Assert.Equal(["eligibility"], first.Pack.Artifacts.Select(x => x.ArtifactId));
    }

    [Fact]
    public void Incompatible_renderer_fails_before_planning()
    {
        var result = TemplatePackLoader.Load(new PackLoadRequest(Source(Manifest(false)), ["1.0"], [new RendererSupport("scriban", "2.0")]), TestContext.Current.CancellationToken);
        Assert.False(result.IsSuccess);
        Assert.Equal("template-pack.renderer.incompatible", Assert.Single(result.Diagnostics).Code);
    }

    private static PackSource Source(string manifest) => new("child-care-pack", manifest,
        ImmutableDictionary<string, string>.Empty.Add("rule.cs", "public static class Eligibility;"));
    private static string Manifest(bool reordered) => reordered ?
        """{"rendererVersion":"1.0","rendererId":"scriban","generationContractVersion":"1.0","version":"1.0.0","id":"child-care-csharp","artifacts":[{"semanticInputIds":["accs-eligibility"],"template":"rule.cs","owner":"child-care","path":"domain/eligibility.cs","id":"eligibility"}]}""" :
        """{"id":"child-care-csharp","version":"1.0.0","generationContractVersion":"1.0","rendererId":"scriban","rendererVersion":"1.0","artifacts":[{"id":"eligibility","path":"domain/eligibility.cs","owner":"child-care","template":"rule.cs","semanticInputIds":["accs-eligibility"]}]}""";
}
