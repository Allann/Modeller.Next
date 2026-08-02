using System.Collections.Immutable;
using Modeller.Templates;
using Xunit;

namespace Modeller.Templates.Tests;

public sealed class TemplatePackLoaderTests
{
    [Fact]
    public void Reordered_child_care_manifest_produces_the_same_validated_descriptor()
    {
        var first = TemplatePackLoader.Load(new PackLoadRequest(Source(Manifest(false)), ["1.0"], [new RendererIdentity("scriban", "1.0")]), TestContext.Current.CancellationToken);
        var second = TemplatePackLoader.Load(new PackLoadRequest(Source(Manifest(true)), ["1.0"], [new RendererIdentity("scriban", "1.0")]), TestContext.Current.CancellationToken);

        Assert.True(first.IsSuccess);
        Assert.Equal(first.Pack!.Digest, second.Pack!.Digest);
        Assert.Equal(["rule"], first.Pack.Outputs.Select(x => x.Id));
        Assert.Equal("1.0.0", first.Pack.PackVersion);
    }

    [Fact]
    public void Incompatible_renderer_fails_before_planning()
    {
        var result = TemplatePackLoader.Load(new PackLoadRequest(Source(Manifest(false)), ["1.0"], [new RendererIdentity("scriban", "2.0")]), TestContext.Current.CancellationToken);
        Assert.False(result.IsSuccess);
        Assert.Equal("template-pack.renderer.incompatible", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Manifest_without_language_is_rejected()
    {
        var manifest = """{"id":"csharp-domain-project","version":"1.0","packVersion":"1.0.0","generationContractVersion":"1.0","rendererId":"scriban","rendererVersion":"1.0","outputs":[{"id":"rule","scope":"rule","logicalPath":"Rules/{definitionName}.cs","owner":"csharp-domain-project","templateId":"rule.cs"}]}""";
        var result = TemplatePackLoader.Load(new PackLoadRequest(Source(manifest), ["1.0"], [new RendererIdentity("scriban", "1.0")]), TestContext.Current.CancellationToken);
        Assert.False(result.IsSuccess);
        Assert.Equal("template-pack.language.required", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Manifest_with_an_unsupported_schema_version_is_rejected()
    {
        var manifest = """{"id":"csharp-domain-project","version":"2.0","packVersion":"1.0.0","generationContractVersion":"1.0","rendererId":"scriban","rendererVersion":"1.0","language":"csharp","outputs":[{"id":"rule","scope":"rule","logicalPath":"Rules/{definitionName}.cs","owner":"csharp-domain-project","templateId":"rule.cs"}]}""";
        var result = TemplatePackLoader.Load(new PackLoadRequest(Source(manifest), ["1.0"], [new RendererIdentity("scriban", "1.0")]), TestContext.Current.CancellationToken);
        Assert.False(result.IsSuccess);
        Assert.Equal("template-pack.schema-version.unsupported", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Invalid_json_manifest_is_rejected()
    {
        var result = TemplatePackLoader.Load(new PackLoadRequest(Source("not json"), ["1.0"], [new RendererIdentity("scriban", "1.0")]), TestContext.Current.CancellationToken);
        Assert.False(result.IsSuccess);
        Assert.Equal("template-pack.manifest.invalid", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Manifest_missing_identity_is_rejected()
    {
        var manifest = """{"id":"","version":"1.0","packVersion":"1.0.0","generationContractVersion":"1.0","rendererId":"scriban","rendererVersion":"1.0","language":"csharp","outputs":[]}""";
        var result = TemplatePackLoader.Load(new PackLoadRequest(Source(manifest), ["1.0"], [new RendererIdentity("scriban", "1.0")]), TestContext.Current.CancellationToken);
        Assert.False(result.IsSuccess);
        Assert.Equal("template-pack.identity.required", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Manifest_with_an_incompatible_generation_contract_is_rejected()
    {
        var result = TemplatePackLoader.Load(new PackLoadRequest(Source(Manifest(false)), ["2.0"], [new RendererIdentity("scriban", "1.0")]), TestContext.Current.CancellationToken);
        Assert.False(result.IsSuccess);
        Assert.Equal("template-pack.generation-contract.incompatible", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Output_with_an_unsupported_scope_is_rejected()
    {
        var manifest = """{"id":"csharp-domain-project","version":"1.0","packVersion":"1.0.0","generationContractVersion":"1.0","rendererId":"scriban","rendererVersion":"1.0","language":"csharp","outputs":[{"id":"rule","scope":"unsupported","logicalPath":"Rules/{definitionName}.cs","owner":"csharp-domain-project","templateId":"rule.cs"}]}""";
        var result = TemplatePackLoader.Load(new PackLoadRequest(Source(manifest), ["1.0"], [new RendererIdentity("scriban", "1.0")]), TestContext.Current.CancellationToken);
        Assert.False(result.IsSuccess);
        Assert.Equal("template-pack.output.invalid", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Output_referencing_a_missing_template_is_rejected()
    {
        var manifest = """{"id":"csharp-domain-project","version":"1.0","packVersion":"1.0.0","generationContractVersion":"1.0","rendererId":"scriban","rendererVersion":"1.0","language":"csharp","outputs":[{"id":"rule","scope":"rule","logicalPath":"Rules/{definitionName}.cs","owner":"csharp-domain-project","templateId":"missing.cs"}]}""";
        var result = TemplatePackLoader.Load(new PackLoadRequest(Source(manifest), ["1.0"], [new RendererIdentity("scriban", "1.0")]), TestContext.Current.CancellationToken);
        Assert.False(result.IsSuccess);
        Assert.Equal("template-pack.template.missing", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Duplicate_output_identities_are_rejected()
    {
        var manifest = """{"id":"csharp-domain-project","version":"1.0","packVersion":"1.0.0","generationContractVersion":"1.0","rendererId":"scriban","rendererVersion":"1.0","language":"csharp","outputs":[{"id":"rule","scope":"rule","logicalPath":"Rules/{definitionName}.cs","owner":"csharp-domain-project","templateId":"rule.cs"},{"id":"rule","scope":"rule","logicalPath":"Rules/Other/{definitionName}.cs","owner":"csharp-domain-project","templateId":"rule.cs"}]}""";
        var result = TemplatePackLoader.Load(new PackLoadRequest(Source(manifest), ["1.0"], [new RendererIdentity("scriban", "1.0")]), TestContext.Current.CancellationToken);
        Assert.False(result.IsSuccess);
        Assert.Equal("template-pack.output.duplicate", Assert.Single(result.Diagnostics).Code);
    }

    private static PackSource Source(string manifest) => new("child-care-pack", manifest,
        ImmutableDictionary<string, string>.Empty.Add("rule.cs", "public static class Eligibility;"));
    private static string Manifest(bool reordered) => reordered ?
        """{"rendererVersion":"1.0","rendererId":"scriban","language":"csharp","generationContractVersion":"1.0","packVersion":"1.0.0","version":"1.0","id":"csharp-domain-project","outputs":[{"scope":"rule","templateId":"rule.cs","owner":"csharp-domain-project","logicalPath":"Rules/{definitionName}.cs","id":"rule"}]}""" :
        """{"id":"csharp-domain-project","version":"1.0","packVersion":"1.0.0","generationContractVersion":"1.0","rendererId":"scriban","rendererVersion":"1.0","language":"csharp","outputs":[{"id":"rule","scope":"rule","logicalPath":"Rules/{definitionName}.cs","owner":"csharp-domain-project","templateId":"rule.cs"}]}""";
}
