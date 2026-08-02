using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Modeller.Contexts;
using Modeller.Configuration;
using Modeller.Generation;
using Modeller.GenerationWorkflow;
using Modeller.Output;
using Modeller.Parsing;
using Modeller.Rendering;
using Modeller.Model;

namespace Modeller.Cli;

internal static class WorkspaceGeneration
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true, WriteIndented = true };

    public static async ValueTask<CliExitCode> ExecuteAsync(string workspace, bool dryRun, bool machine, ICliHost host, CancellationToken cancellationToken)
    {
        var root = Normalize(workspace);
        if (root is null) return await Failure(host, machine, "workspace.path.invalid", "The workspace path must be relative and confined.");
        var configPath = Join(root, ".modeller/config.json");
        if (!host.Exists(configPath)) return await Failure(host, machine, "workspace.configuration.missing", "The workspace configuration could not be read.");

        WorkspaceConfiguration? configuration;
        string configurationText;
        try
        {
            configurationText = await host.ReadTextAsync(configPath, cancellationToken);
            configuration = JsonSerializer.Deserialize<WorkspaceConfiguration>(configurationText, Json);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return CliExitCode.Cancelled; }
        catch (Exception exception) when (exception is IOException or JsonException or NotSupportedException)
        { return await Failure(host, machine, "workspace.configuration.invalid", "The workspace configuration is invalid."); }
        if (configuration is null || configuration.Version != "1.0" || configuration.Sources is null || configuration.Sources.Count == 0 ||
            configuration.Parameters is null || string.IsNullOrWhiteSpace(configuration.Parameters.ProjectName) ||
            string.IsNullOrWhiteSpace(configuration.Parameters.Namespace) || string.IsNullOrWhiteSpace(configuration.Parameters.TargetFramework) ||
            Unsafe(configuration.LogicalOutputRoot) || Unsafe(configuration.TemplatePack) || Unsafe(configuration.OwnershipManifest))
            return await Failure(host, machine, "workspace.configuration.invalid", "The workspace configuration is invalid.");
        var resolvedConfiguration = ConfigurationResolver.Resolve(new ConfigurationRequest([
            new ConfigurationSource("workspace", configuration.Version, ConfigurationSourceKind.Base, null,
                new Dictionary<string, ConfigurationValue>(StringComparer.Ordinal)
                {
                    ["generationContractVersion"] = new(configuration.GenerationContractVersion),
                    ["logicalOutputRoot"] = new(configuration.LogicalOutputRoot)
                }.ToImmutableDictionary(StringComparer.Ordinal))
        ], configuration.Profile), cancellationToken);
        if (!resolvedConfiguration.IsSuccess)
            return await Failure(host, machine, resolvedConfiguration.Diagnostics[0].Code, resolvedConfiguration.Diagnostics[0].Message);

        if (Unsafe(configuration.IdentityRegistry)) return await Failure(host, machine, "workspace.identity-registry.path-invalid", "The identity-registry path is unsafe.");
        var identityPath = Join(root, configuration.IdentityRegistry);
        if (!host.Exists(identityPath)) return await Failure(host, machine, "workspace.identity-registry.missing", "The tooling-owned identity registry could not be read.");
        IdentityRegistry? identityRegistry;
        try { identityRegistry = JsonSerializer.Deserialize<IdentityRegistry>(await host.ReadTextAsync(identityPath, cancellationToken), Json); }
        catch (Exception exception) when (exception is IOException or JsonException or NotSupportedException)
        { return await Failure(host, machine, "workspace.identity-registry.invalid", "The tooling-owned identity registry is invalid."); }
        if (identityRegistry is null || identityRegistry.Version != "1.0" || identityRegistry.Documents is null)
            return await Failure(host, machine, "workspace.identity-registry.invalid", "The tooling-owned identity registry is invalid.");

        var documents = ImmutableArray.CreateBuilder<SourceDocument>();
        foreach (var declared in configuration.Sources.Order(StringComparer.Ordinal))
        {
            if (Unsafe(declared)) return await Failure(host, machine, "workspace.source.path-invalid", "A declared source path is unsafe.");
            var path = Join(root, declared);
            if (!host.Exists(path)) return await Failure(host, machine, "workspace.source.missing", "A declared source could not be read.");
            var documentName = declared.Replace('\\', '/');
            if (!identityRegistry.Documents.TryGetValue(documentName, out var identities))
                return await Failure(host, machine, "workspace.identity-registry.document-missing", $"The identity registry does not cover '{documentName}'.");
            try
            {
                var source = await host.ReadTextAsync(path, cancellationToken);
                documents.Add(new(documentName, RmlCompiler.ApplyIdentities(source, identities).Updated));
            }
            catch (ArgumentException)
            { return await Failure(host, machine, "workspace.identity-registry.out-of-sync", $"The identity registry is out of sync with '{documentName}'."); }
        }

        var parsed = DefinitionParser.Parse(documents.ToImmutable(), ParseOptions.Language1, cancellationToken);
        if (parsed.IsCancelled) return CliExitCode.Cancelled;
        if (!parsed.IsSuccess)
            return await Failure(host, machine, parsed.Diagnostics[0].Code, parsed.Diagnostics[0].Message);

        var packPath = Join(root, configuration.TemplatePack);
        if (!host.Exists(packPath)) return await Failure(host, machine, "workspace.template-pack.missing", "The declared template pack could not be read.");
        TemplatePackFile? pack;
        string packText;
        try
        {
            packText = await host.ReadTextAsync(packPath, cancellationToken);
            pack = JsonSerializer.Deserialize<TemplatePackFile>(packText, Json);
        }
        catch (Exception exception) when (exception is IOException or JsonException or NotSupportedException)
        { return await Failure(host, machine, "workspace.template-pack.invalid", "The declared template pack is invalid."); }
        if (pack is null || pack.Version != "1.0" || pack.Templates is null || pack.Outputs is null || pack.Templates.Count == 0 || pack.Outputs.Count == 0 ||
            pack.Templates.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != pack.Templates.Count ||
            pack.Outputs.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != pack.Outputs.Count)
            return await Failure(host, machine, "workspace.template-pack.invalid", "The declared template pack is invalid.");

        var packDirectory = Path.GetDirectoryName(configuration.TemplatePack.Replace('/', Path.DirectorySeparatorChar))?.Replace('\\', '/') ?? "";
        var templates = ImmutableDictionary.CreateBuilder<string, ScribanTemplateSource>(StringComparer.Ordinal);
        foreach (var declared in pack.Templates.OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            if (Unsafe(declared.Path) || !IsSha256(declared.Digest)) return await Failure(host, machine, "workspace.template.invalid", "A declared template is invalid.");
            var path = Join(root, Join(packDirectory, declared.Path));
            if (!host.Exists(path)) return await Failure(host, machine, "workspace.template.missing", "A declared template could not be read.");
            var content = await host.ReadTextAsync(path, cancellationToken);
            if (!StringComparer.Ordinal.Equals(Digest(content), declared.Digest))
                return await Failure(host, machine, "workspace.template.digest-mismatch", $"Template '{declared.Id}' does not match its pinned digest.");
            templates.Add(declared.Id, new(declared.Digest, content));
        }

        var package = parsed.Package!;
        var contextId = package.AuthoredRevision.Id.ToString();
        var snapshot = new ResolvedGenerationSnapshot(
            new FederationSnapshot([new(contextId, package.AuthoredRevision.Slug.Value, package.AuthoredRevision.ContextVersion, package.PackageDigest, package.SemanticDigest)]),
            package.AuthoredRevision.Definitions.Select(definition => new GenerationSemanticInput(definition.Slug.Value, contextId, package.SemanticDigest)).ToImmutableArray());
        var templateDescriptors = ImmutableArray.CreateBuilder<TemplateArtifactDescriptor>();
        foreach (var recipe in pack.Outputs)
        {
            if (!templates.TryGetValue(recipe.TemplateId, out var template))
                return await Failure(host, machine, "workspace.output.invalid", "A template-pack output recipe is invalid.");
            var selected = Select(package.AuthoredRevision, recipe.Scope);
            if (selected is null)
                return await Failure(host, machine, "workspace.output.scope-invalid", $"Output recipe '{recipe.Id}' uses an unsupported scope.");
            foreach (var definition in selected)
            {
                var name = definition is null ? configuration.Parameters.ProjectName : CSharpTemplateNaming.Identifier(definition.Name.Value);
                var logicalPath = recipe.LogicalPath
                    .Replace("{projectName}", configuration.Parameters.ProjectName, StringComparison.Ordinal)
                    .Replace("{definitionName}", name, StringComparison.Ordinal);
                if (Unsafe(logicalPath)) return await Failure(host, machine, "workspace.output.path-invalid", "An expanded template-pack output path is invalid.");
                var suffix = definition is null ? "context" : definition.Slug.Value;
                templateDescriptors.Add(new($"{recipe.Id}:{suffix}", recipe.TemplateId, logicalPath, recipe.Owner, template.Digest,
                    definition is null ? [] : [definition.Slug.Value]));
            }
        }
        var runtimeConfiguration = resolvedConfiguration.Configuration!;
        var planning = new GenerationPlanningRequest(snapshot,
            new(configuration.Profile, runtimeConfiguration.GenerationContractVersion, runtimeConfiguration.LogicalOutputRoot, Digest(configurationText)),
            new(pack.Id, pack.PackVersion, pack.GenerationContractVersion, Digest(packText), templateDescriptors.ToImmutable()));

        var manifestPath = Join(root, configuration.OwnershipManifest);
        var manifest = OwnershipManifest.Empty;
        string? manifestText = null;
        if (host.Exists(manifestPath))
        {
            try
            {
                manifestText = await host.ReadTextAsync(manifestPath, cancellationToken);
                manifest = JsonSerializer.Deserialize<OwnershipManifest>(manifestText, Json) ?? OwnershipManifest.Empty;
            }
            catch (Exception exception) when (exception is IOException or JsonException or NotSupportedException)
            { return await Failure(host, machine, "workspace.manifest.invalid", "The ownership manifest is invalid."); }
        }
        var outputRoot = Join(root, runtimeConfiguration.LogicalOutputRoot);
        var scriban = new ScribanRendererAdapter("scriban/csharp", "1.0", templates.ToImmutable(), globalsProvider:
            new CSharpTemplateGlobalsProvider(package.AuthoredRevision, configuration.Parameters.Namespace, configuration.Parameters.ProjectName, configuration.Parameters.TargetFramework));
        var execution = await GenerationExecution.ExecuteAsync(new(planning, manifest, dryRun ? OutputMode.Preview : OutputMode.Apply),
            scriban, new CliOutputFileSystem(host, outputRoot), cancellationToken);
        if (execution.Output is null)
            return await Failure(host, machine, execution.Diagnostics.FirstOrDefault() ?? "workspace.generation.failed", "Workspace generation failed.");

        var output = execution.Output;
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

    private sealed record WorkspaceConfiguration(string Version, string GenerationContractVersion, string LogicalOutputRoot, string Profile,
        IReadOnlyList<string> Sources, string TemplatePack, PackParameters Parameters,
        string IdentityRegistry = ".modeller/identities.json", string OwnershipManifest = ".modeller/generated-manifest.json");
    private sealed record IdentityRegistry(string Version, IReadOnlyDictionary<string, IReadOnlyList<string>> Documents);
    private sealed record PackParameters(string ProjectName, string Namespace, string TargetFramework);
    private sealed record TemplatePackFile(string Version, string Id, string PackVersion, string GenerationContractVersion,
        IReadOnlyList<TemplateFile> Templates, IReadOnlyList<OutputFile> Outputs);
    private sealed record TemplateFile(string Id, string Path, string Digest);
    private sealed record OutputFile(string Id, string Scope, string TemplateId, string LogicalPath, string Owner);

}
