namespace Modeller.Initiative;

/// <summary>
/// The Agent Participant's capabilities as a port, kept separate from <see cref="InitiativeSession"/>
/// so the aggregate itself never depends on AI (issue #88's zero-dependency guarantee holds
/// unconditionally). Adapted from Business Statement's <c>IAgentAdvisor</c>
/// (M:\business-statement\src\BusinessStatement.Ports\IAgentAdvisor.cs), broadened to the full set of
/// things the Ubiquitous Language's Agent Participant does — propose questions, draft field updates,
/// propose interventions, evaluate gates — rather than gate evaluation alone.
///
/// Every method returns an <see cref="AgentAdvisorResult{T}"/> instead of throwing for AI-availability
/// failures: an unavailable or misbehaving AI degrades to "no proposal", never breaks the session, per
/// issue #83/#86's requirement that Discover, Frame, and Shape all work fully human-only.
/// </summary>
public interface IAgentAdvisor
{
    Task<AgentAdvisorResult<AgentQuestionSuggestion>> ProposeQuestionAsync(
        ProposeQuestionRequest request, CancellationToken cancellationToken = default);

    Task<AgentAdvisorResult<AgentFieldUpdateSuggestion>> DraftFieldUpdateAsync(
        DraftFieldUpdateRequest request, CancellationToken cancellationToken = default);

    Task<AgentAdvisorResult<AgentInterventionSuggestions>> ProposeInterventionsAsync(
        ProposeInterventionsRequest request, CancellationToken cancellationToken = default);

    Task<AgentAdvisorResult<AgentGateEvaluationSuggestion>> EvaluateGateAsync(
        GateEvaluationRequest request, CancellationToken cancellationToken = default);
}

public sealed record ProposeQuestionRequest(string OriginalChangeRequest, InitiativeStructuredFields CurrentFields, InitiativeField TargetField);

public sealed record AgentQuestionSuggestion(string Text, InitiativeField Field);

public sealed record DraftFieldUpdateRequest(InitiativeField Field, string AcceptedResponseText, IReadOnlyList<string> ExistingEntries);

public sealed record AgentFieldUpdateSuggestion(InitiativeField Field, string DraftText);

public sealed record ProposeInterventionsRequest(InitiativeStructuredFields CurrentFields);

public sealed record AgentInterventionSuggestion(InterventionType Type, string Description, string Rationale);

public sealed record AgentInterventionSuggestions(IReadOnlyList<AgentInterventionSuggestion> Suggestions);

public sealed record GateEvaluationRequest(GateKind Kind, InitiativeStructuredFields CurrentFields);

/// <summary>
/// The advisor's gate-check verdicts, deliberately not a full <see cref="GateEvaluation"/>: only
/// <see cref="InitiativeSession.RecordGateEvaluation"/> can mint the <see cref="QuestionId"/> a
/// recommended follow-up question would need, so this carries the recommendation as plain text/field
/// for the caller to pass straight into that method.
/// </summary>
public sealed record AgentGateEvaluationSuggestion(
    IReadOnlyList<GateCheckResult> Results,
    string? RecommendedQuestionText,
    InitiativeField? RecommendedQuestionField);

/// <summary>
/// Either a successful suggestion, or a reason it's absent. <see cref="AgentEvaluationStatus.Ready"/>
/// with a non-null <see cref="Value"/> is the only success case; every other status carries no value.
/// </summary>
public sealed record AgentAdvisorResult<T>(T? Value, AgentEvaluationStatus Status, string? FailureReason)
{
    public bool Succeeded => Status == AgentEvaluationStatus.Ready && Value is not null;

    public static AgentAdvisorResult<T> Success(T value) => new(value, AgentEvaluationStatus.Ready, null);

    public static AgentAdvisorResult<T> Failure(AgentEvaluationStatus status, string reason)
    {
        if (status == AgentEvaluationStatus.Ready)
        {
            throw new ArgumentException("A failure result cannot use the Ready status.", nameof(status));
        }

        return new(default, status, reason);
    }
}

/// <summary>
/// An adapter's internal signal for a failure it wants to translate into an
/// <see cref="AgentAdvisorResult{T}"/> — never meant to cross the <see cref="IAgentAdvisor"/> boundary
/// itself. <see cref="AgentEvaluationStatus.Ready"/> is never a valid <see cref="FailureKind"/>.
/// </summary>
public sealed class AgentAdvisorException : Exception
{
    public AgentEvaluationStatus FailureKind { get; }

    public AgentAdvisorException(AgentEvaluationStatus failureKind, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        FailureKind = failureKind == AgentEvaluationStatus.Ready
            ? throw new ArgumentException("Ready is not a failure kind.", nameof(failureKind))
            : failureKind;
    }
}
