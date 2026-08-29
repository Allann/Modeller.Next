namespace Modeller.Api.Initiative;

public sealed record CreateInitiativeRequest(string OriginalChangeRequest, string FacilitatorName, string DomainExpertName);

/// <summary>The two role-scoped credentials (issue #146) minted the moment a session starts —
/// <see cref="Facilitator"/> and <see cref="DomainExpert"/> each identify both this session and the
/// bearer's role, and each is only ever handed out once, alongside the sharable link that carries it.</summary>
public sealed record InitiativeCredentialsDto(string Facilitator, string DomainExpert);

/// <summary>Wraps the created session with the two credentials it was just issued, since a plain
/// <see cref="InitiativeSessionDto"/> has nowhere to carry them.</summary>
public sealed record CreateInitiativeResponseDto(InitiativeSessionDto Session, InitiativeCredentialsDto Credentials);

/// <summary>
/// Proposing a question is Facilitator-only (issue #146) — the pipeline derives who proposed it
/// from the caller's own credential and this session's Facilitator participant, never from a
/// client-supplied identity, so a Domain Expert can no longer forge a Facilitator-authored question
/// by echoing the Facilitator's participant ID back from the shared session DTO. If
/// <see cref="Text"/> is omitted, the pipeline asks the configured Agent Advisor to propose one; if
/// none is configured (or it fails), the request is rejected with the failure status so the caller
/// can prompt for manual text instead — see issue #90's "each response uses the Agent Advisor port
/// where AI assistance applies, always able to proceed without it."</summary>
public sealed record ProposeQuestionRequestDto(string Field, string? Text);

public sealed record SubmitResponseRequestDto(string Text);

public sealed record SelectInterventionRequestDto(string Type, string Description, string Rationale, bool ContinuesToDesignWorkspace = false);

public sealed record LinkDesignWorkspaceRequestDto(string Reference);

/// <summary>If <see cref="ManualResults"/> is omitted, the pipeline asks the Agent Advisor to
/// evaluate the gate instead.</summary>
public sealed record RecordGateEvaluationRequestDto(string Kind, IReadOnlyList<GateCheckResultDto>? ManualResults);

public sealed record DismissGateFindingRequestDto(string Check, string? Reason);

public sealed record FinalizeRequestDto(string? Reason = null);

public sealed record AgentInterventionSuggestionDto(string Type, string Description, string Rationale);

public sealed record AgentInterventionSuggestionsResponse(IReadOnlyList<AgentInterventionSuggestionDto> Suggestions);

/// <summary>Public, secret-free state used by the cockpit to explain whether AI actions are available.</summary>
public sealed record AgentAdvisorStatusResponse(bool Available, string? Model, bool RequiresApiKey, string? FreeModel);

/// <summary>A stable error envelope, matching the structured-diagnostic convention already used by
/// <c>WorkspaceEndpoints</c> — never the framework's default empty-bodied 4xx.</summary>
public sealed record InitiativeErrorResponse(string Code, string Message);
