using System.Collections.Immutable;
using Modeller.Templates;

namespace Modeller.Rendering;

/// <summary>
/// Every registered <see cref="IRendererCapability"/>. Adding a new language means adding its own capability
/// implementation in its own file and one entry here — existing capability implementations are never touched.
/// </summary>
public static class RendererCapabilityRegistry
{
    private static readonly ImmutableArray<IRendererCapability> Capabilities =
        [new CSharpScribanRendererCapability(), new PythonScribanRendererCapability()];

    public static IRendererCapability? Resolve(RendererIdentity renderer, string language) =>
        Capabilities.FirstOrDefault(capability =>
            capability.Renderer.Equals(renderer) && StringComparer.Ordinal.Equals(capability.Language, language));

    public static ImmutableArray<RendererIdentity> SupportedRenderers =>
        [.. Capabilities.Select(capability => capability.Renderer).Distinct()];
}
