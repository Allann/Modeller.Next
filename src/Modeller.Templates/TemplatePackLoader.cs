using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Modeller.Templates;

public sealed record PackSource(string Name, string Manifest, ImmutableDictionary<string, string> Templates);
public sealed record RendererSupport(string Id, string Version);
public sealed record PackLoadRequest(PackSource Source, ImmutableArray<string> GenerationContractVersions, ImmutableArray<RendererSupport> Renderers);
public sealed record ValidatedPackArtifact(string ArtifactId, string LogicalPath, string Owner, string TemplateId,
    string TemplateDigest, ImmutableArray<string> SemanticInputIds);
public sealed record ValidatedTemplatePack(string Id, string Version, string GenerationContractVersion, string RendererId,
    string RendererVersion, ImmutableArray<ValidatedPackArtifact> Artifacts, ImmutableDictionary<string, string> Templates, string Digest);
public sealed record TemplatePackDiagnostic(string Code, string Message);
public sealed record TemplatePackResult(ValidatedTemplatePack? Pack, ImmutableArray<TemplatePackDiagnostic> Diagnostics)
{ public bool IsSuccess => Pack is not null && Diagnostics.IsEmpty; }

public static class TemplatePackLoader
{
    public static TemplatePackResult Load(PackLoadRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (cancellationToken.IsCancellationRequested) return Failure("template-pack.cancelled", "Template-pack loading was cancelled.");
        Manifest? manifest;
        try { manifest = JsonSerializer.Deserialize<Manifest>(request.Source.Manifest, JsonOptions); }
        catch (JsonException) { return Failure("template-pack.manifest.invalid", "The template-pack manifest is invalid."); }
        if (manifest is null || Blank(manifest.Id) || Blank(manifest.Version)) return Failure("template-pack.identity.required", "Template-pack identity and version are required.");
        if (!request.GenerationContractVersions.Contains(manifest.GenerationContractVersion, StringComparer.Ordinal))
            return Failure("template-pack.generation-contract.incompatible", "The template pack requires an unsupported generation contract.");
        if (!request.Renderers.Contains(new(manifest.RendererId, manifest.RendererVersion)))
            return Failure("template-pack.renderer.incompatible", "The template pack requires an unsupported renderer.");
        var artifacts = ImmutableArray.CreateBuilder<ValidatedPackArtifact>();
        foreach (var artifact in (manifest.Artifacts ?? []).OrderBy(x => x.Id, StringComparer.Ordinal))
        {
            if (!SafePath(artifact.Path) || !SafePath(artifact.Template)) return Failure("template-pack.path.invalid", "Template-pack paths must remain relative and confined.");
            if (!request.Source.Templates.TryGetValue(artifact.Template, out var content)) return Failure("template-pack.template.missing", "A declared template was not supplied.");
            artifacts.Add(new(artifact.Id, artifact.Path, artifact.Owner, artifact.Template, Digest(content),
                (artifact.SemanticInputIds ?? []).Order(StringComparer.Ordinal).ToImmutableArray()));
        }
        if (artifacts.GroupBy(x => x.LogicalPath, StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1))
            return Failure("template-pack.path.duplicate", "Template-pack artifacts claim the same logical path.");
        var canonical = JsonSerializer.Serialize(new { manifest.Id, manifest.Version, manifest.GenerationContractVersion,
            manifest.RendererId, manifest.RendererVersion, Artifacts = artifacts.ToImmutable() }, JsonOptions);
        return new(new(manifest.Id, manifest.Version, manifest.GenerationContractVersion, manifest.RendererId,
            manifest.RendererVersion, artifacts.ToImmutable(), request.Source.Templates, Digest(canonical)), []);
    }

    private static bool Blank(string value) => string.IsNullOrWhiteSpace(value);
    private static bool SafePath(string value) => !string.IsNullOrWhiteSpace(value) && !Path.IsPathRooted(value) &&
        !value.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries).Contains("..", StringComparer.Ordinal);
    private static string Digest(string value) => $"sha256:{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)))}";
    private static TemplatePackResult Failure(string code, string message) => new(null, [new(code, message)]);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private sealed record Manifest(string Id, string Version, string GenerationContractVersion, string RendererId, string RendererVersion, ManifestArtifact[]? Artifacts);
    private sealed record ManifestArtifact(string Id, string Path, string Owner, string Template, string[]? SemanticInputIds);
}
