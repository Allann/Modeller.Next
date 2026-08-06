namespace Modeller.Api.Initiative;

/// <summary>
/// The canonical persisted-and-wire JSON representation of an Initiative session. One DTO serves
/// both storage (issue #90's JSON-file repository) and the API's read model (GET .../initiative/{id})
/// — v1 dropped Business Statement's role-scoped visibility filtering (see issue #88's "explicitly
/// not built here" list), so there's no reason to keep persistence and wire shapes separate yet.
/// </summary>
public sealed record ParticipantDto(Guid Id, string DisplayName, string Role);

public sealed record QuestionDto(Guid Id, string Text, Guid ProposedBy, string AuthorRole, string Field, string Status);

public sealed record ResponseDto(Guid Id, Guid QuestionId, string Text, string Status);

public sealed record SelectedInterventionDto(Guid Id, string Type, string Description, string Rationale, string? DesignWorkspaceReference);

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
