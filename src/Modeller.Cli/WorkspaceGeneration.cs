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
using Modeller.Parsing;
using Modeller.Rendering;
using Modeller.Templates;
using Modeller.Model;

namespace Modeller.Cli;

internal static class WorkspaceGeneration
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true, WriteIndented = true };

    public static async ValueTask<CliExitCode> ExecuteAsync(string workspace, bool dryRun, bool machine, ICliHost host, CancellationToken cancellationToken)
    {
        var root = Normalize(workspace);
        if (root is null) return await Failure(host, machine, "workspace.path.invalid", "The workspace path must be relative and confined.");

        var configured = await LoadConfigurationAsync(root, host, machine, cancellationToken);
        if (configured.Error is not null) return configured.Error.Value;
        var (configuration, configurationText, runtimeConfiguration) = configured.Value!;

        var identityRegistry = await LoadIdentityRegistryAsync(root, configuration, host, machine, cancellationToken);
        if (identityRegistry.Error is not null) return identityRegistry.Error.Value;

        var documents = await LoadSourceDocumentsAsync(root, configuration, identityRegistry.Value!, host, machine, cancellationToken);
        if (documents.Error is not null) return documents.Error.Value;

        var parsed = DefinitionParser.Parse(documents.Value, ParseOptions.Language1, cancellationToken);
        if (parsed.IsCancelled) return CliExitCode.Cancelled;
        if (!parsed.IsSuccess)
            return await Failure(host, machine, parsed.Diagnostics[0].Code, parsed.Diagnostics[0].Message);
        var package = parsed.Package!;

        var pack = await LoadTemplatePackAsync(root, configuration, runtimeConfiguration, host, machine, cancellationToken);
        if (pack.Error is not null) return pack.Error.Value;
        var (validated, templates, capability) = pack.Value!;

        var languageParameters = ResolveLanguageParameters(configuration, capability, out var parametersFailure);
        if (parametersFailure is not null)
            return await Failure(host, machine, parametersFailure.Value.Code, parametersFailure.Value.Message);

        var descriptors = BuildTemplateDescriptors(package.AuthoredRevision, validated, templates, capability, configuration);
        if (descriptors.Error is not null)
            return await Failure(host, machine, descriptors.Error.Value.Code, descriptors.Error.Value.Message);

        var planning = BuildPlanningRequest(package, validated, descriptors.Value, configuration, configurationText, runtimeConfiguration);

        var ownership = await LoadOwnershipManifestAsync(root, configuration, host, machine, cancellationToken);
        if (ownership.Error is not null) return ownership.Error.Value;
        var (manifest, manifestText) = ownership.Value!;

        var globalsProvider = capability.CreateGlobalsProvider(package.AuthoredRevision, configuration.Parameters.ProjectName, languageParameters!);
        var scriban = new ScribanRendererAdapter(capability.Renderer.Id, capability.Renderer.Version, templates, globalsProvider: globalsProvider);
        var outputRoot = Join(root, runtimeConfiguration.LogicalOutputRoot);
        var execution = await GenerationExecution.ExecuteAsync(new(planning, manifest, dryRun ? OutputMode.Preview : OutputMode.Apply),
            scriban, new CliOutputFileSystem(host, outputRoot), cancellationToken);
        if (execution.Output is null)
            return await Failure(host, machine, execution.Diagnostics.FirstOrDefault() ?? "workspace.generation.failed", "Workspace generation failed.");

        return await EmitResultAsync(root, dryRun, machine, host, execution.Output, manifestText, Join(root, configuration.OwnershipManifest), cancellationToken);
    }

    private static async ValueTask<Outcome<(WorkspaceConfiguration Configuration, string ConfigurationText, RuntimeConfiguration Runtime)>> LoadConfigurationAsync(
        string root, ICliHost host, bool machine, CancellationToken cancellationToken)
    {
        var configPath = Join(root, ".modeller/config.json");
        if (!host.Exists(configPath))
            return await Outcome.FailAsync<(WorkspaceConfiguration, string, RuntimeConfiguration)>(host, machine, "workspace.configuration.missing", "The workspace configuration could not be read.");

        WorkspaceConfiguration? configuration;
        string configurationText;
        try
        {
            configurationText = await host.ReadTextAsync(configPath, cancellationToken);
            configuration = JsonSerializer.Deserialize<WorkspaceConfiguration>(configurationText, Json);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return new(CliExitCode.Cancelled, default); }
        catch (Exception exception) when (exception is IOException or JsonException or NotSupportedException)
        { return await Outcome.FailAsync<(WorkspaceConfiguration, string, RuntimeConfiguration)>(host, machine, "workspace.configuration.invalid", "The workspace configuration is invalid."); }

        if (!IsValidConfiguration(configuration))
            return await Outcome.FailAsync<(WorkspaceConfiguration, string, RuntimeConfiguration)>(host, machine, "workspace.configuration.invalid", "The workspace configuration is invalid.");

        var resolvedConfiguration = ConfigurationResolver.Resolve(new ConfigurationRequest([
            new ConfigurationSource("workspace", configuration!.Version, ConfigurationSourceKind.Base, null,
                new Dictionary<string, ConfigurationValue>(StringComparer.Ordinal)
                {
                    ["generationContractVersion"] = new(configuration.GenerationContractVersion),
                    ["logicalOutputRoot"] = new(configuration.LogicalOutputRoot)
                }.ToImmutableDictionary(StringComparer.Ordinal))
        ], configuration.Profile), cancellationToken);
        if (!resolvedConfiguration.IsSuccess)
            return await Outcome.FailAsync<(WorkspaceConfiguration, string, RuntimeConfiguration)>(host, machine, resolvedConfiguration.Diagnostics[0].Code, resolvedConfiguration.Diagnostics[0].Message);

        return new(null, (configuration, configurationText, resolvedConfiguration.Configuration!));
    }

    private static bool IsValidConfiguration(WorkspaceConfiguration? configuration) =>
        configuration is not null && configuration.Version == "1.0" && HasSources(configuration) && HasValidParameters(configuration) &&
        !Unsafe(configuration.LogicalOutputRoot) && !Unsafe(configuration.TemplatePack) && !Unsafe(configuration.OwnershipManifest);

    private static bool HasSources(WorkspaceConfiguration configuration) => configuration.Sources is { Count: > 0 };

    private static bool HasValidParameters(WorkspaceConfiguration configuration) =>
        configuration.Parameters is not null && !string.IsNullOrWhiteSpace(configuration.Parameters.ProjectName);

    private static async ValueTask<Outcome<IdentityRegistry>> LoadIdentityRegistryAsync(
        string root, WorkspaceConfiguration configuration, ICliHost host, bool machine, CancellationToken cancellationToken)
    {
        if (Unsafe(configuration.IdentityRegistry))
            return await Outcome.FailAsync<IdentityRegistry>(host, machine, "workspace.identity-registry.path-invalid", "The identity-registry path is unsafe.");
        var identityPath = Join(root, configuration.IdentityRegistry);
        if (!host.Exists(identityPath))
            return await Outcome.FailAsync<IdentityRegistry>(host, machine, "workspace.identity-registry.missing", "The tooling-owned identity registry could not be read.");

        IdentityRegistry? identityRegistry;
        try { identityRegistry = JsonSerializer.Deserialize<IdentityRegistry>(await host.ReadTextAsync(identityPath, cancellationToken), Json); }
        catch (Exception exception) when (exception is IOException or JsonException or NotSupportedException)
        { return await Outcome.FailAsync<IdentityRegistry>(host, machine, "workspace.identity-registry.invalid", "The tooling-owned identity registry is invalid."); }
        if (identityRegistry is null || identityRegistry.Version != "1.0" || identityRegistry.Documents is null)
            return await Outcome.FailAsync<IdentityRegistry>(host, machine, "workspace.identity-registry.invalid", "The tooling-owned identity registry is invalid.");

        return new(null, identityRegistry);
    }

    private static async ValueTask<Outcome<ImmutableArray<SourceDocument>>> LoadSourceDocumentsAsync(
        string root, WorkspaceConfiguration configuration, IdentityRegistry identityRegistry, ICliHost host, bool machine, CancellationToken cancellationToken)
    {
        var documents = ImmutableArray.CreateBuilder<SourceDocument>();
        foreach (var declared in configuration.Sources.Order(StringComparer.Ordinal))
        {
            if (Unsafe(declared))
                return await Outcome.FailAsync<ImmutableArray<SourceDocument>>(host, machine, "workspace.source.path-invalid", "A declared source path is unsafe.");
            var path = Join(root, declared);
            if (!host.Exists(path))
                return await Outcome.FailAsync<ImmutableArray<SourceDocument>>(host, machine, "workspace.source.missing", "A declared source could not be read.");
            var documentName = declared.Replace('\\', '/');
            if (!identityRegistry.Documents.TryGetValue(documentName, out var identities))
                return await Outcome.FailAsync<ImmutableArray<SourceDocument>>(host, machine, "workspace.identity-registry.document-missing", $"The identity registry does not cover '{documentName}'.");
            try
            {
                var source = await host.ReadTextAsync(path, cancellationToken);
                documents.Add(new(documentName, RmlCompiler.ApplyIdentities(source, identities).Updated));
            }
            catch (ArgumentException)
            { return await Outcome.FailAsync<ImmutableArray<SourceDocument>>(host, machine, "workspace.identity-registry.out-of-sync", $"The identity registry is out of sync with '{documentName}'."); }
        }
        return new(null, documents.ToImmutable());
    }

    private static async ValueTask<Outcome<(ValidatedTemplatePack Validated, ImmutableDictionary<string, ScribanTemplateSource> Templates, IRendererCapability Capability)>> LoadTemplatePackAsync(
        string root, WorkspaceConfiguration configuration, RuntimeConfiguration runtimeConfiguration, ICliHost host, bool machine, CancellationToken cancellationToken)
    {
        var packPath = Join(root, configuration.TemplatePack);
        if (!host.Exists(packPath))
            return await Outcome.FailAsync<(ValidatedTemplatePack, ImmutableDictionary<string, ScribanTemplateSource>, IRendererCapability)>(host, machine, "workspace.template-pack.missing", "The declared template pack could not be read.");

        TemplatePinningManifest? pinning;
        string packText;
        try
        {
            packText = await host.ReadTextAsync(packPath, cancellationToken);
            pinning = JsonSerializer.Deserialize<TemplatePinningManifest>(packText, Json);
        }
        catch (Exception exception) when (exception is IOException or JsonException or NotSupportedException)
        { return await Outcome.FailAsync<(ValidatedTemplatePack, ImmutableDictionary<string, ScribanTemplateSource>, IRendererCapability)>(host, machine, "workspace.template-pack.invalid", "The declared template pack is invalid."); }
        if (!HasUniqueTemplateIds(pinning))
            return await Outcome.FailAsync<(ValidatedTemplatePack, ImmutableDictionary<string, ScribanTemplateSource>, IRendererCapability)>(host, machine, "workspace.template-pack.invalid", "The declared template pack is invalid.");

        var packDirectory = Path.GetDirectoryName(configuration.TemplatePack.Replace('/', Path.DirectorySeparatorChar))?.Replace('\\', '/') ?? "";
        var templates = ImmutableDictionary.CreateBuilder<string, ScribanTemplateSource>(StringComparer.Ordinal);
        foreach (var declared in pinning!.Templates.OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            if (Unsafe(declared.Path) || !IsSha256(declared.Digest))
                return await Outcome.FailAsync<(ValidatedTemplatePack, ImmutableDictionary<string, ScribanTemplateSource>, IRendererCapability)>(host, machine, "workspace.template.invalid", "A declared template is invalid.");
            var path = Join(root, Join(packDirectory, declared.Path));
            if (!host.Exists(path))
                return await Outcome.FailAsync<(ValidatedTemplatePack, ImmutableDictionary<string, ScribanTemplateSource>, IRendererCapability)>(host, machine, "workspace.template.missing", "A declared template could not be read.");
            var content = await host.ReadTextAsync(path, cancellationToken);
            if (!StringComparer.Ordinal.Equals(Digest(content), declared.Digest))
                return await Outcome.FailAsync<(ValidatedTemplatePack, ImmutableDictionary<string, ScribanTemplateSource>, IRendererCapability)>(host, machine, "workspace.template.digest-mismatch", $"Template '{declared.Id}' does not match its pinned digest.");
            templates.Add(declared.Id, new(declared.Digest, content));
        }

        var packSource = new PackSource(
            Path.GetFileNameWithoutExtension(configuration.TemplatePack),
            packText,
            templates.ToImmutable().ToImmutableDictionary(item => item.Key, item => item.Value.Content, StringComparer.Ordinal));
        var loaded = TemplatePackLoader.Load(new PackLoadRequest(packSource,
            [runtimeConfiguration.GenerationContractVersion], RendererCapabilityRegistry.SupportedRenderers), cancellationToken);
        if (!loaded.IsSuccess)
            return await Outcome.FailAsync<(ValidatedTemplatePack, ImmutableDictionary<string, ScribanTemplateSource>, IRendererCapability)>(host, machine, loaded.Diagnostics[0].Code, loaded.Diagnostics[0].Message);
        var validated = loaded.Pack!;

        var capability = RendererCapabilityRegistry.Resolve(validated.Renderer, validated.Language);
        if (capability is null)
            return await Outcome.FailAsync<(ValidatedTemplatePack, ImmutableDictionary<string, ScribanTemplateSource>, IRendererCapability)>(host, machine, "workspace.template-pack.renderer-unsupported", "The declared template pack targets an unsupported renderer/language combination.");

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
    private static IReadOnlyDictionary<string, string>? ResolveLanguageParameters(WorkspaceConfiguration configuration, IRendererCapability capability, out Diagnostic? failure)
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

    private static IReadOnlyDictionary<string, string>? ExtractLanguageParameters(PackParameters parameters, string language)
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
        IRendererCapability capability, WorkspaceConfiguration configuration)
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
                if (Unsafe(logicalPath))
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
        WorkspaceConfiguration configuration, string configurationText, RuntimeConfiguration runtimeConfiguration)
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

    private static async ValueTask<Outcome<(OwnershipManifest Manifest, string? ManifestText)>> LoadOwnershipManifestAsync(
        string root, WorkspaceConfiguration configuration, ICliHost host, bool machine, CancellationToken cancellationToken)
    {
        var manifestPath = Join(root, configuration.OwnershipManifest);
        if (!host.Exists(manifestPath)) return new(null, (OwnershipManifest.Empty, null));
        try
        {
            var manifestText = await host.ReadTextAsync(manifestPath, cancellationToken);
            var manifest = JsonSerializer.Deserialize<OwnershipManifest>(manifestText, Json) ?? OwnershipManifest.Empty;
            return new(null, (manifest, manifestText));
        }
        catch (Exception exception) when (exception is IOException or JsonException or NotSupportedException)
        { return await Outcome.FailAsync<(OwnershipManifest, string?)>(host, machine, "workspace.manifest.invalid", "The ownership manifest is invalid."); }
    }

    private static async ValueTask<CliExitCode> EmitResultAsync(
        string root, bool dryRun, bool machine, ICliHost host, OutputReport output, string? manifestText, string manifestPath, CancellationToken cancellationToken)
    {
        if (!dryRun && output.IsSuccess)
        {
            var nextManifest = SerializeManifest(output.Manifest);
            if (!StringComparer.Ordinal.Equals(manifestText, nextManifest))
                await host.WriteTextAsync(manifestPath, nextManifest, true, cancellationToken);
        }
        if (machine)
            await host.Output.WriteLineAsync(JsonSerializer.Serialize(new { outputVersion = "1.0", workspace = root, dryRun,
                changes = output.Changes.Select(change => new { change.Path, status = change.Status.ToString().ToLowerInvariant(), change.ArtifactId }),
                diagnostics = output.Diagnostics, manifest = output.Manifest }, Json));
        else
            foreach (var change in output.Changes) await host.Output.WriteLineAsync($"{change.Status}: {change.Path}");
        return output.IsSuccess ? CliExitCode.Success : CliExitCode.Configuration;
    }

    private static async ValueTask<CliExitCode> Failure(ICliHost host, bool machine, string code, string message)
    {
        if (machine) await host.Output.WriteLineAsync(JsonSerializer.Serialize(new { outputVersion = "1.0", diagnostics = new[] { new { code, message } } }, Json));
        else await host.Error.WriteLineAsync($"{code}: {message}");
        return CliExitCode.Configuration;
    }

    private static string Join(string left, string right) => string.IsNullOrEmpty(left) ? right.Replace('\\', '/') : $"{left.TrimEnd('/', '\\')}/{right.Replace('\\', '/')}";
    private static string? Normalize(string path) => Unsafe(path) ? null : path.Replace('\\', '/').TrimEnd('/');
    private static bool Unsafe(string path) => string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries).Contains("..", StringComparer.Ordinal) || path.Contains('\0');
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

    /// <summary>A stage's outcome: either a <see cref="CliExitCode"/> already reported to the host, or a produced value.</summary>
    private readonly record struct Outcome<T>(CliExitCode? Error, T Value);
    /// <summary>A synchronous stage's outcome: either an unreported <see cref="Diagnostic"/> for the caller to report, or a produced value.</summary>
    private readonly record struct Pending<T>(Diagnostic? Error, T Value);
    private readonly record struct Diagnostic(string Code, string Message);

    private static class Outcome
    {
        public static async ValueTask<Outcome<T>> FailAsync<T>(ICliHost host, bool machine, string code, string message) =>
            new(await Failure(host, machine, code, message), default!);
    }

    private sealed record WorkspaceConfiguration(string Version, string GenerationContractVersion, string LogicalOutputRoot, string Profile,
        IReadOnlyList<string> Sources, string TemplatePack, PackParameters Parameters,
        string IdentityRegistry = ".modeller/identities.json", string OwnershipManifest = ".modeller/generated-manifest.json");
    private sealed record IdentityRegistry(string Version, IReadOnlyDictionary<string, IReadOnlyList<string>> Documents);

    /// <summary>
    /// <c>projectName</c> plus an open-ended set of language-keyed parameter blocks (e.g. <c>csharp</c>,
    /// <c>python</c>). Unrecognized top-level properties fall into <see cref="Languages"/> rather than requiring
    /// a dedicated record per language.
    /// </summary>
    private sealed record PackParameters(string ProjectName)
    {
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Languages { get; init; }
    }

    private sealed record TemplatePinningManifest(IReadOnlyList<TemplateFile> Templates);
    private sealed record TemplateFile(string Id, string Path, string Digest);
}
