using System.Collections.Immutable;
using Modeller.Model;
using Modeller.Templates;

namespace Modeller.Rendering;

/// <summary>
/// Resolves the naming convention, globals projection and renderer identity for a template pack's declared
/// language through a single registered strategy, instead of repeated language switches at each call site.
/// The compatibility seam a caller validates against remains <see cref="Renderer"/> — a single
/// <see cref="RendererIdentity"/>, never independent id/version strings that could drift apart — see
/// <see cref="Modeller.Templates.TemplatePackLoader"/>; <see cref="Language"/> only selects which capability
/// applies among renderers sharing that identity.
/// </summary>
public interface IRendererCapability
{
    RendererIdentity Renderer { get; }
    string Language { get; }
    Func<string, string> NameForPath { get; }

    /// <summary>The pack-parameter keys this capability requires, e.g. <c>["namespace", "targetFramework"]</c>.</summary>
    ImmutableArray<string> RequiredParameterKeys { get; }

    /// <summary>Validates language-scoped pack parameters, returning a stable diagnostic code on failure.</summary>
    bool TryValidateParameters(IReadOnlyDictionary<string, string> parameters, out string? diagnosticCode);

    ITemplateGlobalsProvider CreateGlobalsProvider(AuthoredContextRevision revision, string projectName, IReadOnlyDictionary<string, string> parameters);
}

public static class RendererCapabilityValidation
{
    /// <summary>
    /// Shared across every capability: every <see cref="IRendererCapability.RequiredParameterKeys"/> entry must be
    /// present and non-blank. Capabilities call this first, then layer their own format-specific checks (regex,
    /// etc.) on top — the presence check itself is never duplicated per language.
    /// </summary>
    public static bool HasAllRequiredParameters(this IRendererCapability capability, IReadOnlyDictionary<string, string> parameters) =>
        capability.RequiredParameterKeys.All(key => parameters.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value));
}
