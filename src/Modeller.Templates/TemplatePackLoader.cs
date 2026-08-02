using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Modeller.Templates;

/// <summary>A renderer's id and version travel together everywhere they're compared or recorded — never as two independent strings.</summary>
public readonly record struct RendererIdentity(string Id, string Version);

public sealed record PackSource(string Name, string Manifest, ImmutableDictionary<string, string> Templates);
public sealed record PackLoadRequest(PackSource Source, ImmutableArray<string> GenerationContractVersions, ImmutableArray<RendererIdentity> Renderers);
public sealed record ValidatedPackOutput(string Id, string Scope, string LogicalPathPattern, string Owner, string TemplateId,
    string TemplateDigest);
public sealed record ValidatedTemplatePack(string Id, string PackVersion, string GenerationContractVersion, RendererIdentity Renderer,
    string Language, ImmutableArray<ValidatedPackOutput> Outputs, ImmutableDictionary<string, string> Templates, string Digest);
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
        if (manifest is null || Blank(manifest.Id) || Blank(manifest.PackVersion)) return Failure("template-pack.identity.required", "Template-pack identity and version are required.");
        if (manifest.Version != "1.0") return Failure("template-pack.schema-version.unsupported", "The template-pack manifest schema version is unsupported.");
        if (Blank(manifest.Language)) return Failure("template-pack.language.required", "Template-pack language is required.");
        if (!request.GenerationContractVersions.Contains(manifest.GenerationContractVersion, StringComparer.Ordinal))
            return Failure("template-pack.generation-contract.incompatible", "The template pack requires an unsupported generation contract.");
        var renderer = new RendererIdentity(manifest.RendererId, manifest.RendererVersion);
        if (!request.Renderers.Contains(renderer))
            return Failure("template-pack.renderer.incompatible", "The template pack requires an unsupported renderer.");
        var outputs = ImmutableArray.CreateBuilder<ValidatedPackOutput>();
        foreach (var output in (manifest.Outputs ?? []).OrderBy(x => x.Id, StringComparer.Ordinal))
        {
            if (!Scopes.Contains(output.Scope) || !SafePath(output.LogicalPath) || !SafePath(output.TemplateId)) return Failure("template-pack.output.invalid", "Template-pack output recipes require a supported scope and confined paths.");
            if (!request.Source.Templates.TryGetValue(output.TemplateId, out var content)) return Failure("template-pack.template.missing", "A declared template was not supplied.");
            outputs.Add(new(output.Id, output.Scope, output.LogicalPath, output.Owner, output.TemplateId, Digest(content)));
        }
        if (outputs.GroupBy(x => x.Id, StringComparer.Ordinal).Any(x => x.Count() > 1))
            return Failure("template-pack.output.duplicate", "Template-pack output recipe identities must be unique.");
        var canonical = JsonSerializer.Serialize(new { manifest.Id, manifest.PackVersion, manifest.GenerationContractVersion,
            manifest.RendererId, manifest.RendererVersion, manifest.Language, Outputs = outputs.ToImmutable() }, JsonOptions);
        return new(new(manifest.Id, manifest.PackVersion, manifest.GenerationContractVersion, renderer,
            manifest.Language, outputs.ToImmutable(), request.Source.Templates, Digest(canonical)), []);
    }

    private static bool Blank(string value) => string.IsNullOrWhiteSpace(value);
    private static bool SafePath(string value) => !string.IsNullOrWhiteSpace(value) && !Path.IsPathRooted(value) &&
        !value.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries).Contains("..", StringComparer.Ordinal);
    private static string Digest(string value) => $"sha256:{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)))}";
    private static TemplatePackResult Failure(string code, string message) => new(null, [new(code, message)]);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly ImmutableHashSet<string> Scopes = ["context", "entity", "enumeration", "rule", "behaviour"];
    private sealed record Manifest(string Version, string Id, string PackVersion, string GenerationContractVersion, string RendererId, string RendererVersion, string Language, ManifestOutput[]? Outputs);
    private sealed record ManifestOutput(string Id, string Scope, string LogicalPath, string Owner, string TemplateId);
}
