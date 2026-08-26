using CsCheck;
using Xunit;

namespace Modeller.Api.Tests;

public sealed class EmbeddedTemplatePackCatalogTests
{
    [Fact]
    public void TryGet_resolves_the_known_csharp_domain_project_pack()
    {
        Assert.True(EmbeddedTemplatePackCatalog.TryGet("csharp/domain-project", out var source));
        Assert.Equal("csharp-domain-project", source.Name);
        Assert.False(string.IsNullOrWhiteSpace(source.Manifest));
    }

    [Fact]
    public void TryGet_rejects_a_null_id()
    {
        Assert.False(EmbeddedTemplatePackCatalog.TryGet(null, out var source));
        Assert.Null(source);
    }

    /// <summary>Round-trip invariant: every template the pack's own manifest (<c>pack.json</c>'s
    /// "templates" array) claims to contain must actually be present in the loaded
    /// <c>PackSource.Templates</c> dictionary with non-empty content - if the manifest and the
    /// embedded resources ever drift (a template renamed/removed from one but not the other), this
    /// invariant is what would catch it, rather than a failure surfacing only much later inside
    /// <c>TemplatePackLoader.Load</c> or <c>TemplateRenderer.RenderAsync</c>.</summary>
    [Fact]
    public void TryGet_result_never_omits_a_template_its_own_manifest_declares()
    {
        Assert.True(EmbeddedTemplatePackCatalog.TryGet("csharp/domain-project", out var source));
        using var document = System.Text.Json.JsonDocument.Parse(source.Manifest);
        var declaredIds = document.RootElement.GetProperty("templates").EnumerateArray()
            .Select(template => template.GetProperty("id").GetString()!)
            .ToArray();

        Assert.NotEmpty(declaredIds);
        foreach (var id in declaredIds)
        {
            Assert.True(source.Templates.ContainsKey(id), $"manifest declares template '{id}' but the loaded pack has no content for it");
            Assert.False(string.IsNullOrEmpty(source.Templates[id]));
        }
    }

    /// <summary>Robustness property: <c>TryGet</c> is a total function over arbitrary caller input -
    /// it must never throw, and must return <c>false</c> for anything other than the exact known
    /// pack id, however the string is shaped (empty, whitespace, wrong case, embedded separators,
    /// non-ASCII). A request-shape endpoint like <c>POST /v1/workspace/generate</c> passes an
    /// arbitrary client-supplied string straight into this lookup, so "never throws" is the actual
    /// contract, not just "returns the right answer for the happy path".</summary>
    [Fact]
    public void TryGet_never_throws_and_only_ever_succeeds_for_the_known_id()
    {
        var gen = Gen.String[Gen.Char[(char)0, (char)0x2FFF], 0, 64];

        gen.Sample(candidate =>
        {
            var found = EmbeddedTemplatePackCatalog.TryGet(candidate, out var source);
            if (candidate == "csharp/domain-project")
            {
                Assert.True(found);
                Assert.NotNull(source);
            }
            else
            {
                Assert.False(found);
                Assert.Null(source);
            }
        });
    }
}
