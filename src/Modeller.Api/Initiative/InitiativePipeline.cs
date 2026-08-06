using Microsoft.AspNetCore.SignalR;
using Modeller.Initiative;

namespace Modeller.Api.Initiative;

/// <summary>The HTTP status code to send alongside a response body — mirrors
/// <see cref="Modeller.Api.PipelineResult"/>'s shape for the workspace pipeline.</summary>
public sealed record ApiResult(object Body, int StatusCode);

/// <summary>Either an updated session to persist and return, or an early result (a validation
/// failure, an Agent Advisor degradation) that must not be persisted.</summary>
public sealed record InitiativeMutationOutcome(InitiativeSession? Session, ApiResult? EarlyResult)
{
    public static InitiativeMutationOutcome Success(InitiativeSession session) => new(session, null);

    public static InitiativeMutationOutcome Early(ApiResult result) => new(null, result);
}

/// <summary>
/// The Initiative command pipeline: load, mutate the aggregate from #88, consult the Agent Advisor
/// from #89 where AI assistance applies (always able to proceed without it), persist, broadcast.
/// Holding this here — not in the endpoint handlers — keeps <see cref="InitiativeEndpoints"/> a thin,
/// declarative route list, matching <c>WorkspaceAnalysisPipeline</c>'s existing separation, and makes
/// the pipeline independently unit-testable without an HTTP host.
///
/// #88's <c>InitiativeSession</c> is immutable — every mutator returns a *new* session rather than
/// changing the loaded one in place — so every method here follows the same shape: load, call a
/// domain method and capture its returned session, persist that returned session.
/// </summary>
public sealed class InitiativePipeline(
    IInitiativeSessionRepository repository,
    IAgentAdvisor advisor,
    IHubContext<InitiativeHub> hub,
    ILogger<InitiativePipeline> logger)
{
    public async Task<ApiResult> CreateAsync(CreateInitiativeRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.OriginalChangeRequest)
            || string.IsNullOrWhiteSpace(request.FacilitatorName)
            || string.IsNullOrWhiteSpace(request.DomainExpertName))
        {
            return Invalid("An original change request, a facilitator name, and a domain expert name are all required.");
        }

        var session = InitiativeSession.CreateNew(request.OriginalChangeRequest);
        session = session.AddParticipant(Participant.CreateNew(request.FacilitatorName, ParticipantRole.Facilitator));
        session = session.AddParticipant(Participant.CreateNew(request.DomainExpertName, ParticipantRole.DomainExpert));
        await repository.SaveAsync(session, cancellationToken);
        logger.LogInformation("Created Initiative {InitiativeId}", session.Id);
        return Ok(session);
    }

    /// <summary>
    /// <paramref name="viewerRole"/> selects the projection: omitted or <c>Facilitator</c> returns
    /// the full session; <c>DomainExpert</c> returns the role-scoped view (see
    /// <see cref="InitiativeSessionMapper.ToDomainExpertDto"/>) the Domain Expert's page renders.
    /// </summary>
    public async Task<ApiResult> GetAsync(Guid id, string? viewerRole, CancellationToken cancellationToken)
    {
        var session = await repository.LoadAsync(InitiativeId.FromExisting(id), cancellationToken);
        if (session is null) return NotFound(id);

        if (string.Equals(viewerRole, "DomainExpert", StringComparison.OrdinalIgnoreCase))
        {
            return new ApiResult(InitiativeSessionMapper.ToDomainExpertDto(session), StatusCodes.Status200OK);
        }

        return Ok(session);
    }

    public Task<ApiResult> ProposeQuestionAsync(Guid id, ProposeQuestionRequestDto request, CancellationToken cancellationToken) =>
        ExecuteAsync(id, cancellationToken, async session =>
        {
            if (!Enum.TryParse<ParticipantRole>(request.AuthorRole, out var authorRole))
                return InitiativeMutationOutcome.Early(Invalid($"'{request.AuthorRole}' is not a recognised participant role."));
            if (!Enum.TryParse<InitiativeField>(request.Field, out var field))
                return InitiativeMutationOutcome.Early(Invalid($"'{request.Field}' is not a recognised Initiative field."));

            var text = request.Text;
            if (string.IsNullOrWhiteSpace(text))
            {
                var suggestion = await advisor.ProposeQuestionAsync(
                    new Modeller.Initiative.ProposeQuestionRequest(session.OriginalChangeRequest, session.BuildStructuredFields(), field),
                    cancellationToken);
                if (!suggestion.Succeeded) return InitiativeMutationOutcome.Early(AgentUnavailable(suggestion.Status, suggestion.FailureReason));
                text = suggestion.Value!.Text;
            }

            var (updated, _) = session.ProposeQuestion(text, ParticipantId.FromExisting(request.ProposedBy), authorRole, field);
            return InitiativeMutationOutcome.Success(updated);
        });

    public Task<ApiResult> SendQuestionAsync(Guid id, Guid questionId, CancellationToken cancellationToken) =>
        ExecuteAsync(id, cancellationToken, session =>
            Task.FromResult(InitiativeMutationOutcome.Success(session.SendQuestion(QuestionId.FromExisting(questionId)))));

    public Task<ApiResult> RejectQuestionAsync(Guid id, Guid questionId, CancellationToken cancellationToken) =>
        ExecuteAsync(id, cancellationToken, session =>
            Task.FromResult(InitiativeMutationOutcome.Success(session.RejectProposedQuestion(QuestionId.FromExisting(questionId)))));

    public Task<ApiResult> SubmitResponseAsync(Guid id, Guid questionId, SubmitResponseRequestDto request, CancellationToken cancellationToken) =>
        ExecuteAsync(id, cancellationToken, session =>
        {
            if (string.IsNullOrWhiteSpace(request.Text))
                return Task.FromResult(InitiativeMutationOutcome.Early(Invalid("A response requires non-empty text.")));

            var (updated, _) = session.SubmitResponse(QuestionId.FromExisting(questionId), request.Text);
            return Task.FromResult(InitiativeMutationOutcome.Success(updated));
        });

    public Task<ApiResult> AcceptResponseAsync(Guid id, Guid responseId, CancellationToken cancellationToken) =>
        ExecuteAsync(id, cancellationToken, session =>
            Task.FromResult(InitiativeMutationOutcome.Success(session.AcceptResponse(ResponseId.FromExisting(responseId)))));

    public async Task<ApiResult> GetInterventionSuggestionsAsync(Guid id, CancellationToken cancellationToken)
    {
        var session = await repository.LoadAsync(InitiativeId.FromExisting(id), cancellationToken);
        if (session is null) return NotFound(id);

        var suggestions = await advisor.ProposeInterventionsAsync(new ProposeInterventionsRequest(session.BuildStructuredFields()), cancellationToken);
        if (!suggestions.Succeeded) return AgentUnavailable(suggestions.Status, suggestions.FailureReason);

        return Ok(new AgentInterventionSuggestionsResponse(
            [.. suggestions.Value!.Suggestions.Select(s => new AgentInterventionSuggestionDto(s.Type.ToString(), s.Description, s.Rationale))]));
    }

    public Task<ApiResult> SelectInterventionAsync(Guid id, SelectInterventionRequestDto request, CancellationToken cancellationToken) =>
        ExecuteAsync(id, cancellationToken, session =>
        {
            if (!Enum.TryParse<InterventionType>(request.Type, out var type))
                return Task.FromResult(InitiativeMutationOutcome.Early(Invalid($"'{request.Type}' is not a recognised intervention type.")));

            var (updated, _) = session.SelectIntervention(type, request.Description, request.Rationale, request.ContinuesToDesignWorkspace);
            return Task.FromResult(InitiativeMutationOutcome.Success(updated));
        });

    public Task<ApiResult> WithdrawInterventionAsync(Guid id, Guid interventionId, CancellationToken cancellationToken) =>
        ExecuteAsync(id, cancellationToken, session =>
            Task.FromResult(InitiativeMutationOutcome.Success(session.WithdrawIntervention(InterventionId.FromExisting(interventionId)))));

    public Task<ApiResult> LinkDesignWorkspaceAsync(Guid id, Guid interventionId, LinkDesignWorkspaceRequestDto request, CancellationToken cancellationToken) =>
        ExecuteAsync(id, cancellationToken, session =>
            Task.FromResult(InitiativeMutationOutcome.Success(session.LinkDesignWorkspace(InterventionId.FromExisting(interventionId), request.Reference))));

    public Task<ApiResult> RecordGateEvaluationAsync(Guid id, RecordGateEvaluationRequestDto request, CancellationToken cancellationToken) =>
        ExecuteAsync(id, cancellationToken, async session =>
        {
            if (!Enum.TryParse<GateKind>(request.Kind, out var kind))
                return InitiativeMutationOutcome.Early(Invalid($"'{request.Kind}' is not a recognised gate."));

            IReadOnlyList<GateCheckResult> results;
            string? recommendedText = null;
            InitiativeField? recommendedField = null;

            if (request.ManualResults is { Count: > 0 } manual)
            {
                var parsed = new List<GateCheckResult>();
                foreach (var result in manual)
                {
                    if (!Enum.TryParse<GateCheck>(result.Check, out var check))
                        return InitiativeMutationOutcome.Early(Invalid($"'{result.Check}' is not a recognised gate check."));
                    parsed.Add(new GateCheckResult(check, result.Passed, result.Reason));
                }

                results = parsed;
            }
            else
            {
                var suggestion = await advisor.EvaluateGateAsync(new GateEvaluationRequest(kind, session.BuildStructuredFields()), cancellationToken);
                if (!suggestion.Succeeded) return InitiativeMutationOutcome.Early(AgentUnavailable(suggestion.Status, suggestion.FailureReason));
                results = suggestion.Value!.Results;
                recommendedText = suggestion.Value.RecommendedQuestionText;
                recommendedField = suggestion.Value.RecommendedQuestionField;
            }

            var evaluation = new GateEvaluation(kind, results, null, DateTimeOffset.UtcNow, AgentEvaluationStatus.Ready);
            var updated = session.RecordGateEvaluation(evaluation, recommendedText, recommendedField);
            return InitiativeMutationOutcome.Success(updated);
        });

    public Task<ApiResult> DismissGateFindingAsync(Guid id, string kind, DismissGateFindingRequestDto request, CancellationToken cancellationToken) =>
        ExecuteAsync(id, cancellationToken, session =>
        {
            if (!Enum.TryParse<GateKind>(kind, out var gateKind))
                return Task.FromResult(InitiativeMutationOutcome.Early(Invalid($"'{kind}' is not a recognised gate.")));
            if (!Enum.TryParse<GateCheck>(request.Check, out var check))
                return Task.FromResult(InitiativeMutationOutcome.Early(Invalid($"'{request.Check}' is not a recognised gate check.")));

            var (updated, _) = session.DismissGateFinding(gateKind, check, request.Reason);
            return Task.FromResult(InitiativeMutationOutcome.Success(updated));
        });

    public Task<ApiResult> FinalizeAsync(Guid id, FinalizeRequestDto request, CancellationToken cancellationToken) =>
        ExecuteAsync(id, cancellationToken, session =>
            Task.FromResult(InitiativeMutationOutcome.Success(session.FinalizeInitiative(DateTimeOffset.UtcNow, request.Reason))));

    public Task<ApiResult> ReopenAsync(Guid id, CancellationToken cancellationToken) =>
        ExecuteAsync(id, cancellationToken, session => Task.FromResult(InitiativeMutationOutcome.Success(session.Reopen())));

    /// <summary>
    /// Loads, applies <paramref name="mutate"/>, and — only for a <see cref="InitiativeMutationOutcome.Success"/>
    /// outcome — persists the *returned* session (never the one that was loaded) and broadcasts. A
    /// domain invariant violation (<see cref="InvalidOperationException"/> or <see cref="ArgumentException"/>)
    /// becomes a 400 rather than an unhandled 500, since these are exactly the exceptions #88's
    /// aggregate raises for a disallowed transition.
    /// </summary>
    private async Task<ApiResult> ExecuteAsync(Guid id, CancellationToken cancellationToken, Func<InitiativeSession, Task<InitiativeMutationOutcome>> mutate)
    {
        var session = await repository.LoadAsync(InitiativeId.FromExisting(id), cancellationToken);
        if (session is null) return NotFound(id);

        InitiativeMutationOutcome outcome;
        try
        {
            outcome = await mutate(session);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return Invalid(ex.Message);
        }

        if (outcome.EarlyResult is not null) return outcome.EarlyResult;

        var updated = outcome.Session!;
        await repository.SaveAsync(updated, cancellationToken);
        await hub.Clients.Group(InitiativeHub.GroupName(id)).SendAsync(InitiativeHub.SessionUpdated, id, cancellationToken);
        return Ok(updated);
    }

    private static ApiResult Ok(InitiativeSession session) => new(InitiativeSessionMapper.ToDto(session), StatusCodes.Status200OK);

    private static ApiResult Ok(object body) => new(body, StatusCodes.Status200OK);

    private static ApiResult NotFound(Guid id) =>
        new(new InitiativeErrorResponse("initiative.not_found", $"No Initiative session '{id}' was found."), StatusCodes.Status404NotFound);

    private static ApiResult Invalid(string message) =>
        new(new InitiativeErrorResponse("initiative.request.invalid", message), StatusCodes.Status400BadRequest);

    private static ApiResult AgentUnavailable(AgentEvaluationStatus status, string? reason) =>
        new(new InitiativeErrorResponse($"initiative.agent.{status}", reason ?? "The Agent Advisor did not produce a suggestion."),
            StatusCodes.Status422UnprocessableEntity);
}
