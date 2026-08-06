namespace Modeller.Api.Initiative;

/// <summary>
/// The canonical persisted JSON representation of an Initiative session (issue #90's JSON-file
/// repository). This is also the shape returned to the Facilitator; the Domain Expert gets a
/// filtered projection — see <see cref="InitiativeSessionMapper.ToDomainExpertDto"/> — because,
/// contrary to what earlier issue comments claimed, #88 never dropped Business Statement's
/// role-scoped visibility rule; it only dropped the roles beyond the three v1 keeps.
/// </summary>
public sealed record ParticipantDto(Guid Id, string DisplayName, string Role);

public sealed record QuestionDto(Guid Id, string Text, Guid ProposedBy, string AuthorRole, string Field, string Status);

public sealed record ResponseDto(Guid Id, Guid QuestionId, string Text, string Status);

public sealed record SelectedInterventionDto(
    Guid Id, string Type, string Description, string Rationale, bool ContinuesToDesignWorkspace, string? DesignWorkspaceReference);

public sealed record GateCheckResultDto(string Check, bool Passed, string Reason);

public sealed record GateEvaluationDto(
    string Kind, IReadOnlyList<GateCheckResultDto> Results, Guid? RecommendedQuestionId, DateTimeOffset EvaluatedAt, string AgentStatus);

public sealed record GateOverrideDto(
    Guid Id,
    string Kind,
    string OverrideType,
    GateCheckResultDto? DismissedFinding,
    IReadOnlyList<GateCheckResultDto>? FinalizedFindings,
    string? Reason);

public sealed record FinalizationDto(string Status, string MarkdownSnapshot, DateTimeOffset FinalizedAt);

public sealed record InitiativeSessionDto(
    Guid Id,
    string OriginalChangeRequest,
    IReadOnlyList<ParticipantDto> Participants,
    IReadOnlyList<QuestionDto> Questions,
    IReadOnlyList<ResponseDto> Responses,
    IReadOnlyList<SelectedInterventionDto> SelectedInterventions,
    IReadOnlyList<GateOverrideDto> GateOverrides,
    GateEvaluationDto? LatestDiscoveryGateEvaluation,
    GateEvaluationDto? LatestShapeGateEvaluation,
    FinalizationDto? Finalization);
