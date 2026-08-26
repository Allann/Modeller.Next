using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using Modeller.Templates;

namespace Modeller.Api;

/// <summary>
/// The small, in-process catalog of template packs <c>POST /v1/workspace/generate</c> can resolve
/// without a host filesystem — mapping a request's public <c>TemplatePackId</c> string (e.g.
/// "csharp/domain-project") to the <see cref="PackSource"/> <see cref="TemplatePackLoader.Load"/>
/// needs. Pack content is embedded at build time (see the <c>EmbeddedResource</c> entries in
/// Modeller.Api.csproj) rather than read from disk at runtime, so resolution never depends on the
/// deployed container's working directory or the samples/ tree being present alongside it. Today
/// this holds exactly one pack — the Child Care sample's "csharp/domain-project" — matching the
/// only pack the Specifier stage named as known.
/// </summary>
public static class EmbeddedTemplatePackCatalog
{
    private static readonly ImmutableDictionary<string, PackSource> Packs = BuildCatalog();

    /// <summary>An unrecognized <paramref name="templatePackId"/> is expected user input (an
    /// unknown pack id typed by a caller), not a server error — the caller (the generation-preview
    /// pipeline) turns a <c>false</c> result into a content diagnostic on a 200 response, exactly
    /// like a parse/validation failure, per the Specifier stage's decision.</summary>
    public static bool TryGet(string? templatePackId, out PackSource source)
    {
        if (templatePackId is not null && Packs.TryGetValue(templatePackId, out var found))
        {
            source = found;
            return true;
        }
        source = null!;
        return false;
    }

    private static ImmutableDictionary<string, PackSource> BuildCatalog()
    {
        var builder = ImmutableDictionary.CreateBuilder<string, PackSource>(StringComparer.Ordinal);
        var csharpDomainProject = LoadEmbeddedPack("csharp-domain-project");
        if (csharpDomainProject is not null)
            builder.Add("csharp/domain-project", csharpDomainProject);
        return builder.ToImmutable();
    }

    private static PackSource? LoadEmbeddedPack(string resourceFolder)
    {
        var assembly = typeof(EmbeddedTemplatePackCatalog).Assembly;
        var manifestText = ReadResource(assembly, $"TemplatePacks/{resourceFolder}/pack.json");
        if (manifestText is null) return null;

        JsonDocument document;
        try { document = JsonDocument.Parse(manifestText); }
        catch (JsonException) { return null; }
        using (document)
        {
            if (!document.RootElement.TryGetProperty("templates", out var templatesElement)) return null;
            var templates = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
            foreach (var template in templatesElement.EnumerateArray())
            {
                if (!template.TryGetProperty("id", out var idProperty) || !template.TryGetProperty("path", out var pathProperty))
                    return null;
                var id = idProperty.GetString();
                var path = pathProperty.GetString();
                if (id is null || path is null) return null;
                var content = ReadResource(assembly, $"TemplatePacks/{resourceFolder}/{path}");
                if (content is null) return null;
                templates[id] = content;
            }
            return new PackSource(resourceFolder, manifestText, templates.ToImmutable());
        }
    }

    private static string? ReadResource(Assembly assembly, string logicalName)
    {
        using var stream = assembly.GetManifestResourceStream(logicalName);
        if (stream is null) return null;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
