namespace Modeller.Initiative;

/// <summary>Discovery Gate sits at Frame -&gt; Shape; Shape Gate sits before finalization. Both are strictly advisory (issue #83/#86).</summary>
public enum GateKind
{
    Discovery,
    Shape,
}

/// <summary>
/// Discovery Gate checks are adapted from Business Statement's Discovery Gate Criteria
/// (docs/vault/Discovery Gate Criteria.md). Shape Gate checks are from issue #83's resolution:
/// every selected Technology intervention needs a stated rationale, and "No action" must have been
/// considered as a baseline before the Initiative is finalized.
/// </summary>
public enum GateCheck
{
    OriginalChangeRequestCaptured,
    ProblemStatementDescribesBusinessProblem,
    AffectedUsersNamed,
    PainPointsAreConcrete,
    OutcomesAreObservable,
    SuccessCriteriaAreUnderstandable,
    NonGoalsAreListed,
    ConstraintsAreListed,
    AssumptionsAreListed,
    OpenQuestionsAreListed,
    RisksAreListed,
    NoUnresolvedSolutionLedLanguage,

    SelectedTechnologyInterventionsHaveRationale,
    NoActionWasConsidered,
}

public sealed record GateCheckResult(GateCheck Check, bool Passed, string Reason);

/// <summary>
/// Mirrors Business Statement's <c>AgentEvaluationStatus</c>: a gate evaluation can be produced by a
/// human Facilitator with no AI involved at all (<see cref="Ready"/> covers both — the domain does not
/// distinguish who produced the evaluation, only whether one exists), or it can record why an Agent
/// Advisor (issue #89) failed to produce one, so the caller can degrade gracefully instead of blocking.
/// </summary>
public enum AgentEvaluationStatus
{
    Ready,
    EndpointUnavailable,
    TimedOut,
    InvalidResponse,
    ModelError,
    ConfigurationError,
    RequestFailed,

    /// <summary>No <see cref="IAgentAdvisor"/> is wired in at all — the intentional, by-design state
    /// for a fully human-only Initiative (issue #89), distinct from an AI that is configured but
    /// failing.</summary>
    NotConfigured,
}

public sealed record GateEvaluation(
    GateKind Kind,
    IReadOnlyList<GateCheckResult> Results,
    QuestionId? RecommendedQuestionId,
    DateTimeOffset EvaluatedAt,
    AgentEvaluationStatus AgentStatus)
{
    public bool AllPassed => Results.All(result => result.Passed);
}
