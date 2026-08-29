using Microsoft.AspNetCore.SignalR;
using Modeller.Api.Analytics;
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
    IProductAnalytics analytics,
    IInitiativeCredentialService credentials,
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
        await analytics.CaptureAsync(ProductEvents.InitiativeCreated, session.Id.Value, cancellationToken: cancellationToken);
        logger.LogInformation("Created Initiative {InitiativeId}", session.Id);

        // Issue #146: the only two times these credentials are ever handed out — the Facilitator's
        // and Domain Expert's sharable links each carry exactly one of them.
        string facilitatorCredential, domainExpertCredential;
        try
        {
            facilitatorCredential = credentials.Mint(session.Id.Value, InitiativeCredentialRole.Facilitator);
            domainExpertCredential = credentials.Mint(session.Id.Value, InitiativeCredentialRole.DomainExpert);
        }
        catch (InvalidOperationException)
        {
            return CredentialServiceMisconfigured();
        }
        return new ApiResult(
            new CreateInitiativeResponseDto(InitiativeSessionMapper.ToDto(session), new InitiativeCredentialsDto(facilitatorCredential, domainExpertCredential)),
            StatusCodes.Status200OK);
    }

    /// <summary>
    /// The projection is derived from the presented credential's own role — never from a
    /// client-supplied role claim (issue #146) — so a Domain Expert credential always gets
    /// <see cref="InitiativeSessionMapper.ToDomainExpertDto"/>'s role-scoped view regardless of what
    /// the request otherwise claims.
    /// </summary>
    public async Task<ApiResult> GetAsync(Guid id, string? credential, CancellationToken cancellationToken)
    {
        var (authorized, error) = await AuthorizeAndLoadAsync(id, credential, requiredRole: null, cancellationToken);
        if (error is not null) return error;
        var (session, role) = authorized!.Value;

        await analytics.CaptureAsync(ProductEvents.InitiativeViewed, id,
            new Dictionary<string, object?> { ["viewer_role"] = role.ToString() }, cancellationToken);

        return role == InitiativeCredentialRole.DomainExpert
            ? new ApiResult(InitiativeSessionMapper.ToDomainExpertDto(session), StatusCodes.Status200OK)
            : Ok(session);
    }

    public Task<ApiResult> ProposeQuestionAsync(Guid id, string? credential, ProposeQuestionRequestDto request, CancellationToken cancellationToken) =>
        ExecuteAsync(id, credential, InitiativeCredentialRole.Facilitator, ProductEvents.QuestionProposed, cancellationToken, async session =>
        {
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

            // The credential already proved the caller is the Facilitator — provenance comes from
            // this session's own Facilitator participant, never from a client-supplied ID/role pair.
            var facilitator = session.Participants.Single(p => p.Role == ParticipantRole.Facilitator);
            var (updated, _) = session.ProposeQuestion(text, facilitator.Id, ParticipantRole.Facilitator, field);
            return InitiativeMutationOutcome.Success(updated);
        });

    public Task<ApiResult> SendQuestionAsync(Guid id, string? credential, Guid questionId, CancellationToken cancellationToken) =>
        ExecuteAsync(id, credential, InitiativeCredentialRole.Facilitator, ProductEvents.QuestionSent, cancellationToken, session =>
            Task.FromResult(InitiativeMutationOutcome.Success(session.SendQuestion(QuestionId.FromExisting(questionId)))));

    public Task<ApiResult> RejectQuestionAsync(Guid id, string? credential, Guid questionId, CancellationToken cancellationToken) =>
        ExecuteAsync(id, credential, InitiativeCredentialRole.Facilitator, null, cancellationToken, session =>
            Task.FromResult(InitiativeMutationOutcome.Success(session.RejectProposedQuestion(QuestionId.FromExisting(questionId)))));

    public Task<ApiResult> SubmitResponseAsync(Guid id, string? credential, Guid questionId, SubmitResponseRequestDto request, CancellationToken cancellationToken) =>
        ExecuteAsync(id, credential, InitiativeCredentialRole.DomainExpert, ProductEvents.ResponseSubmitted, cancellationToken, session =>
        {
            if (string.IsNullOrWhiteSpace(request.Text))
                return Task.FromResult(InitiativeMutationOutcome.Early(Invalid("A response requires non-empty text.")));

            var (updated, _) = session.SubmitResponse(QuestionId.FromExisting(questionId), request.Text);
            return Task.FromResult(InitiativeMutationOutcome.Success(updated));
        });

    public Task<ApiResult> AcceptResponseAsync(Guid id, string? credential, Guid responseId, CancellationToken cancellationToken) =>
        ExecuteAsync(id, credential, InitiativeCredentialRole.Facilitator, ProductEvents.ResponseAccepted, cancellationToken, session =>
            Task.FromResult(InitiativeMutationOutcome.Success(session.AcceptResponse(ResponseId.FromExisting(responseId)))));

    public async Task<ApiResult> GetInterventionSuggestionsAsync(Guid id, string? credential, CancellationToken cancellationToken)
    {
        var (authorized, error) = await AuthorizeAndLoadAsync(id, credential, InitiativeCredentialRole.Facilitator, cancellationToken);
        if (error is not null) return error;
        var session = authorized!.Value.Session;

        var suggestions = await advisor.ProposeInterventionsAsync(new ProposeInterventionsRequest(session.BuildStructuredFields()), cancellationToken);
        if (!suggestions.Succeeded) return AgentUnavailable(suggestions.Status, suggestions.FailureReason);

        return Ok(new AgentInterventionSuggestionsResponse(
            [.. suggestions.Value!.Suggestions.Select(s => new AgentInterventionSuggestionDto(s.Type.ToString(), s.Description, s.Rationale))]));
    }

    public Task<ApiResult> SelectInterventionAsync(Guid id, string? credential, SelectInterventionRequestDto request, CancellationToken cancellationToken) =>
        ExecuteAsync(id, credential, InitiativeCredentialRole.Facilitator, ProductEvents.InterventionSelected, cancellationToken, session =>
        {
            if (!Enum.TryParse<InterventionType>(request.Type, out var type))
                return Task.FromResult(InitiativeMutationOutcome.Early(Invalid($"'{request.Type}' is not a recognised intervention type.")));

            var (updated, _) = session.SelectIntervention(type, request.Description, request.Rationale, request.ContinuesToDesignWorkspace);
            return Task.FromResult(InitiativeMutationOutcome.Success(updated));
        });

    public Task<ApiResult> WithdrawInterventionAsync(Guid id, string? credential, Guid interventionId, CancellationToken cancellationToken) =>
        ExecuteAsync(id, credential, InitiativeCredentialRole.Facilitator, null, cancellationToken, session =>
            Task.FromResult(InitiativeMutationOutcome.Success(session.WithdrawIntervention(InterventionId.FromExisting(interventionId)))));

    public Task<ApiResult> LinkDesignWorkspaceAsync(Guid id, string? credential, Guid interventionId, LinkDesignWorkspaceRequestDto request, CancellationToken cancellationToken) =>
        ExecuteAsync(id, credential, InitiativeCredentialRole.Facilitator, null, cancellationToken, session =>
            Task.FromResult(InitiativeMutationOutcome.Success(session.LinkDesignWorkspace(InterventionId.FromExisting(interventionId), request.Reference))));

    public Task<ApiResult> RecordGateEvaluationAsync(Guid id, string? credential, RecordGateEvaluationRequestDto request, CancellationToken cancellationToken) =>
        ExecuteAsync(id, credential, InitiativeCredentialRole.Facilitator, ProductEvents.GateEvaluated, cancellationToken, async session =>
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

    public Task<ApiResult> DismissGateFindingAsync(Guid id, string? credential, string kind, DismissGateFindingRequestDto request, CancellationToken cancellationToken) =>
        ExecuteAsync(id, credential, InitiativeCredentialRole.Facilitator, null, cancellationToken, session =>
        {
            if (!Enum.TryParse<GateKind>(kind, out var gateKind))
                return Task.FromResult(InitiativeMutationOutcome.Early(Invalid($"'{kind}' is not a recognised gate.")));
            if (!Enum.TryParse<GateCheck>(request.Check, out var check))
                return Task.FromResult(InitiativeMutationOutcome.Early(Invalid($"'{request.Check}' is not a recognised gate check.")));

            var (updated, _) = session.DismissGateFinding(gateKind, check, request.Reason);
            return Task.FromResult(InitiativeMutationOutcome.Success(updated));
        });

    public Task<ApiResult> FinalizeAsync(Guid id, string? credential, FinalizeRequestDto request, CancellationToken cancellationToken) =>
        ExecuteAsync(id, credential, InitiativeCredentialRole.Facilitator, ProductEvents.InitiativeFinalized, cancellationToken, session =>
            Task.FromResult(InitiativeMutationOutcome.Success(session.FinalizeInitiative(DateTimeOffset.UtcNow, request.Reason))));

    public Task<ApiResult> ReopenAsync(Guid id, string? credential, CancellationToken cancellationToken) =>
        ExecuteAsync(id, credential, InitiativeCredentialRole.Facilitator, ProductEvents.InitiativeReopened, cancellationToken,
            session => Task.FromResult(InitiativeMutationOutcome.Success(session.Reopen())));

    /// <summary>An Initiative session loaded after its credential proved <see cref="Role"/> for it.</summary>
    private readonly record struct AuthorizedSession(InitiativeSession Session, InitiativeCredentialRole Role);

    /// <summary>
    /// The one credential-then-load preamble every mutating and read endpoint shares (issue #146):
    /// validate the presented credential against <paramref name="id"/>, optionally require it to
    /// carry <paramref name="requiredRole"/>, and only then load the session. The check is a pure
    /// function of the credential string and the route's session ID (see
    /// <see cref="IInitiativeCredentialService"/>), so a bad, expired, wrong-session, or wrong-role
    /// credential never even reaches the repository. <paramref name="requiredRole"/> is <see langword="null"/>
    /// for <see cref="GetAsync"/>, which accepts either role and instead branches on which one it got.
    /// </summary>
    private async Task<(AuthorizedSession? Authorized, ApiResult? Error)> AuthorizeAndLoadAsync(
        Guid id, string? credential, InitiativeCredentialRole? requiredRole, CancellationToken cancellationToken)
    {
        InitiativeCredentialResult auth;
        try
        {
            auth = credentials.Validate(credential, id);
        }
        catch (InvalidOperationException)
        {
            return (null, CredentialServiceMisconfigured());
        }
        if (!auth.Succeeded) return (null, CredentialError(auth.Failure!.Value));
        if (requiredRole is not null && auth.Role != requiredRole) return (null, WrongRole(requiredRole.Value));

        var session = await repository.LoadAsync(InitiativeId.FromExisting(id), cancellationToken);
        if (session is null) return (null, NotFound(id));

        return (new AuthorizedSession(session, auth.Role!.Value), null);
    }

    /// <summary>
    /// Applies <paramref name="mutate"/> to the session an already-authorized <paramref name="id"/>/
    /// <paramref name="requiredRole"/> pair loaded (see <see cref="AuthorizeAndLoadAsync"/>) and —
    /// only for a <see cref="InitiativeMutationOutcome.Success"/> outcome — persists the *returned*
    /// session (never the one that was loaded) and broadcasts. A domain invariant violation
    /// (<see cref="InvalidOperationException"/> or <see cref="ArgumentException"/>) becomes a 400
    /// rather than an unhandled 500, since these are exactly the exceptions #88's aggregate raises
    /// for a disallowed transition.
    /// </summary>
    private async Task<ApiResult> ExecuteAsync(
        Guid id, string? credential, InitiativeCredentialRole requiredRole, string? eventName, CancellationToken cancellationToken,
        Func<InitiativeSession, Task<InitiativeMutationOutcome>> mutate)
    {
        var (authorized, error) = await AuthorizeAndLoadAsync(id, credential, requiredRole, cancellationToken);
        if (error is not null) return error;
        var session = authorized!.Value.Session;

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
        if (eventName is not null)
        {
            await analytics.CaptureAsync(eventName, id, cancellationToken: cancellationToken);
        }
        var previousPhase = CurrentEngagementPhase(session);
        var updatedPhase = CurrentEngagementPhase(updated);
        if (updatedPhase > previousPhase)
        {
            await analytics.CaptureAsync(ProductEvents.InitiativePhaseReached, id,
                new Dictionary<string, object?> { ["phase"] = updatedPhase.ToString() }, cancellationToken);
        }
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

    /// <summary>
    /// Every credential failure and every wrong-role rejection (see <see cref="WrongRole"/>) uses
    /// this API's own <see cref="InitiativeErrorResponse"/> envelope at 400 — never a bare 401/403 —
    /// per issue #146's "the same structured error shape the session already uses for its other
    /// failures, not a generic error."
    /// </summary>
    private static ApiResult CredentialError(InitiativeCredentialFailure failure) => failure switch
    {
        InitiativeCredentialFailure.Missing =>
            new(new InitiativeErrorResponse("initiative.credential.missing", "A session credential is required for this request."), StatusCodes.Status400BadRequest),
        InitiativeCredentialFailure.Malformed =>
            new(new InitiativeErrorResponse("initiative.credential.malformed", "The session credential could not be parsed."), StatusCodes.Status400BadRequest),
        InitiativeCredentialFailure.Expired =>
            new(new InitiativeErrorResponse("initiative.credential.expired", "The session credential has expired."), StatusCodes.Status400BadRequest),
        InitiativeCredentialFailure.WrongSession =>
            new(new InitiativeErrorResponse("initiative.credential.wrong_session", "This credential was not issued for this Initiative session."), StatusCodes.Status400BadRequest),
        _ => throw new ArgumentOutOfRangeException(nameof(failure)),
    };

    private static ApiResult WrongRole(InitiativeCredentialRole requiredRole) =>
        new(new InitiativeErrorResponse("initiative.credential.wrong_role", $"This action requires a {requiredRole} credential."), StatusCodes.Status400BadRequest);

    /// <summary>
    /// <see cref="IInitiativeCredentialService"/> throws <see cref="InvalidOperationException"/> when
    /// <c>Initiative:CredentialSigningKey</c> is unconfigured outside Development (deliberately not
    /// checked at startup — see that service's own doc comment) — turned into this API's structured
    /// error envelope here so an operator misconfiguration surfaces as a clear 500, not an unhandled
    /// exception, and stays confined to Initiative endpoints rather than crashing the whole process.
    /// </summary>
    private static ApiResult CredentialServiceMisconfigured() =>
        new(new InitiativeErrorResponse("initiative.credential_service.misconfigured",
            "The Initiative credential service is not configured for this environment."), StatusCodes.Status500InternalServerError);

    private static EngagementPhase CurrentEngagementPhase(InitiativeSession session)
    {
        if (session.Finalization is not null) return EngagementPhase.Finalized;
        if (session.SelectedInterventions.Count > 0) return EngagementPhase.Shape;
        if (session.Responses.OfType<AcceptedResponse>().Any()) return EngagementPhase.Frame;
        return EngagementPhase.Discover;
    }

    private enum EngagementPhase { Discover, Frame, Shape, Finalized }
}
