using System.Collections.Immutable;
using Scriban;
using Scriban.Runtime;

namespace Modeller.Rendering;

public sealed record ScribanTemplateSource(string Digest, string Content);

public interface ITemplateGlobalsProvider
{
    IReadOnlyDictionary<string, object?> GetGlobals(ArtifactRenderingContext context);
}

public sealed record ScribanRendererLimits(
    int MaximumTemplateCharacters = 262_144,
    int MaximumOutputBytes = 1_048_576,
    int MaximumLoopIterations = 10_000,
    int MaximumRecursiveDepth = 32);

public sealed class ScribanRendererAdapter : IRendererAdapter
{
    private readonly ImmutableDictionary<string, ScribanTemplateSource> templates;
    private readonly ScribanRendererLimits limits;
    private readonly ITemplateGlobalsProvider? globalsProvider;

    public ScribanRendererAdapter(
        string rendererId,
        string contractVersion,
        ImmutableDictionary<string, ScribanTemplateSource> templates,
        ScribanRendererLimits? limits = null,
        ITemplateGlobalsProvider? globalsProvider = null)
    {
        RendererId = rendererId;
        ContractVersion = contractVersion;
        this.templates = templates.WithComparers(StringComparer.Ordinal);
        this.limits = limits ?? new ScribanRendererLimits();
        this.globalsProvider = globalsProvider;
    }

    public string RendererId { get; }
    public string ContractVersion { get; }

    public ValueTask<AdapterRenderResult> RenderAsync(
        ArtifactRenderingContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var artifact = context.Artifact;
        if (!templates.TryGetValue(artifact.Ownership.TemplateId, out var source))
        {
            return Failure("rendering.template.unresolved", "The proposed artifact's pinned template was not supplied.");
        }

        if (!StringComparer.Ordinal.Equals(source.Digest, artifact.TemplateDigest))
        {
            return Failure("rendering.template.digest-mismatch", "The supplied template does not match the generation plan.");
        }

        if (source.Content.Length > limits.MaximumTemplateCharacters)
        {
            return Failure("rendering.limit.template-size", "The template exceeded the configured character limit.");
        }

        var template = Template.Parse(source.Content, artifact.Ownership.TemplateId);
        if (template.HasErrors)
        {
            return Failure("rendering.template.invalid", "The template could not be parsed.");
        }

        try
        {
            var globals = new ScriptObject
            {
                { "artifact_id", artifact.ArtifactId },
                { "logical_path", artifact.LogicalPath },
                { "owner", artifact.Ownership.Owner },
                { "pack_id", artifact.Ownership.PackId },
                { "pack_version", artifact.Ownership.PackVersion },
                { "template_id", artifact.Ownership.TemplateId },
                { "plan_digest", context.Plan.Digest },
                { "input_digest", artifact.InputDigest },
                { "semantic_inputs", artifact.SemanticInputs.Select(input => new ScriptObject
                    {
                        { "id", input.Id },
                        { "context_id", input.ContextId },
                        { "semantic_digest", input.SemanticDigest }
                    }).ToArray() }
            };
            if (globalsProvider is not null)
            {
                foreach (var value in globalsProvider.GetGlobals(context))
                {
                    globals.Add(value.Key, value.Value);
                }
            }
            var templateContext = new TemplateContext
            {
                StrictVariables = true,
                LoopLimit = limits.MaximumLoopIterations,
                RecursiveLimit = limits.MaximumRecursiveDepth
            };
            templateContext.PushGlobal(globals);
            var content = template.Render(templateContext);
            cancellationToken.ThrowIfCancellationRequested();
            if (System.Text.Encoding.UTF8.GetByteCount(content) > limits.MaximumOutputBytes)
            {
                return Failure("rendering.limit.output-size", "The rendered artifact exceeded the configured byte limit.");
            }

            return ValueTask.FromResult(new AdapterRenderResult(content, []));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return Failure("rendering.template.failed", "Template rendering failed for the proposed artifact.");
        }
    }

    private static ValueTask<AdapterRenderResult> Failure(string code, string message) =>
        ValueTask.FromResult(new AdapterRenderResult(null, [new RenderingDiagnostic(code, message)]));
}
