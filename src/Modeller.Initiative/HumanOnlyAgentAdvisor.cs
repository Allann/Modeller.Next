namespace Modeller.Initiative;

/// <summary>
/// The default <see cref="IAgentAdvisor"/>: no AI is configured, by design. Every method returns
/// <see cref="AgentEvaluationStatus.NotConfigured"/> immediately, with no I/O, so a Facilitator can run
/// a complete Discover -&gt; Frame -&gt; Shape Initiative — proposing every question, drafting every field
/// update, and evaluating both gates by hand — without ever wiring in a real AI adapter. This is the
/// implementation that must exist and be exercised for issue #83/#86's "AI must be a pluggable add-on,
/// never a hard dependency" requirement to actually hold, not just be a stated intention. Mirrors
/// this repo's existing <c>DeterministicAiProposalProvider</c> pattern (src/Modeller.AI/AiProposals.cs).
/// </summary>
public sealed class HumanOnlyAgentAdvisor : IAgentAdvisor
{
    private const string Reason = "No Agent Advisor is configured for this Initiative; proceed manually.";

    public Task<AgentAdvisorResult<AgentQuestionSuggestion>> ProposeQuestionAsync(
        ProposeQuestionRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(AgentAdvisorResult<AgentQuestionSuggestion>.Failure(AgentEvaluationStatus.NotConfigured, Reason));

    public Task<AgentAdvisorResult<AgentFieldUpdateSuggestion>> DraftFieldUpdateAsync(
        DraftFieldUpdateRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(AgentAdvisorResult<AgentFieldUpdateSuggestion>.Failure(AgentEvaluationStatus.NotConfigured, Reason));

    public Task<AgentAdvisorResult<AgentInterventionSuggestions>> ProposeInterventionsAsync(
        ProposeInterventionsRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(AgentAdvisorResult<AgentInterventionSuggestions>.Failure(AgentEvaluationStatus.NotConfigured, Reason));

    public Task<AgentAdvisorResult<AgentGateEvaluationSuggestion>> EvaluateGateAsync(
        GateEvaluationRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(AgentAdvisorResult<AgentGateEvaluationSuggestion>.Failure(AgentEvaluationStatus.NotConfigured, Reason));
}
