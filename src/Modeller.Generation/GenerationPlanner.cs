using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Modeller.Contexts;
using Modeller.Templates;

namespace Modeller.Generation;

public static class GenerationPlanner
{
    private const int MaximumArtifacts = 10_000;

    public static GenerationPlanResult Plan(
        GenerationPlanningRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (cancellationToken.IsCancellationRequested)
        {
            return Failure("generation.plan.cancelled", "Generation planning was cancelled.");
        }

        if (!IsSafeRelativePath(request.Configuration.LogicalOutputRoot))
        {
            return Failure(
                "generation.configuration.output-root-invalid",
                "The logical output root must be relative and cannot escape the configured output.");
        }

        if (!string.Equals(
                request.Configuration.GenerationContractVersion,
                request.TemplatePack.GenerationContractVersion,
                StringComparison.Ordinal))
        {
            return Failure(
                "generation.pack.incompatible",
                $"Template pack contract '{request.TemplatePack.GenerationContractVersion}' is incompatible with configuration contract '{request.Configuration.GenerationContractVersion}'.");
        }

        if (request.TemplatePack.Artifacts.Length > MaximumArtifacts)
        {
            return Failure(
                "generation.plan.limit.artifacts",
                $"A generation plan cannot contain more than {MaximumArtifacts} proposed artifacts.");
        }

        var packages = request.Snapshot.Federation.Packages
            .ToDictionary(package => package.ContextId, StringComparer.Ordinal);
        var duplicateInput = request.Snapshot.SemanticInputs
            .GroupBy(input => input.Id, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Skip(1).Any());
        if (duplicateInput is not null)
        {
            return Failure(
                "generation.snapshot.input-duplicate",
                $"Semantic input '{duplicateInput.Key}' occurs more than once in the resolved snapshot.");
        }

        var inputs = request.Snapshot.SemanticInputs.ToDictionary(input => input.Id, StringComparer.Ordinal);
        foreach (var input in inputs.Values)
        {
            if (!packages.ContainsKey(input.ContextId))
            {
                return Failure(
                    "generation.snapshot.input-context-unresolved",
                    $"Semantic input '{input.Id}' refers to context '{input.ContextId}', which is not in the federation snapshot.");
            }
        }

        var proposed = ImmutableArray.CreateBuilder<ProposedArtifact>();
        foreach (var descriptor in request.TemplatePack.Artifacts)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Failure("generation.plan.cancelled", "Generation planning was cancelled.");
            }

            if (!IsSafeRelativePath(descriptor.LogicalPath))
            {
                return Failure(
                    "generation.artifact.path-invalid",
                    $"Artifact '{descriptor.ArtifactId}' has a logical path that escapes the configured output root.");
            }

            var artifactInputs = ImmutableArray.CreateBuilder<PlannedSemanticInput>();
            foreach (var inputId in descriptor.SemanticInputIds.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
            {
                if (!inputs.TryGetValue(inputId, out var input))
                {
                    return Failure(
                        "generation.artifact.input-unresolved",
                        $"Artifact '{descriptor.ArtifactId}' requires semantic input '{inputId}', which is not in the resolved snapshot.");
                }

                artifactInputs.Add(new PlannedSemanticInput(input.Id, input.ContextId, input.SemanticDigest));
            }

            var ownership = new ArtifactOwnership(
                descriptor.Owner,
                request.TemplatePack.PackId,
                request.TemplatePack.PackVersion,
                descriptor.TemplateId,
                request.TemplatePack.Renderer);
            var inputDigest = Digest(string.Join(
                '\n',
                request.Configuration.Digest,
                request.TemplatePack.Digest,
                request.TemplatePack.Renderer.Id,
                request.TemplatePack.Renderer.Version,
                descriptor.TemplateDigest,
                descriptor.ArtifactId,
                descriptor.LogicalPath.Replace('\\', '/'),
                descriptor.Owner,
                string.Join('\n', artifactInputs.Select(input => $"{input.Id}:{input.SemanticDigest}"))));
            proposed.Add(new ProposedArtifact(
                0,
                descriptor.ArtifactId,
                descriptor.LogicalPath.Replace('\\', '/'),
                ownership,
                artifactInputs.ToImmutable(),
                descriptor.TemplateDigest,
                inputDigest));
        }

