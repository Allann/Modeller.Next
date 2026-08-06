namespace Modeller.Api.Initiative;

public sealed record CreateInitiativeRequest(string OriginalChangeRequest, string FacilitatorName, string DomainExpertName);

/// <summary>If <see cref="Text"/> is omitted, the pipeline asks the configured Agent Advisor to
/// propose one; if none is configured (or it fails), the request is rejected with the failure status
/// so the caller can prompt for manual text instead — see issue #90's "each response uses the Agent
/// Advisor port where AI assistance applies, always able to proceed without it."</summary>
public sealed record ProposeQuestionRequestDto(Guid ProposedBy, string AuthorRole, string Field, string? Text);

public sealed record SubmitResponseRequestDto(string Text);

public sealed record SelectInterventionRequestDto(string Type, string Description, string Rationale);

public sealed record LinkDesignWorkspaceRequestDto(string Reference);

/// <summary>If <see cref="ManualResults"/> is omitted, the pipeline asks the Agent Advisor to
/// evaluate the gate instead.</summary>
public sealed record RecordGateEvaluationRequestDto(string Kind, IReadOnlyList<GateCheckResultDto>? ManualResults);

public sealed record DismissGateFindingRequestDto(string Check, string? Reason);

public sealed record FinalizeRequestDto(string? Reason = null);

public sealed record AgentInterventionSuggestionDto(string Type, string Description, string Rationale);

public sealed record AgentInterventionSuggestionsResponse(IReadOnlyList<AgentInterventionSuggestionDto> Suggestions);

/// <summary>A stable error envelope, matching the structured-diagnostic convention already used by
/// <c>WorkspaceEndpoints</c> — never the framework's default empty-bodied 4xx.</summary>
public sealed record InitiativeErrorResponse(string Code, string Message);
