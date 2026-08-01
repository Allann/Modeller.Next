using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Modeller.Generation;

namespace Modeller.Rendering;

public interface IRendererAdapter
{
    string RendererId { get; }
    string ContractVersion { get; }

    ValueTask<AdapterRenderResult> RenderAsync(
        ArtifactRenderingContext context,
        CancellationToken cancellationToken = default);
}

public static class TemplateRenderer
{
    public static async ValueTask<RenderingResult> RenderAsync(
        RenderingRequest request,
        IRendererAdapter adapter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(adapter);

        if (!StringComparer.Ordinal.Equals(request.Plan.SchemaVersion, adapter.ContractVersion))
        {
            return Failure("rendering.renderer.incompatible", "The renderer contract is incompatible with the generation plan.");
        }

        var rendered = ImmutableArray.CreateBuilder<ProposedOutputArtifact>();
        foreach (var artifact in request.Plan.Artifacts)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Failure("rendering.cancelled", "Template rendering was cancelled.");
            }

            AdapterRenderResult result;
            try
            {
                result = await adapter.RenderAsync(
                    new ArtifactRenderingContext(request.Plan, artifact),
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return Failure("rendering.cancelled", "Template rendering was cancelled.");
            }
            catch (Exception)
            {
                return Failure("rendering.adapter.failed", "The renderer adapter failed.");
            }
            if (result.Content is null || !result.Diagnostics.IsEmpty)
            {
                return new RenderingResult([], result.Diagnostics);
            }

            rendered.Add(new ProposedOutputArtifact(
                artifact.Ordinal,
                artifact.ArtifactId,
                artifact.LogicalPath,
                result.Content,
                Digest(result.Content),
                new RenderingProvenance(
                    request.Plan.Digest,
                    artifact.Ownership.PackId,
                    artifact.Ownership.PackVersion,
                    artifact.Ownership.TemplateId,
                    artifact.TemplateDigest,
                    artifact.InputDigest,
                    adapter.RendererId,
                    adapter.ContractVersion)));
        }

        return new RenderingResult(rendered.ToImmutable(), []);
    }

    private static string Digest(string content) =>
        $"sha256:{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content)))}";

    private static RenderingResult Failure(string code, string message) =>
        new([], [new RenderingDiagnostic(code, message)]);
}

public sealed record RenderingRequest(GenerationPlan Plan);
public sealed record ArtifactRenderingContext(GenerationPlan Plan, ProposedArtifact Artifact);
public sealed record AdapterRenderResult(string? Content, ImmutableArray<RenderingDiagnostic> Diagnostics);
public sealed record RenderingDiagnostic(string Code, string Message);
public sealed record RenderingProvenance(
    string PlanDigest,
    string PackId,
    string PackVersion,
    string TemplateId,
    string TemplateDigest,
    string InputDigest,
    string RendererId,
    string RendererContractVersion);
public sealed record ProposedOutputArtifact(
    int Ordinal,
    string ArtifactId,
    string LogicalPath,
    string Content,
    string ContentDigest,
    RenderingProvenance Provenance);
public sealed record RenderingResult(
    ImmutableArray<ProposedOutputArtifact> Artifacts,
    ImmutableArray<RenderingDiagnostic> Diagnostics)
{
    public bool IsSuccess => Diagnostics.IsEmpty;
}
