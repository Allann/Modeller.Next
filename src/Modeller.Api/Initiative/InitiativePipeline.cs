using Microsoft.AspNetCore.SignalR;
using Modeller.Initiative;

namespace Modeller.Api.Initiative;

/// <summary>The HTTP status code to send alongside a response body — mirrors
/// <see cref="Modeller.Api.PipelineResult"/>'s shape for the workspace pipeline.</summary>
public sealed record ApiResult(object Body, int StatusCode);

/// <summary>
/// The Initiative command pipeline: load, mutate the aggregate from #88, consult the Agent Advisor
/// from #89 where AI assistance applies (always able to proceed without it), persist, broadcast.
/// Holding this here — not in the endpoint handlers — keeps <see cref="InitiativeEndpoints"/> a thin,
/// declarative route list, matching <c>WorkspaceAnalysisPipeline</c>'s existing separation, and makes
/// the pipeline independently unit-testable without an HTTP host.
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

        var session = InitiativeSession.Create(InitiativeId.New(), request.OriginalChangeRequest);
        session.AddParticipant(new Participant(ParticipantId.New(), request.FacilitatorName, ParticipantRole.Facilitator));
        session.AddParticipant(new Participant(ParticipantId.New(), request.DomainExpertName, ParticipantRole.DomainExpert));
        await repository.SaveAsync(session, cancellationToken);
        logger.LogInformation("Created Initiative {InitiativeId}", session.Id);
        return Ok(session);
    }

    public async Task<ApiResult> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var session = await repository.LoadAsync(InitiativeId.FromExisting(id), cancellationToken);
        return session is null ? NotFound(id) : Ok(session);
    }

    public Task<ApiResult> ProposeQuestionAsync(Guid id, ProposeQuestionRequestDto request, CancellationToken cancellationToken) =>
        ExecuteAsync(id, cancellationToken, async session =>
        {
            if (!Enum.TryParse<ParticipantRole>(request.AuthorRole, out var authorRole))
                return Invalid($"'{request.AuthorRole}' is not a recognised participant role.");
            if (!Enum.TryParse<InitiativeField>(request.Field, out var field))
                return Invalid($"'{request.Field}' is not a recognised Initiative field.");

            var text = request.Text;
            if (string.IsNullOrWhiteSpace(text))
            {
                var suggestion = await advisor.ProposeQuestionAsync(
                    new Modeller.Initiative.ProposeQuestionRequest(session.OriginalChangeRequest, session.BuildStructuredFields(), field),
                    cancellationToken);
                if (!suggestion.Succeeded) return AgentUnavailable(suggestion.Status, suggestion.FailureReason);
                text = suggestion.Value!.Text;
            }

            session.ProposeQuestion(text, ParticipantId.FromExisting(request.ProposedBy), authorRole, field);
            return null;
        });

    public Task<ApiResult> SendQuestionAsync(Guid id, Guid questionId, CancellationToken cancellationToken) =>
        ExecuteAsync(id, cancellationToken, session =>
        {
            session.SendQuestion(QuestionId.FromExisting(questionId));
            return Task.FromResult<ApiResult?>(null);
        });

    public Task<ApiResult> RejectQuestionAsync(Guid id, Guid questionId, CancellationToken cancellationToken) =>
        ExecuteAsync(id, cancellationToken, session =>
        {
            session.RejectProposedQuestion(QuestionId.FromExisting(questionId));
            return Task.FromResult<ApiResult?>(null);
        });

    public Task<ApiResult> SubmitResponseAsync(Guid id, Guid questionId, SubmitResponseRequestDto request, CancellationToken cancellationToken) =>
        ExecuteAsync(id, cancellationToken, session =>
        {
            if (string.IsNullOrWhiteSpace(request.Text)) return Task.FromResult<ApiResult?>(Invalid("A response requires non-empty text."));
            session.SubmitResponse(QuestionId.FromExisting(questionId), request.Text);
            return Task.FromResult<ApiResult?>(null);
        });

    public Task<ApiResult> AcceptResponseAsync(Guid id, Guid responseId, CancellationToken cancellationToken) =>
        ExecuteAsync(id, cancellationToken, session =>
        {
            session.AcceptResponse(ResponseId.FromExisting(responseId));
            return Task.FromResult<ApiResult?>(null);
        });

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
                return Task.FromResult<ApiResult?>(Invalid($"'{request.Type}' is not a recognised intervention type."));

            session.SelectIntervention(type, request.Description, request.Rationale);
            return Task.FromResult<ApiResult?>(null);
        });

    public Task<ApiResult> WithdrawInterventionAsync(Guid id, Guid interventionId, CancellationToken cancellationToken) =>
        ExecuteAsync(id, cancellationToken, session =>
        {
            session.WithdrawIntervention(InterventionId.FromExisting(interventionId));
            return Task.FromResult<ApiResult?>(null);
        });

    public Task<ApiResult> LinkDesignWorkspaceAsync(Guid id, Guid interventionId, LinkDesignWorkspaceRequestDto request, CancellationToken cancellationToken) =>
        ExecuteAsync(id, cancellationToken, session =>
        {
            session.LinkDesignWorkspace(InterventionId.FromExisting(interventionId), request.Reference);
            return Task.FromResult<ApiResult?>(null);
        });

    public Task<ApiResult> RecordGateEvaluationAsync(Guid id, RecordGateEvaluationRequestDto request, CancellationToken cancellationToken) =>
        ExecuteAsync(id, cancellationToken, async session =>
        {
            if (!Enum.TryParse<GateKind>(request.Kind, out var kind))
                return Invalid($"'{request.Kind}' is not a recognised gate.");

            IReadOnlyList<GateCheckResult> results;
            string? recommendedText = null;
            InitiativeField? recommendedField = null;

            if (request.ManualResults is { Count: > 0 } manual)
            {
                var parsed = new List<GateCheckResult>();
                foreach (var result in manual)
                {
                    if (!Enum.TryParse<GateCheck>(result.Check, out var check))
                        return Invalid($"'{result.Check}' is not a recognised gate check.");
                    parsed.Add(new GateCheckResult(check, result.Passed, result.Reason));
                }

                results = parsed;
            }
            else
            {
                var suggestion = await advisor.EvaluateGateAsync(new GateEvaluationRequest(kind, session.BuildStructuredFields()), cancellationToken);
                if (!suggestion.Succeeded) return AgentUnavailable(suggestion.Status, suggestion.FailureReason);
                results = suggestion.Value!.Results;
                recommendedText = suggestion.Value.RecommendedQuestionText;
                recommendedField = suggestion.Value.RecommendedQuestionField;
            }

            var evaluation = new GateEvaluation(kind, results, null, DateTimeOffset.UtcNow, AgentEvaluationStatus.Ready);
            session.RecordGateEvaluation(evaluation, recommendedText, recommendedField);
            return null;
        });

    public Task<ApiResult> DismissGateFindingAsync(Guid id, string kind, DismissGateFindingRequestDto request, CancellationToken cancellationToken) =>
        ExecuteAsync(id, cancellationToken, session =>
        {
            if (!Enum.TryParse<GateKind>(kind, out var gateKind))
                return Task.FromResult<ApiResult?>(Invalid($"'{kind}' is not a recognised gate."));
            if (!Enum.TryParse<GateCheck>(request.Check, out var check))
                return Task.FromResult<ApiResult?>(Invalid($"'{request.Check}' is not a recognised gate check."));

            session.DismissGateFinding(gateKind, check, request.Reason);
            return Task.FromResult<ApiResult?>(null);
        });

    public Task<ApiResult> FinalizeAsync(Guid id, FinalizeRequestDto request, CancellationToken cancellationToken) =>
        ExecuteAsync(id, cancellationToken, session =>
        {
            session.FinalizeInitiative(DateTimeOffset.UtcNow, request.Reason);
            return Task.FromResult<ApiResult?>(null);
        });

    public Task<ApiResult> ReopenAsync(Guid id, CancellationToken cancellationToken) =>
        ExecuteAsync(id, cancellationToken, session =>
        {
            session.Reopen();
            return Task.FromResult<ApiResult?>(null);
        });

    /// <summary>
    /// Loads, applies <paramref name="mutate"/>, and — only if it neither returned an early result nor
    /// threw — persists and broadcasts. A domain invariant violation (<see cref="InvalidOperationException"/>
    /// or <see cref="ArgumentException"/>) becomes a 400 rather than an unhandled 500, since these are
    /// exactly the exceptions #88's aggregate raises for a disallowed transition.
    /// </summary>
    private async Task<ApiResult> ExecuteAsync(Guid id, CancellationToken cancellationToken, Func<InitiativeSession, Task<ApiResult?>> mutate)
    {
        var session = await repository.LoadAsync(InitiativeId.FromExisting(id), cancellationToken);
        if (session is null) return NotFound(id);

        try
        {
            var earlyResult = await mutate(session);
            if (earlyResult is not null) return earlyResult;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return Invalid(ex.Message);
        }

        await repository.SaveAsync(session, cancellationToken);
        await hub.Clients.Group(InitiativeHub.GroupName(id)).SendAsync(InitiativeHub.SessionUpdated, id, cancellationToken);
        return Ok(session);
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
