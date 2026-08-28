using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Modeller.Contexts;
using Modeller.Configuration;
using Modeller.Generation;
using Modeller.GenerationWorkflow;
using Modeller.Output;
using Modeller.Rendering;
using Modeller.Templates;
using Modeller.Model;

namespace Modeller.Cli;

internal static class WorkspaceGeneration
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true, WriteIndented = true };

    public static async ValueTask<CliExitCode> ExecuteAsync(string workspace, bool dryRun, bool machine, ICliHost host, CancellationToken cancellationToken)
    {
        var loaded = await WorkspaceLoader.LoadAsync(workspace, host, machine, cancellationToken);
        if (loaded.Error is not null) return loaded.Error.Value;
        var (root, package, configuration, configurationText, runtimeConfiguration) = loaded.Value;

        var pack = await LoadTemplatePackAsync(root, configuration, runtimeConfiguration, host, machine, cancellationToken);
        if (pack.Error is not null) return pack.Error.Value;
        var (validated, templates, capability) = pack.Value!;

        var languageParameters = ResolveLanguageParameters(configuration, capability, out var parametersFailure);
        if (parametersFailure is not null)
            return await WorkspaceLoader.Failure(host, machine, parametersFailure.Value.Code, parametersFailure.Value.Message);

        var descriptors = BuildTemplateDescriptors(package.AuthoredRevision, validated, templates, capability, configuration);
        if (descriptors.Error is not null)
            return await WorkspaceLoader.Failure(host, machine, descriptors.Error.Value.Code, descriptors.Error.Value.Message);

        var planning = BuildPlanningRequest(package, validated, descriptors.Value, configuration, configurationText, runtimeConfiguration);

        var ownership = await LoadOwnershipManifestAsync(root, configuration, host, machine, cancellationToken);
        if (ownership.Error is not null) return ownership.Error.Value;
        var (manifest, manifestText) = ownership.Value!;

        var globalsProvider = capability.CreateGlobalsProvider(package.AuthoredRevision, configuration.Parameters.ProjectName, languageParameters!);
        var scriban = new ScribanRendererAdapter(capability.Renderer.Id, capability.Renderer.Version, templates, globalsProvider: globalsProvider);
        var outputRoot = WorkspaceLoader.Join(root, runtimeConfiguration.LogicalOutputRoot);
        var execution = await GenerationExecution.ExecuteAsync(new(planning, manifest, dryRun ? OutputMode.Preview : OutputMode.Apply),
            scriban, new CliOutputFileSystem(host, outputRoot), cancellationToken);
        if (execution.Output is null)
            return await WorkspaceLoader.Failure(host, machine, execution.Diagnostics.FirstOrDefault() ?? "workspace.generation.failed", "Workspace generation failed.");

        return await EmitResultAsync(root, dryRun, machine, host, execution.Plan!, execution.Rendering!, execution.Output, manifestText, WorkspaceLoader.Join(root, configuration.OwnershipManifest), cancellationToken);
    }

    private static async ValueTask<WorkspaceLoader.Outcome<(ValidatedTemplatePack Validated, ImmutableDictionary<string, ScribanTemplateSource> Templates, IRendererCapability Capability)>> LoadTemplatePackAsync(
        string root, WorkspaceLoader.WorkspaceConfiguration configuration, RuntimeConfiguration runtimeConfiguration, ICliHost host, bool machine, CancellationToken cancellationToken)
    {
        var packPath = WorkspaceLoader.Join(root, configuration.TemplatePack);
        if (!host.Exists(packPath))
            return await WorkspaceLoader.Outcome.FailAsync<(ValidatedTemplatePack, ImmutableDictionary<string, ScribanTemplateSource>, IRendererCapability)>(host, machine, "workspace.template-pack.missing", "The declared template pack could not be read.");

        TemplatePinningManifest? pinning;
        string packText;
        try
        {
            packText = await host.ReadTextAsync(packPath, cancellationToken);
            pinning = JsonSerializer.Deserialize<TemplatePinningManifest>(packText, Json);
        }
        catch (Exception exception) when (exception is IOException or JsonException or NotSupportedException)
        { return await WorkspaceLoader.Outcome.FailAsync<(ValidatedTemplatePack, ImmutableDictionary<string, ScribanTemplateSource>, IRendererCapability)>(host, machine, "workspace.template-pack.invalid", "The declared template pack is invalid."); }
        if (!HasUniqueTemplateIds(pinning))
            return await WorkspaceLoader.Outcome.FailAsync<(ValidatedTemplatePack, ImmutableDictionary<string, ScribanTemplateSource>, IRendererCapability)>(host, machine, "workspace.template-pack.invalid", "The declared template pack is invalid.");

        var packDirectory = Path.GetDirectoryName(configuration.TemplatePack.Replace('/', Path.DirectorySeparatorChar))?.Replace('\\', '/') ?? "";
        var templates = ImmutableDictionary.CreateBuilder<string, ScribanTemplateSource>(StringComparer.Ordinal);
        foreach (var declared in pinning!.Templates.OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            if (WorkspaceLoader.Unsafe(declared.Path) || !IsSha256(declared.Digest))
                return await WorkspaceLoader.Outcome.FailAsync<(ValidatedTemplatePack, ImmutableDictionary<string, ScribanTemplateSource>, IRendererCapability)>(host, machine, "workspace.template.invalid", "A declared template is invalid.");
            var path = WorkspaceLoader.Join(root, WorkspaceLoader.Join(packDirectory, declared.Path));
            if (!host.Exists(path))
                return await WorkspaceLoader.Outcome.FailAsync<(ValidatedTemplatePack, ImmutableDictionary<string, ScribanTemplateSource>, IRendererCapability)>(host, machine, "workspace.template.missing", "A declared template could not be read.");
            var content = await host.ReadTextAsync(path, cancellationToken);
            if (!StringComparer.Ordinal.Equals(Digest(content), declared.Digest))
                return await WorkspaceLoader.Outcome.FailAsync<(ValidatedTemplatePack, ImmutableDictionary<string, ScribanTemplateSource>, IRendererCapability)>(host, machine, "workspace.template.digest-mismatch", $"Template '{declared.Id}' does not match its pinned digest.");
            templates.Add(declared.Id, new(declared.Digest, content));
        }

        var packSource = new PackSource(
            Path.GetFileNameWithoutExtension(configuration.TemplatePack),
            packText,
            templates.ToImmutable().ToImmutableDictionary(item => item.Key, item => item.Value.Content, StringComparer.Ordinal));
        var loaded = TemplatePackLoader.Load(new PackLoadRequest(packSource,
            [runtimeConfiguration.GenerationContractVersion], RendererCapabilityRegistry.SupportedRenderers), cancellationToken);
        if (!loaded.IsSuccess)
            return await WorkspaceLoader.Outcome.FailAsync<(ValidatedTemplatePack, ImmutableDictionary<string, ScribanTemplateSource>, IRendererCapability)>(host, machine, loaded.Diagnostics[0].Code, loaded.Diagnostics[0].Message);
        var validated = loaded.Pack!;

        var capability = RendererCapabilityRegistry.Resolve(validated.Renderer, validated.Language);
        if (capability is null)
            return await WorkspaceLoader.Outcome.FailAsync<(ValidatedTemplatePack, ImmutableDictionary<string, ScribanTemplateSource>, IRendererCapability)>(host, machine, "workspace.template-pack.renderer-unsupported", "The declared template pack targets an unsupported renderer/language combination.");

        return new(null, (validated, templates.ToImmutable(), capability));
    }

    private static bool HasUniqueTemplateIds(TemplatePinningManifest? pinning) =>
        pinning?.Templates is { Count: > 0 } templates && templates.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() == templates.Count;

    /// <summary>
    /// Extracts the pack-declared language's own parameter block from the language-neutral configuration bag.
    /// Adding a renderer capability for a new language never requires a new CLI switch or configuration DTO here —
    /// the capability's own <see cref="IRendererCapability.Language"/> is the only key used to look up its block,
    /// and the capability itself validates the shape of its own parameters.
    /// </summary>
    private static IReadOnlyDictionary<string, string>? ResolveLanguageParameters(WorkspaceLoader.WorkspaceConfiguration configuration, IRendererCapability capability, out Diagnostic? failure)
    {
        var languageParameters = ExtractLanguageParameters(configuration.Parameters, capability.Language);
        if (languageParameters is null)
        {
            failure = new("workspace.configuration.parameters-invalid", $"The workspace configuration does not declare '{capability.Language}' parameters for the selected template pack.");
            return null;
        }
        if (!capability.TryValidateParameters(languageParameters, out var parametersDiagnosticCode))
        {
            failure = new(parametersDiagnosticCode!, "The workspace configuration parameters are invalid for the selected template pack.");
            return null;
        }
        failure = null;
        return languageParameters;
    }

    private static IReadOnlyDictionary<string, string>? ExtractLanguageParameters(WorkspaceLoader.PackParameters parameters, string language)
    {
        if (parameters.Languages is null || !parameters.Languages.TryGetValue(language, out var block) || block.ValueKind != JsonValueKind.Object)
            return null;
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in block.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.String) return null;
            result[property.Name] = property.Value.GetString()!;
        }
        return result;
    }

    private static Pending<ImmutableArray<TemplateArtifactDescriptor>> BuildTemplateDescriptors(
        AuthoredContextRevision revision, ValidatedTemplatePack validated, ImmutableDictionary<string, ScribanTemplateSource> templates,
        IRendererCapability capability, WorkspaceLoader.WorkspaceConfiguration configuration)
    {
        var templateDescriptors = ImmutableArray.CreateBuilder<TemplateArtifactDescriptor>();
        foreach (var recipe in validated.Outputs)
        {
            if (!templates.TryGetValue(recipe.TemplateId, out var template))
                return new(new("workspace.output.invalid", "A template-pack output recipe is invalid."), default);
            var selected = Select(revision, recipe.Scope);
            if (selected is null)
                return new(new("workspace.output.scope-invalid", $"Output recipe '{recipe.Id}' uses an unsupported scope."), default);
            foreach (var definition in selected)
            {
                var name = definition is null ? configuration.Parameters.ProjectName : capability.NameForPath(definition.Name.Value);
                var logicalPath = recipe.LogicalPathPattern
                    .Replace("{projectName}", configuration.Parameters.ProjectName, StringComparison.Ordinal)
                    .Replace("{definitionName}", name, StringComparison.Ordinal);
                if (WorkspaceLoader.Unsafe(logicalPath))
                    return new(new("workspace.output.path-invalid", "An expanded template-pack output path is invalid."), default);
                var suffix = definition is null ? "context" : definition.Slug.Value;
                templateDescriptors.Add(new($"{recipe.Id}:{suffix}", recipe.TemplateId, logicalPath, recipe.Owner, template.Digest,
                    definition is null ? [] : [definition.Slug.Value]));
            }
        }
        return new(null, templateDescriptors.ToImmutable());
    }

    private static GenerationPlanningRequest BuildPlanningRequest(
        LoadedContextPackage package, ValidatedTemplatePack validated, ImmutableArray<TemplateArtifactDescriptor> descriptors,
        WorkspaceLoader.WorkspaceConfiguration configuration, string configurationText, RuntimeConfiguration runtimeConfiguration)
    {
        var contextId = package.AuthoredRevision.Id.ToString();
        var snapshot = new ResolvedGenerationSnapshot(
            new FederationSnapshot([new(contextId, package.AuthoredRevision.Slug.Value, package.AuthoredRevision.ContextVersion, package.PackageDigest, package.SemanticDigest)]),
            package.AuthoredRevision.Definitions.Select(definition => new GenerationSemanticInput(definition.Slug.Value, contextId, package.SemanticDigest)).ToImmutableArray());
        return new GenerationPlanningRequest(snapshot,
            new(configuration.Profile, runtimeConfiguration.GenerationContractVersion, runtimeConfiguration.LogicalOutputRoot, Digest(configurationText)),
            new ValidatedTemplatePackDescriptor(
                validated.Id, validated.PackVersion, validated.GenerationContractVersion, validated.Digest, descriptors,
                validated.Renderer, validated.Language));
    }

    private static async ValueTask<WorkspaceLoader.Outcome<(OwnershipManifest Manifest, string? ManifestText)>> LoadOwnershipManifestAsync(
        string root, WorkspaceLoader.WorkspaceConfiguration configuration, ICliHost host, bool machine, CancellationToken cancellationToken)
    {
        var manifestPath = WorkspaceLoader.Join(root, configuration.OwnershipManifest);
        if (!host.Exists(manifestPath)) return new(null, (OwnershipManifest.Empty, null));
        try
        {
            var manifestText = await host.ReadTextAsync(manifestPath, cancellationToken);
            var manifest = JsonSerializer.Deserialize<OwnershipManifest>(manifestText, Json) ?? OwnershipManifest.Empty;
            return new(null, (manifest, manifestText));
        }
        catch (Exception exception) when (exception is IOException or JsonException or NotSupportedException)
        { return await WorkspaceLoader.Outcome.FailAsync<(OwnershipManifest, string?)>(host, machine, "workspace.manifest.invalid", "The ownership manifest is invalid."); }
    }

    private static async ValueTask<CliExitCode> EmitResultAsync(
        string root, bool dryRun, bool machine, ICliHost host, GenerationPlan plan, RenderingResult rendering, OutputReport output,
        string? manifestText, string manifestPath, CancellationToken cancellationToken)
    {
        if (!dryRun && output.IsSuccess)
        {
            var nextManifest = SerializeManifest(output.Manifest);
            if (!StringComparer.Ordinal.Equals(manifestText, nextManifest))
                await host.WriteTextAsync(manifestPath, nextManifest, true, cancellationToken);
        }
        if (machine)
        {
            // Mirrors Modeller.Api's WorkspaceGenerationPreviewPipeline mapping (path/owner/packId/
            // templateId/content) so apps/studio's local Studio can shell out to this command and feed
            // the result straight into the same GenerationPreview UI the playground already uses.
            var ownerByOrdinal = plan.Artifacts.ToImmutableDictionary(artifact => artifact.Ordinal, artifact => artifact.Ownership.Owner);
            var artifacts = rendering.Artifacts.OrderBy(artifact => artifact.Ordinal).Select(artifact => new
            {
                path = artifact.LogicalPath,
                owner = ownerByOrdinal[artifact.Ordinal],
                packId = artifact.Provenance.PackId,
                templateId = artifact.Provenance.TemplateId,
                content = artifact.Content,
            });
            await host.Output.WriteLineAsync(JsonSerializer.Serialize(new { outputVersion = "1.0", workspace = root, dryRun,
                changes = output.Changes.Select(change => new { change.Path, status = change.Status.ToString().ToLowerInvariant(), change.ArtifactId }),
                artifacts, diagnostics = output.Diagnostics, manifest = output.Manifest }, Json));
        }
        else
            foreach (var change in output.Changes) await host.Output.WriteLineAsync($"{change.Status}: {change.Path}");
        return output.IsSuccess ? CliExitCode.Success : CliExitCode.Configuration;
    }

    private static bool IsSha256(string value) => value.Length == 71 && value.StartsWith("sha256:", StringComparison.Ordinal) && value[7..].All(Uri.IsHexDigit);
    private static string Digest(string content) => $"sha256:{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content)))}";
    private static IEnumerable<SemanticDefinition?>? Select(AuthoredContextRevision revision, string scope) => scope switch
    {
        "context" => new SemanticDefinition?[] { null },
        "entity" => revision.Definitions.OfType<EntityDefinition>().OrderBy(item => item.Slug.Value, StringComparer.Ordinal),
        "enumeration" => revision.Definitions.OfType<EnumerationDefinition>().OrderBy(item => item.Slug.Value, StringComparer.Ordinal),
        "rule" => revision.Definitions.OfType<RuleDefinition>().OrderBy(item => item.Slug.Value, StringComparer.Ordinal),
        "behaviour" => revision.Definitions.OfType<BehaviourDefinition>().OrderBy(item => item.Slug.Value, StringComparer.Ordinal),
        _ => null
    };
    private static string SerializeManifest(OwnershipManifest manifest) => JsonSerializer.Serialize(new
    {
        manifest.Version,
        Artifacts = manifest.Artifacts.OrderBy(item => item.Key, StringComparer.Ordinal)
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal)
    }, Json) + Environment.NewLine;

    /// <summary>A synchronous stage's outcome: either an unreported <see cref="Diagnostic"/> for the caller to report, or a produced value.</summary>
    private readonly record struct Pending<T>(Diagnostic? Error, T Value);
    private readonly record struct Diagnostic(string Code, string Message);

    private sealed record TemplatePinningManifest(IReadOnlyList<TemplateFile> Templates);
    private sealed record TemplateFile(string Id, string Path, string Digest);
}
