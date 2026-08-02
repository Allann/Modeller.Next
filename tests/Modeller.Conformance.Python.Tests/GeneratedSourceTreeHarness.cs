using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Modeller.Contexts;
using Modeller.Generation;
using Modeller.GenerationWorkflow;
using Modeller.Model;
using Modeller.Output;
using Modeller.Parsing;
using Modeller.Rendering;
using Modeller.Templates;

namespace Modeller.Conformance.Python.Tests;

public sealed record GeneratedSourceTreeResult(
    bool Success, ImmutableArray<string> Diagnostics, IReadOnlyDictionary<string, string> Files,
    OwnershipManifest Manifest, ImmutableArray<OutputChange> Changes, InMemoryOutputFileSystem FileSystem)
{
    public static GeneratedSourceTreeResult Failed(string diagnostic) =>
        new(false, [diagnostic], ImmutableDictionary<string, string>.Empty, OwnershipManifest.Empty, [], new InMemoryOutputFileSystem());
}

/// <summary>
/// Drives template packs through the exact same public pipeline the CLI uses — <see cref="TemplatePackLoader"/>,
/// <see cref="RendererCapabilityRegistry"/>, <see cref="GenerationPlanner"/>, <see cref="TemplateRenderer"/> and
/// <see cref="GenerationExecution"/> — against an in-memory output filesystem. Nothing here is Child-Care- or
/// Python-specific: the sample workspace root, pack directory, and profile id are all parameters (the workspace's
/// own <c>.modeller/config.json</c> supplies the model sources), and the renderer identity used to construct the
/// adapter always comes from the resolved <see cref="IRendererCapability"/>, never a literal. A future language
/// renderer's conformance suite can reuse this harness directly against its own sample workspace and pack.
/// </summary>
public static class GeneratedSourceTreeHarness
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

    public static async ValueTask<GeneratedSourceTreeResult> GenerateAsync(
        string packDirectoryRelativeToSampleRoot,
        string projectName,
        IReadOnlyDictionary<string, string> languageParameters,
        string sampleRootRelativeToRepository = "samples/child-care",
        string? profileId = null,
        ImmutableArray<RendererIdentity>? renderersOverride = null,
        Func<string, string>? mutatePackText = null,
        OwnershipManifest? previousManifest = null,
        InMemoryOutputFileSystem? fileSystem = null,
        CancellationToken cancellationToken = default)
    {
        var sampleRoot = Path.Combine(RepositoryRoot(), sampleRootRelativeToRepository.Replace('/', Path.DirectorySeparatorChar));

        var parsed = ParseModel(sampleRoot, cancellationToken);
        if (!parsed.IsSuccess) return GeneratedSourceTreeResult.Failed(parsed.Diagnostics.IsEmpty ? "model.parse.failed" : parsed.Diagnostics[0].Code);

        var packDirectory = Path.Combine(sampleRoot, packDirectoryRelativeToSampleRoot.Replace('/', Path.DirectorySeparatorChar));
        var packText = File.ReadAllText(Path.Combine(packDirectory, "pack.json"));
        if (mutatePackText is not null) packText = mutatePackText(packText);

        TemplatePinningManifest? pinning;
        try { pinning = JsonSerializer.Deserialize<TemplatePinningManifest>(packText, JsonOptions); }
        catch (JsonException) { return GeneratedSourceTreeResult.Failed("template-pack.manifest.invalid"); }
        if (pinning?.Templates is null || pinning.Templates.Count == 0) return GeneratedSourceTreeResult.Failed("template-pack.invalid");

        var templates = ImmutableDictionary.CreateBuilder<string, ScribanTemplateSource>(StringComparer.Ordinal);
        var rawTemplates = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        foreach (var declared in pinning.Templates)
        {
            var path = Path.Combine(packDirectory, declared.Path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) return GeneratedSourceTreeResult.Failed($"template.missing:{declared.Id}");
            var content = File.ReadAllText(path);
            var digest = Digest(content);
            if (!StringComparer.Ordinal.Equals(digest, declared.Digest)) return GeneratedSourceTreeResult.Failed($"template.digest-mismatch:{declared.Id}");
            templates[declared.Id] = new(digest, content);
            rawTemplates[declared.Id] = content;
        }

        var packSource = new PackSource(Path.GetFileName(packDirectory), packText, rawTemplates.ToImmutable());
        var renderers = renderersOverride ?? RendererCapabilityRegistry.SupportedRenderers;
        var loaded = TemplatePackLoader.Load(new PackLoadRequest(packSource, ["1.0"], renderers), cancellationToken);
        if (!loaded.IsSuccess) return GeneratedSourceTreeResult.Failed(loaded.Diagnostics[0].Code);
        var validated = loaded.Pack!;

        var capability = RendererCapabilityRegistry.Resolve(validated.Renderer, validated.Language);
        if (capability is null) return GeneratedSourceTreeResult.Failed("template-pack.renderer-unsupported");
        var resolvedProfileId = profileId ?? $"{Path.GetFileName(sampleRoot)}-{capability.Language}";

        var package = parsed.Package!;
        var contextId = package.AuthoredRevision.Id.ToString();
        var snapshot = new ResolvedGenerationSnapshot(
            new FederationSnapshot([new(contextId, package.AuthoredRevision.Slug.Value, package.AuthoredRevision.ContextVersion, package.PackageDigest, package.SemanticDigest)]),
            package.AuthoredRevision.Definitions.Select(definition => new GenerationSemanticInput(definition.Slug.Value, contextId, package.SemanticDigest)).ToImmutableArray());

        var templateDescriptors = ImmutableArray.CreateBuilder<TemplateArtifactDescriptor>();
        foreach (var recipe in validated.Outputs)
        {
            if (!templates.TryGetValue(recipe.TemplateId, out var template)) return GeneratedSourceTreeResult.Failed("output.invalid");
            var selected = Select(package.AuthoredRevision, recipe.Scope);
            if (selected is null) return GeneratedSourceTreeResult.Failed($"output.scope-invalid:{recipe.Id}");
            foreach (var definition in selected)
            {
                var name = definition is null ? projectName : capability.NameForPath(definition.Name.Value);
                var logicalPath = recipe.LogicalPathPattern.Replace("{projectName}", projectName, StringComparison.Ordinal).Replace("{definitionName}", name, StringComparison.Ordinal);
                var suffix = definition is null ? "context" : definition.Slug.Value;
                templateDescriptors.Add(new($"{recipe.Id}:{suffix}", recipe.TemplateId, logicalPath, recipe.Owner, template.Digest,
                    definition is null ? [] : [definition.Slug.Value]));
            }
        }

        var planning = new GenerationPlanningRequest(snapshot,
            new ValidatedGenerationConfiguration(resolvedProfileId, "1.0", "generated", Digest(packText)),
            new ValidatedTemplatePackDescriptor(validated.Id, validated.PackVersion, validated.GenerationContractVersion, validated.Digest,
                templateDescriptors.ToImmutable(), validated.Renderer, validated.Language));

        var globalsProvider = capability.CreateGlobalsProvider(package.AuthoredRevision, projectName, languageParameters);
        var adapter = new ScribanRendererAdapter(capability.Renderer.Id, capability.Renderer.Version, templates.ToImmutable(), globalsProvider: globalsProvider);
        var outputFileSystem = fileSystem ?? new InMemoryOutputFileSystem();
        var execution = await GenerationExecution.ExecuteAsync(new(planning, previousManifest ?? OwnershipManifest.Empty, OutputMode.Apply), adapter, outputFileSystem, cancellationToken);
        if (execution.Output is null) return GeneratedSourceTreeResult.Failed(execution.Diagnostics.FirstOrDefault() ?? "generation.failed");

        return new GeneratedSourceTreeResult(execution.IsSuccess, execution.Diagnostics, outputFileSystem.Files,
            execution.Output.Manifest, execution.Output.Changes, outputFileSystem);
    }

    /// <summary>Parses whatever model a sample workspace's own <c>.modeller/config.json</c> declares — nothing here is Child-Care-specific.</summary>
    private static ParseResult ParseModel(string sampleRoot, CancellationToken cancellationToken)
    {
        var configText = File.ReadAllText(Path.Combine(sampleRoot, ".modeller", "config.json"));
        using var config = JsonDocument.Parse(configText);
        var sources = config.RootElement.GetProperty("sources").EnumerateArray().Select(item => item.GetString()!).ToArray();

        var identitiesText = File.ReadAllText(Path.Combine(sampleRoot, ".modeller", "identities.json"));
        using var identitiesDocument = JsonDocument.Parse(identitiesText);
        var documentsElement = identitiesDocument.RootElement.GetProperty("documents");

        var documents = ImmutableArray.CreateBuilder<SourceDocument>();
        foreach (var declared in sources.Order(StringComparer.Ordinal))
        {
            var identities = documentsElement.GetProperty(declared).EnumerateArray().Select(item => item.GetString()!).ToArray();
            var source = File.ReadAllText(Path.Combine(sampleRoot, declared.Replace('/', Path.DirectorySeparatorChar)));
            documents.Add(new(declared, RmlCompiler.ApplyIdentities(source, identities).Updated));
        }

        return DefinitionParser.Parse(documents.ToImmutable(), ParseOptions.Language1, cancellationToken);
    }

    private static IEnumerable<SemanticDefinition?>? Select(AuthoredContextRevision revision, string scope) => scope switch
    {
        "context" => new SemanticDefinition?[] { null },
        "entity" => revision.Definitions.OfType<EntityDefinition>().OrderBy(item => item.Slug.Value, StringComparer.Ordinal),
        "enumeration" => revision.Definitions.OfType<EnumerationDefinition>().OrderBy(item => item.Slug.Value, StringComparer.Ordinal),
        "rule" => revision.Definitions.OfType<RuleDefinition>().OrderBy(item => item.Slug.Value, StringComparer.Ordinal),
        "behaviour" => revision.Definitions.OfType<BehaviourDefinition>().OrderBy(item => item.Slug.Value, StringComparer.Ordinal),
        _ => null
    };

    private static string Digest(string content) => $"sha256:{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content)))}";

    public static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Modeller.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Could not locate the repository root from the test output directory.");
    }

    private sealed record TemplatePinningManifest(IReadOnlyList<TemplateFile> Templates);
    private sealed record TemplateFile(string Id, string Path, string Digest);
}
