using CsCheck;
using Modeller.Rendering;
using Modeller.Templates;
using Xunit;

namespace Modeller.Api.Tests;

public sealed class WorkspaceGenerationPreviewPipelineTests
{
    /// <summary>Determinism/format invariant: <see cref="WorkspaceGenerationPreviewPipeline.Digest"/>
    /// is used as a cache/comparison key for the request's effective configuration
    /// (<c>ConfigurationDigest</c> on <c>WorkspaceGenerateResponse</c>), so callers rely on it being
    /// a pure function of its input with a stable shape - same content in, same digest out, every
    /// time, always <c>sha256:</c> followed by exactly 64 lowercase hex characters.</summary>
    [Fact]
    public void Digest_is_deterministic_and_fixed_shape_for_arbitrary_content()
    {
        var gen = Gen.String[0, 500];

        gen.Sample(content =>
        {
            var first = WorkspaceGenerationPreviewPipeline.Digest(content);
            var second = WorkspaceGenerationPreviewPipeline.Digest(content);

            Assert.Equal(first, second);
            Assert.StartsWith("sha256:", first, StringComparison.Ordinal);
            var hex = first["sha256:".Length..];
            Assert.Equal(64, hex.Length);
            Assert.Matches("^[0-9a-f]{64}$", hex);
        });
    }

    /// <summary>Collision-avoidance property: two distinct inputs must not produce the same digest.
    /// This isn't a mathematical guarantee for any hash function, but for SHA-256 over the small,
    /// short random strings CsCheck samples here, a collision is astronomically unlikely - so this
    /// is a meaningful regression guard against, e.g., an accidental truncation or a switch to a
    /// weaker/narrower hash that would make collisions actually plausible.</summary>
    [Fact]
    public void Digest_differs_for_distinct_content()
    {
        var gen =
            from a in Gen.String[1, 100]
            from b in Gen.String[1, 100]
            where a != b
            select (a, b);

        gen.Sample(pair =>
        {
            var (a, b) = pair;
            Assert.NotEqual(WorkspaceGenerationPreviewPipeline.Digest(a), WorkspaceGenerationPreviewPipeline.Digest(b));
        });
    }

    /// <summary>Round-trip invariant for the C# language-parameter defaults: the pipeline has no
    /// request field for namespace/target-framework (see the deviation noted in
    /// <see cref="WorkspaceGenerationPreviewPipeline"/>'s doc comment on <c>BuildResponseAsync</c>),
    /// so it derives them from <c>projectName</c>. Whatever <c>projectName</c> the caller supplies,
    /// the namespace default must echo it back exactly (never silently mutate or truncate it) and
    /// the pinned target framework must never vary.</summary>
    [Fact]
    public void DefaultLanguageParameters_for_csharp_echoes_project_name_as_namespace()
    {
        var capability = RendererCapabilityRegistry.Resolve(new RendererIdentity("scriban", "1.0"), "csharp")!;
        var gen = Gen.String[1, 80];

        gen.Sample(projectName =>
        {
            var parameters = WorkspaceGenerationPreviewPipeline.DefaultLanguageParameters(capability, projectName);

            Assert.Equal(projectName, parameters["namespace"]);
            Assert.Equal("net10.0", parameters["targetFramework"]);
        });
    }

    /// <summary>Same round-trip invariant for Python: the package name default must be the
    /// lower-invariant transform of <c>projectName</c> and nothing else - not a truncation, not a
    /// slug, not the original casing.</summary>
    [Fact]
    public void DefaultLanguageParameters_for_python_lowercases_project_name_as_package_name()
    {
        var capability = RendererCapabilityRegistry.Resolve(new RendererIdentity("scriban", "1.0"), "python")!;
        var gen = Gen.String[1, 80];

        gen.Sample(projectName =>
        {
            var parameters = WorkspaceGenerationPreviewPipeline.DefaultLanguageParameters(capability, projectName);

            Assert.Equal(projectName.ToLowerInvariant(), parameters["packageName"]);
            Assert.Equal("3.13", parameters["pythonVersion"]);
        });
    }
}
