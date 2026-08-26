using System.Collections.Immutable;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Modeller.Api.Contracts;
using Modeller.Contexts;
using Modeller.Generation;
using Modeller.Model;
using Modeller.Rendering;
using Modeller.Templates;
using Modeller.Workspace;

namespace Modeller.Api;

/// <summary>The HTTP status code to send alongside a <see cref="WorkspaceGenerateResponse"/> body.</summary>
public sealed record GeneratePipelineResult(WorkspaceGenerateResponse Body, int StatusCode);

/// <summary>
/// The generation-preview request pipeline for <c>POST /v1/workspace/generate</c>: validate,
/// analyze (exactly as <c>/analyze</c> does), resolve the named template pack from the small
/// in-process <see cref="EmbeddedTemplatePackCatalog"/>, plan with <see cref="GenerationPlanner"/>,
/// and render with <see cref="TemplateRenderer"/> — entirely in-memory, never touching a
/// filesystem. Mirrors <see cref="WorkspaceAnalysisPipeline"/>'s status-code pattern: 400 for a
/// request-shape violation, 200 with diagnostics for any content-level failure (parse, validation,
/// unknown pack, incompatible generation contract, plan/render failure), 503 on cancellation.
/// </summary>
public sealed class WorkspaceGenerationPreviewPipeline(ILogger<WorkspaceGenerationPreviewPipeline> logger)
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);
    private static readonly JsonSerializerOptions ConfigurationDigestJson = new(JsonSerializerDefaults.Web);

    public async Task<GeneratePipelineResult> HandleAsync(WorkspaceGenerateRequest request, CancellationToken cancellationToken)
    {
        var violations = RequestLimits.Validate(request);
        if (violations.Count > 0)
        {
            logger.LogInformation("Workspace generate request rejected: {DiagnosticCodes}", string.Join(',', violations.Select(v => v.Code)));
            return new(new("1.0", violations, []), StatusCodes.Status400BadRequest);
        }

        var stopwatch = Stopwatch.StartNew();
        using var timeout = new CancellationTokenSource(RequestTimeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

        var analyzed = ModellerWorkspace.Analyze(request.ToWorkspaceInput(), linked.Token);
        var result = analyzed switch
        {
            WorkspaceOutcome<AnalyzedWorkspace>.Success success => await BuildResponseAsync(success.Value, request.TemplatePackId, linked.Token),
            WorkspaceOutcome<AnalyzedWorkspace>.Failed failed => WorkspaceOutcome.Failed<WorkspaceGenerateResponse>(failed.Diagnostics),
            _ => WorkspaceOutcome.Cancelled<WorkspaceGenerateResponse>(),
        };

        LogOutcome(result, stopwatch.ElapsedMilliseconds);
        return ToPipelineResult(result);
    }

    private static async Task<WorkspaceOutcome<WorkspaceGenerateResponse>> BuildResponseAsync(
        AnalyzedWorkspace analyzed, string templatePackId, CancellationToken cancellationToken)
    {
        if (!EmbeddedTemplatePackCatalog.TryGet(templatePackId, out var source))
            return WorkspaceOutcome.Success(ContentDiagnostic(
                "api.generate.template-pack.unknown", $"Template pack '{templatePackId}' is not recognized."));

        var loaded = TemplatePackLoader.Load(
            new PackLoadRequest(source, [analyzed.Configuration.GenerationContractVersion], RendererCapabilityRegistry.SupportedRenderers),
            cancellationToken);
        if (!loaded.IsSuccess)
            return WorkspaceOutcome.Success(new WorkspaceGenerateResponse(
                "1.0", [.. loaded.Diagnostics.Select(diagnostic => new ApiDiagnostic(diagnostic.Code, diagnostic.Message))], []));
        var validated = loaded.Pack!;

        var capability = RendererCapabilityRegistry.Resolve(validated.Renderer, validated.Language);
        if (capability is null)
            return WorkspaceOutcome.Success(ContentDiagnostic(
                "api.generate.template-pack.renderer-unsupported", "The template pack targets an unsupported renderer/language combination."));

        var revision = analyzed.Package.AuthoredRevision;
        // The request shape (Documents/Identity/Configuration + TemplatePackId) carries no
        // language-parameter block the way the CLI's on-disk workspace configuration does
        // (namespace/targetFramework for a C# pack) — the Specifier stage didn't add one. Default
        // project name and namespace from the analyzed context's own name, and pin a fixed target
        // framework, so a preview never depends on parameters this request has no field for. This
        // is a deviation from the CLI's parameter-driven flow, noted for the record.
        var projectName = capability.NameForPath(revision.Name.Value);
        var parameters = DefaultLanguageParameters(capability, projectName);
        if (!capability.TryValidateParameters(parameters, out var parameterDiagnosticCode))
            return WorkspaceOutcome.Success(ContentDiagnostic(
                parameterDiagnosticCode!, "The default generation parameters are invalid for the selected template pack."));

        var (descriptorError, descriptors) = BuildTemplateDescriptors(revision, validated, projectName, capability);
        if (descriptorError is not null)
            return WorkspaceOutcome.Success(ContentDiagnostic(descriptorError.Value.Code, descriptorError.Value.Message));

        var snapshot = BuildSnapshot(analyzed.Package);
        var configurationDigest = Digest(JsonSerializer.Serialize(
            new { analyzed.Configuration.GenerationContractVersion, analyzed.Configuration.LogicalOutputRoot }, ConfigurationDigestJson));
        var planningRequest = new GenerationPlanningRequest(
            snapshot,
            new ValidatedGenerationConfiguration("default", analyzed.Configuration.GenerationContractVersion, analyzed.Configuration.LogicalOutputRoot, configurationDigest),
            new ValidatedTemplatePackDescriptor(validated.Id, validated.PackVersion, validated.GenerationContractVersion, validated.Digest, descriptors, validated.Renderer, validated.Language));

        var planned = GenerationPlanner.Plan(planningRequest, cancellationToken);
        if (!planned.IsSuccess)
            return WorkspaceOutcome.Success(new WorkspaceGenerateResponse(
                "1.0", [.. planned.Diagnostics.Select(diagnostic => new ApiDiagnostic(diagnostic.Code, diagnostic.Message))], []));
        var plan = planned.Plan!;

        var globalsProvider = capability.CreateGlobalsProvider(revision, projectName, parameters);
        var templates = validated.Outputs
            .GroupBy(output => output.TemplateId, StringComparer.Ordinal)
            .ToImmutableDictionary(
                group => group.Key,
                group => new ScribanTemplateSource(group.First().TemplateDigest, validated.Templates[group.Key]),
                StringComparer.Ordinal);
        var adapter = new ScribanRendererAdapter(capability.Renderer.Id, capability.Renderer.Version, templates, globalsProvider: globalsProvider);

        var rendered = await TemplateRenderer.RenderAsync(new RenderingRequest(plan), adapter, cancellationToken);
        if (!rendered.IsSuccess)
            return WorkspaceOutcome.Success(new WorkspaceGenerateResponse(
                "1.0", [.. rendered.Diagnostics.Select(diagnostic => new ApiDiagnostic(diagnostic.Code, diagnostic.Message))], []));

        var ownerByOrdinal = plan.Artifacts.ToImmutableDictionary(artifact => artifact.Ordinal, artifact => artifact.Ownership.Owner);
        var artifacts = rendered.Artifacts
            .OrderBy(artifact => artifact.Ordinal)
            .Select(artifact => new GeneratedArtifactDto(
                artifact.LogicalPath, ownerByOrdinal[artifact.Ordinal], artifact.Provenance.PackId, artifact.Provenance.TemplateId,
                artifact.Content, artifact.ContentDigest))
            .ToArray();
        return WorkspaceOutcome.Success(new WorkspaceGenerateResponse("1.0", [], artifacts));
    }

    private static WorkspaceGenerateResponse ContentDiagnostic(string code, string message) => new("1.0", [new(code, message)], []);

    /// <summary>Internal (not private) so <c>Modeller.Api.Tests</c> (<c>InternalsVisibleTo</c> in
    /// the csproj) can property-test its per-language mapping invariants directly, without spinning
    /// up the whole pipeline through HTTP.</summary>
    internal static IReadOnlyDictionary<string, string> DefaultLanguageParameters(IRendererCapability capability, string projectName) =>
        capability.Language switch
        {
            "csharp" => new Dictionary<string, string>(StringComparer.Ordinal) { ["namespace"] = projectName, ["targetFramework"] = "net10.0" },
            "python" => new Dictionary<string, string>(StringComparer.Ordinal) { ["packageName"] = projectName.ToLowerInvariant(), ["pythonVersion"] = "3.13" },
            _ => new Dictionary<string, string>(StringComparer.Ordinal),
        };

    private static (Diagnostic? Error, ImmutableArray<TemplateArtifactDescriptor> Value) BuildTemplateDescriptors(
        AuthoredContextRevision revision, ValidatedTemplatePack validated, string projectName, IRendererCapability capability)
    {
        var templateDescriptors = ImmutableArray.CreateBuilder<TemplateArtifactDescriptor>();
        foreach (var recipe in validated.Outputs)
        {
            var selected = SelectByScope(revision, recipe.Scope);
            if (selected is null)
                return (new Diagnostic("workspace.output.scope-invalid", $"Output recipe '{recipe.Id}' uses an unsupported scope."), default);
            foreach (var definition in selected)
            {
                var name = definition is null ? projectName : capability.NameForPath(definition.Name.Value);
                var logicalPath = recipe.LogicalPathPattern
                    .Replace("{projectName}", projectName, StringComparison.Ordinal)
                    .Replace("{definitionName}", name, StringComparison.Ordinal);
                var suffix = definition is null ? "context" : definition.Slug.Value;
                templateDescriptors.Add(new(
                    $"{recipe.Id}:{suffix}", recipe.TemplateId, logicalPath, recipe.Owner, recipe.TemplateDigest,
                    definition is null ? [] : [definition.Slug.Value]));
            }
        }
        return (null, templateDescriptors.ToImmutable());
    }

    private static IEnumerable<SemanticDefinition?>? SelectByScope(AuthoredContextRevision revision, string scope) => scope switch
    {
        "context" => new SemanticDefinition?[] { null },
        "entity" => revision.Definitions.OfType<EntityDefinition>().OrderBy(item => item.Slug.Value, StringComparer.Ordinal),
        "enumeration" => revision.Definitions.OfType<EnumerationDefinition>().OrderBy(item => item.Slug.Value, StringComparer.Ordinal),
        "rule" => revision.Definitions.OfType<RuleDefinition>().OrderBy(item => item.Slug.Value, StringComparer.Ordinal),
        "behaviour" => revision.Definitions.OfType<BehaviourDefinition>().OrderBy(item => item.Slug.Value, StringComparer.Ordinal),
        _ => null,
    };

    private static ResolvedGenerationSnapshot BuildSnapshot(LoadedContextPackage package)
    {
        var revision = package.AuthoredRevision;
        var contextId = revision.Id.ToString();
        return new ResolvedGenerationSnapshot(
            new FederationSnapshot([new(contextId, revision.Slug.Value, revision.ContextVersion, package.PackageDigest, package.SemanticDigest)]),
            [.. revision.Definitions.Select(definition => new GenerationSemanticInput(definition.Slug.Value, contextId, package.SemanticDigest))]);
    }

    /// <summary>Internal (not private) so <c>Modeller.Api.Tests</c> can property-test the digest's
    /// determinism/format/collision-resistance invariants directly.</summary>
    internal static string Digest(string content) => $"sha256:{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content)))}";

    private void LogOutcome(WorkspaceOutcome<WorkspaceGenerateResponse> result, long elapsedMilliseconds)
    {
        switch (result)
        {
            case WorkspaceOutcome<WorkspaceGenerateResponse>.Success success:
                logger.LogInformation("Workspace generate request succeeded with {ArtifactCount} artifact(s) in {ElapsedMs}ms.", success.Value.Artifacts.Count, elapsedMilliseconds);
                break;
            case WorkspaceOutcome<WorkspaceGenerateResponse>.Failed failed:
                logger.LogInformation("Workspace generate request failed with {DiagnosticCount} diagnostic(s) in {ElapsedMs}ms.", failed.Diagnostics.Length, elapsedMilliseconds);
                break;
            default:
                logger.LogWarning("Workspace generate request timed out or was cancelled after {ElapsedMs}ms.", elapsedMilliseconds);
                break;
        }
    }

    private static GeneratePipelineResult ToPipelineResult(WorkspaceOutcome<WorkspaceGenerateResponse> result) => result switch
    {
        WorkspaceOutcome<WorkspaceGenerateResponse>.Success success => new(success.Value, StatusCodes.Status200OK),
        WorkspaceOutcome<WorkspaceGenerateResponse>.Failed failed =>
            new(new("1.0", [.. failed.Diagnostics.Select(WorkspaceContractMappings.ToApiDiagnostic)], []), StatusCodes.Status200OK),
        _ => new(
            new("1.0", [new("api.request.timeout", "The request was cancelled or exceeded the server-side time budget.")], []),
            StatusCodes.Status503ServiceUnavailable),
    };

    private readonly record struct Diagnostic(string Code, string Message);
}