        var conflict = proposed
            .GroupBy(artifact => artifact.LogicalPath, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Skip(1).Any());
        if (conflict is not null)
        {
            return Failure(
                "generation.artifact.ownership-conflict",
                $"Multiple proposed artifacts claim logical path '{conflict.Key}'.");
        }

        var artifacts = proposed
            .OrderBy(artifact => artifact.LogicalPath, StringComparer.Ordinal)
            .ThenBy(artifact => artifact.ArtifactId, StringComparer.Ordinal)
            .Select((artifact, index) => artifact with { Ordinal = index })
            .ToImmutableArray();
        var planDigest = Digest(string.Join(
            '\n',
            "generation-plan:1.0",
            request.Configuration.ProfileId,
            request.Configuration.GenerationContractVersion,
            request.Configuration.LogicalOutputRoot.Replace('\\', '/'),
            request.TemplatePack.PackId,
            request.TemplatePack.PackVersion,
            request.TemplatePack.Renderer.Id,
            request.TemplatePack.Renderer.Version,
            string.Join('\n', artifacts.Select(artifact =>
                $"{artifact.Ordinal}:{artifact.ArtifactId}:{artifact.LogicalPath}:{artifact.Ownership.Owner}:{artifact.InputDigest}"))));

        return new GenerationPlanResult(
            new GenerationPlan(
                "1.0",
                request.Configuration.LogicalOutputRoot.Replace('\\', '/'),
                artifacts,
                planDigest),
            []);
    }

    private static bool IsSafeRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || IsRooted(path))
        {
            return false;
        }

        var normalized = path.Replace('\\', '/');
        return !normalized.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Contains("..", StringComparer.Ordinal) &&
            !normalized.StartsWith("/", StringComparison.Ordinal) &&
            !normalized.Contains('\0');
    }

    private static bool IsRooted(string value) =>
        Path.IsPathRooted(value) || value.StartsWith('/') || value.StartsWith('\\') ||
        (value.Length >= 2 && value[1] == ':' && char.IsAsciiLetter(value[0]));

    private static string Digest(string value) =>
        $"sha256:{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)))}";

    private static GenerationPlanResult Failure(string code, string message) =>
        new(null, [new GenerationDiagnostic(code, message)]);
}

public sealed record GenerationPlanningRequest(
    ResolvedGenerationSnapshot Snapshot,
    ValidatedGenerationConfiguration Configuration,
    ValidatedTemplatePackDescriptor TemplatePack);

public sealed record ResolvedGenerationSnapshot(
    FederationSnapshot Federation,
    ImmutableArray<GenerationSemanticInput> SemanticInputs);

public sealed record GenerationSemanticInput(string Id, string ContextId, string SemanticDigest);

public sealed record ValidatedGenerationConfiguration(
    string ProfileId,
    string GenerationContractVersion,
    string LogicalOutputRoot,
    string Digest);

public sealed record ValidatedTemplatePackDescriptor(
    string PackId,
    string PackVersion,
    string GenerationContractVersion,
    string Digest,
    ImmutableArray<TemplateArtifactDescriptor> Artifacts,
    RendererIdentity Renderer = default,
    string Language = "");

public sealed record TemplateArtifactDescriptor(
    string ArtifactId,
    string TemplateId,
    string LogicalPath,
    string Owner,
    string TemplateDigest,
    ImmutableArray<string> SemanticInputIds);

public sealed record GenerationPlan(
    string SchemaVersion,
    string LogicalOutputRoot,
    ImmutableArray<ProposedArtifact> Artifacts,
    string Digest);

public sealed record ProposedArtifact(
    int Ordinal,
    string ArtifactId,
    string LogicalPath,
    ArtifactOwnership Ownership,
    ImmutableArray<PlannedSemanticInput> SemanticInputs,
    string TemplateDigest,
    string InputDigest);

public sealed record ArtifactOwnership(string Owner, string PackId, string PackVersion, string TemplateId,
    RendererIdentity Renderer = default);

public sealed record PlannedSemanticInput(string Id, string ContextId, string SemanticDigest);

public sealed record GenerationDiagnostic(string Code, string Message);

public sealed record GenerationPlanResult(
    GenerationPlan? Plan,
    ImmutableArray<GenerationDiagnostic> Diagnostics)
{
    public bool IsSuccess => Plan is not null && Diagnostics.IsEmpty;
}
