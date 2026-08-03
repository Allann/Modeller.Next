using System.Collections.Immutable;
using System.Text.Json;
using Modeller.Configuration;
using Modeller.Contexts;
using Modeller.Workspace;

namespace Modeller.Cli;

/// <summary>
/// Loads a workspace's declared sources into one parsed <see cref="LoadedContextPackage"/>. Reads
/// config.json, the identity registry, and each declared source off disk (via <see cref="ICliHost"/>)
/// exactly as before, then delegates identity application and parsing to
/// <see cref="ModellerWorkspace.Analyze"/> — the transport-neutral seam shared with any other host
/// (local Studio, a future hosted API, tests). This is the CLI's adapter over that seam: it owns
/// file I/O and CLI-specific diagnostics/exit codes, and none of that leaks into
/// <see cref="Modeller.Workspace"/> itself.
/// </summary>
internal static class WorkspaceLoader
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true, WriteIndented = true };

    public static async ValueTask<Outcome<LoadedWorkspace>> LoadAsync(string workspace, ICliHost host, bool machine, CancellationToken cancellationToken)
    {
        var root = Normalize(workspace);
        if (root is null) return await Outcome.FailAsync<LoadedWorkspace>(host, machine, "workspace.path.invalid", "The workspace path must be relative and confined.");

        var configured = await LoadConfigurationAsync(root, host, machine, cancellationToken);
        if (configured.Error is not null) return new(configured.Error, default!);
        var (configuration, configurationText) = configured.Value!;

        var identityRegistry = await LoadIdentityRegistryAsync(root, configuration, host, machine, cancellationToken);
        if (identityRegistry.Error is not null) return new(identityRegistry.Error, default!);

        var documents = await LoadSourceDocumentsAsync(root, configuration, identityRegistry.Value!, host, machine, cancellationToken);
        if (documents.Error is not null) return new(documents.Error, default!);
        var (rawDocuments, registry) = documents.Value;

        var input = new WorkspaceInput(rawDocuments, new IdentityStrategy.Durable(registry),
            new WorkspaceConfigurationInput(configuration.GenerationContractVersion, configuration.LogicalOutputRoot, configuration.Profile));
        var analyzed = ModellerWorkspace.Analyze(input, cancellationToken);
        return analyzed switch
        {
            WorkspaceOutcome<AnalyzedWorkspace>.Cancelled => new(CliExitCode.Cancelled, default!),
            WorkspaceOutcome<AnalyzedWorkspace>.Failed failed =>
                new(await Failure(host, machine, failed.Diagnostics[0].Code, failed.Diagnostics[0].Message), default!),
            WorkspaceOutcome<AnalyzedWorkspace>.Success success =>
                new(null, new(root, success.Value.Package, configuration, configurationText, success.Value.Configuration)),
            _ => throw new InvalidOperationException("Unreachable."),
        };
    }

    private static async ValueTask<Outcome<(WorkspaceConfiguration Configuration, string ConfigurationText)>> LoadConfigurationAsync(
        string root, ICliHost host, bool machine, CancellationToken cancellationToken)
    {
        var configPath = Join(root, ".modeller/config.json");
        if (!host.Exists(configPath))
            return await Outcome.FailAsync<(WorkspaceConfiguration, string)>(host, machine, "workspace.configuration.missing", "The workspace configuration could not be read.");

        WorkspaceConfiguration? configuration;
        string configurationText;
        try
        {
            configurationText = await host.ReadTextAsync(configPath, cancellationToken);
            configuration = JsonSerializer.Deserialize<WorkspaceConfiguration>(configurationText, Json);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return new(CliExitCode.Cancelled, default); }
        catch (Exception exception) when (exception is IOException or JsonException or NotSupportedException)
        { return await Outcome.FailAsync<(WorkspaceConfiguration, string)>(host, machine, "workspace.configuration.invalid", "The workspace configuration is invalid."); }

        if (!IsValidConfiguration(configuration))
            return await Outcome.FailAsync<(WorkspaceConfiguration, string)>(host, machine, "workspace.configuration.invalid", "The workspace configuration is invalid.");

        return new(null, (configuration!, configurationText));
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

    private static async ValueTask<Outcome<(ImmutableArray<WorkspaceDocument> Documents, WorkspaceIdentityRegistry Registry)>> LoadSourceDocumentsAsync(
        string root, WorkspaceConfiguration configuration, IdentityRegistry identityRegistry, ICliHost host, bool machine, CancellationToken cancellationToken)
    {
        var documents = ImmutableArray.CreateBuilder<WorkspaceDocument>();
        var identities = ImmutableDictionary.CreateBuilder<LogicalPath, ImmutableArray<string>>();
        foreach (var declared in configuration.Sources.Order(StringComparer.Ordinal))
        {
            if (!LogicalPath.TryCreate(declared, out var documentPath))
                return await Outcome.FailAsync<(ImmutableArray<WorkspaceDocument>, WorkspaceIdentityRegistry)>(host, machine, "workspace.source.path-invalid", "A declared source path is unsafe.");
            var path = Join(root, declared);
            if (!host.Exists(path))
                return await Outcome.FailAsync<(ImmutableArray<WorkspaceDocument>, WorkspaceIdentityRegistry)>(host, machine, "workspace.source.missing", "A declared source could not be read.");
            if (!identityRegistry.Documents.TryGetValue(documentPath.Value, out var documentIdentities))
                return await Outcome.FailAsync<(ImmutableArray<WorkspaceDocument>, WorkspaceIdentityRegistry)>(host, machine, "workspace.identity-registry.document-missing", $"The identity registry does not cover '{documentPath.Value}'.");
            var source = await host.ReadTextAsync(path, cancellationToken);
            documents.Add(new(documentPath, source));
            identities[documentPath] = documentIdentities.ToImmutableArray();
        }
        return new(null, (documents.ToImmutable(), new WorkspaceIdentityRegistry("1.0", identities.ToImmutable())));
    }

    public static async ValueTask<CliExitCode> Failure(ICliHost host, bool machine, string code, string message)
    {
        if (machine) await host.Output.WriteLineAsync(JsonSerializer.Serialize(new { outputVersion = "1.0", diagnostics = new[] { new { code, message } } }, Json));
        else await host.Error.WriteLineAsync($"{code}: {message}");
        return CliExitCode.Configuration;
    }

    public static string Join(string left, string right) => string.IsNullOrEmpty(left) ? right.Replace('\\', '/') : $"{left.TrimEnd('/', '\\')}/{right.Replace('\\', '/')}";
    private static string? Normalize(string path) => LogicalPath.TryCreate(path, out var logical) ? logical.Value.TrimEnd('/') : null;

    /// <summary>Confined to the workspace per <see cref="LogicalPath"/> — kept as a boolean predicate since most
    /// call sites only need a validity check, not the normalized value itself.</summary>
    public static bool Unsafe(string path) => !LogicalPath.TryCreate(path, out _);

    /// <summary>A stage's outcome: either a <see cref="CliExitCode"/> already reported to the host, or a produced value.</summary>
    public readonly record struct Outcome<T>(CliExitCode? Error, T Value);

    public static class Outcome
    {
        public static async ValueTask<Outcome<T>> FailAsync<T>(ICliHost host, bool machine, string code, string message) =>
            new(await Failure(host, machine, code, message), default!);
    }

    internal sealed record WorkspaceConfiguration(string Version, string GenerationContractVersion, string LogicalOutputRoot, string Profile,
        IReadOnlyList<string> Sources, string TemplatePack, PackParameters Parameters,
        string IdentityRegistry = ".modeller/identities.json", string OwnershipManifest = ".modeller/generated-manifest.json");

    internal sealed record IdentityRegistry(string Version, IReadOnlyDictionary<string, IReadOnlyList<string>> Documents);

    /// <summary>
    /// <c>projectName</c> plus an open-ended set of language-keyed parameter blocks (e.g. <c>csharp</c>,
    /// <c>python</c>). Unrecognized top-level properties fall into <see cref="Languages"/> rather than requiring
    /// a dedicated record per language.
    /// </summary>
    internal sealed record PackParameters(string ProjectName)
    {
        [System.Text.Json.Serialization.JsonExtensionData]
        public Dictionary<string, JsonElement>? Languages { get; init; }
    }
}

internal sealed record LoadedWorkspace(
    string Root,
    LoadedContextPackage Package,
    WorkspaceLoader.WorkspaceConfiguration Configuration,
    string ConfigurationText,
    RuntimeConfiguration Runtime);
