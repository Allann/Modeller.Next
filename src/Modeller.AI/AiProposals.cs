using System.Collections.Immutable;
using Modeller.Model;
using Modeller.Validation;

namespace Modeller.AI;

public interface IAiProposalProvider
{
    string Id { get; }
    string Version { get; }
    ValueTask<ProviderProposal> ProposeAsync(AiProviderRequest request, CancellationToken cancellationToken = default);
}

public sealed record AiProposalRequest(AuthoredContextRevision Revision, long ExpectedRevision, string Intent, ImmutableArray<SemanticId> PermittedConcepts);
public sealed record AiProviderRequest(string Intent, string ContextName, long Revision, ImmutableArray<AiConceptSummary> Concepts);
public sealed record AiConceptSummary(SemanticId Id, string Kind, string Name, string Slug);
public sealed record ProviderProposal(ImmutableArray<ModelOperation> Operations, string Explanation);
public sealed record AiProposalProvenance(string ProviderId, string ProviderVersion, long SourceRevision);
public sealed record AiProposalDiagnostic(string Code, string Message);
public sealed record AiProposal(string Id, long SourceRevision, ImmutableArray<ModelOperation> Operations, string Explanation, AiProposalProvenance Provenance);
public sealed record AiProposalResult(AiProposal? Proposal, ImmutableArray<AiProposalDiagnostic> Diagnostics)
{ 
    public bool IsSuccess => Proposal is not null && Diagnostics.IsEmpty; 
}

public sealed record AiAcceptanceResult(AuthoredContextRevision Revision, ImmutableArray<AiProposalDiagnostic> Diagnostics)
{ 
    public bool IsSuccess => Diagnostics.IsEmpty; 
}

public static class AiProposalWorkflow
{
    private const int MaximumOperations = 64;

    public static async ValueTask<AiProposalResult> ProposeAsync(
        AiProposalRequest request,
        IAiProposalProvider provider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(provider);
        if (cancellationToken.IsCancellationRequested) return Failure("ai.proposal.cancelled", "AI proposal was cancelled.");
        if (request.ExpectedRevision != request.Revision.Revision) return Failure("ai.proposal.stale-revision", "The semantic revision changed before proposal generation.");
        if (string.IsNullOrWhiteSpace(request.Intent)) return Failure("ai.proposal.intent-required", "Explicit user intent is required.");
        var permitted = request.PermittedConcepts.ToImmutableHashSet();
        var summaries = permitted.Select(request.Revision.FindConcept)
            .Where(concept => concept is not null)
            .Select(concept => new AiConceptSummary(concept!.Id, concept.Kind.ToString(), concept.Name.Value, concept.Slug.Value))
            .ToImmutableArray();
        ProviderProposal response;
        try { response = await provider.ProposeAsync(new(request.Intent.Trim(), request.Revision.Name.Value, request.Revision.Revision, summaries), cancellationToken); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return Failure("ai.proposal.cancelled", "AI proposal was cancelled."); }
        catch (Exception) { return Failure("ai.provider.failed", "The AI provider failed without producing a trusted proposal."); }
        if (response.Operations.IsDefault || response.Operations.Length == 0 || response.Operations.Length > MaximumOperations)
            return Failure("ai.proposal.operations-invalid", "The provider returned an empty or excessive operation set.");
        if (response.Operations.Any(operation => !TouchesOnly(operation, permitted)))
            return Failure("ai.proposal.scope-violation", "The provider proposed an operation outside the permitted semantic scope.");
        var applied = CanonicalModel.Apply(request.Revision, response.Operations.AsSpan());
        if (!applied.Succeeded) return Failure("ai.proposal.operation-invalid", "The proposed operations are not valid for the canonical model.");
        var validation = SemanticValidation.Validate(ValidationRequest.For(applied.Revision, ValidationProfile.Canonical), cancellationToken);
        if (!validation.IsValid) return Failure("ai.proposal.validation-failed", "The proposed model failed canonical validation.");
        var id = $"ai:{provider.Id}:{request.Revision.Revision}:{response.Operations.Length}";
        return new(new(id, request.Revision.Revision, response.Operations, response.Explanation ?? string.Empty,
            new(provider.Id, provider.Version, request.Revision.Revision)), []);
    }

    public static AiAcceptanceResult Accept(AuthoredContextRevision revision, AiProposal proposal)
    {
        if (revision.Revision != proposal.SourceRevision)
            return new(revision, [new("ai.acceptance.stale-revision", "The semantic revision changed; review a new proposal.")]);
        var applied = CanonicalModel.Apply(revision, proposal.Operations.AsSpan());
        return applied.Succeeded
            ? new(applied.Revision, [])
            : new(revision, [new("ai.acceptance.operation-invalid", "The accepted proposal is no longer valid.")]);
    }

    private static bool TouchesOnly(ModelOperation operation, ImmutableHashSet<SemanticId> permitted) => operation switch
    {
        RenameConcept rename => permitted.Contains(rename.ConceptId),
        DeleteConcept delete => permitted.Contains(delete.ConceptId),
        AddDefinition => true,
        _ => false
    };
    private static AiProposalResult Failure(string code, string message) => new(null, [new(code, message)]);
}

public sealed class DeterministicAiProposalProvider(
    string id,
    string version,
    Func<AiProviderRequest, ProviderProposal> propose) : IAiProposalProvider
{
    public string Id { get; } = id;
    public string Version { get; } = version;
    public ValueTask<ProviderProposal> ProposeAsync(AiProviderRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(propose(request));
    }
}
