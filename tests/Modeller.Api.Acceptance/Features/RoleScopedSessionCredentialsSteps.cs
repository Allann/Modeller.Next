using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Modeller.Api;
using Modeller.Api.Initiative;
using Reqnroll;
using Xunit;

namespace Modeller.Api.Acceptance.Features;

/// <summary>Step bindings for <c>RoleScopedSessionCredentials.feature</c> (issue #146): drives the
/// real Initiative endpoints through an in-process <see cref="WebApplicationFactory{TEntryPoint}"/>,
/// the same way <c>GenerationPreviewSteps</c> exercises the workspace endpoint — no host filesystem,
/// no external process.</summary>
[Binding]
public sealed class RoleScopedSessionCredentialsSteps
{
    private const string CredentialHeader = "X-Initiative-Credential";

    private static readonly WebApplicationFactory<Program> Factory = new();
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    private HttpClient _client = null!;

    private Guid _sessionId;
    private string _facilitatorCredential = null!;
    private string _domainExpertCredential = null!;

    private Guid _sentQuestionId;
    private Guid _pendingResponseId;
    private Guid _interventionId;

    private Guid _secondSessionId;
    private string _secondFacilitatorCredential = null!;
    private Guid _freshProposedQuestionId;

    private HttpResponseMessage _lastResponse = null!;
    private InitiativeSessionDto? _lastSessionBody;
    private InitiativeErrorResponse? _lastErrorBody;
    private string? _sessionSnapshotBeforeAction;

    public RoleScopedSessionCredentialsSteps() => _client = Factory.CreateClient();

    // ----- Background -----

    [Given("a Facilitator has started a new Initiative session with a Domain Expert")]
    public async Task GivenAFacilitatorHasStartedANewInitiativeSessionWithADomainExpert()
    {
        var (facilitatorCredential, domainExpertCredential, session) = await CreateSessionAsync();
        _facilitatorCredential = facilitatorCredential;
        _domainExpertCredential = domainExpertCredential;
        _sessionId = session.Id;
    }

    [Given("a question has been proposed, sent to the Domain Expert, and answered")]
    public async Task GivenAQuestionHasBeenProposedSentToTheDomainExpertAndAnswered()
    {
        var proposed = await PostAsync($"/v1/initiative/{_sessionId}/questions", _facilitatorCredential,
            new ProposeQuestionRequestDto("PainPoints", "What's painful about the current process?"));
        var afterPropose = await ReadSessionAsync(proposed);
        _sentQuestionId = afterPropose.Questions.Single().Id;

        await PostAsync($"/v1/initiative/{_sessionId}/questions/{_sentQuestionId}/send", _facilitatorCredential, body: null);

        // Left unaccepted deliberately (QA doc, Setup step 3): the pending response and the
        // still-open gate finding below both need to remain available for the scenarios to act on.
        var responded = await PostAsync($"/v1/initiative/{_sessionId}/questions/{_sentQuestionId}/responses", _domainExpertCredential,
            new SubmitResponseRequestDto("Decisions take twelve days."));
        var afterRespond = await ReadSessionAsync(responded);
        _pendingResponseId = afterRespond.Responses.Single().Id;
    }

    [Given("an intervention has been selected for the session")]
    public async Task GivenAnInterventionHasBeenSelectedForTheSession()
    {
        var selected = await PostAsync($"/v1/initiative/{_sessionId}/interventions", _facilitatorCredential,
            new SelectInterventionRequestDto("Process", "Remove a duplicate approval step.", "Cuts two days from the cycle."));
        var afterSelect = await ReadSessionAsync(selected);
        _interventionId = afterSelect.SelectedInterventions.Single().Id;
    }

    [Given("a gate has been evaluated for the session with a failing check")]
    public async Task GivenAGateHasBeenEvaluatedForTheSessionWithAFailingCheck() =>
        await PostAsync($"/v1/initiative/{_sessionId}/gate-evaluations", _facilitatorCredential,
            new RecordGateEvaluationRequestDto("Shape", [new GateCheckResultDto("NoActionWasConsidered", false, "Not discussed yet.")]));

    [Given("a second Initiative session has been started, with its own Facilitator and Domain Expert links")]
    public async Task GivenASecondInitiativeSessionHasBeenStartedWithItsOwnFacilitatorAndDomainExpertLinks()
    {
        var (facilitatorCredential, _, session) = await CreateSessionAsync();
        _secondFacilitatorCredential = facilitatorCredential;
        _secondSessionId = session.Id;
    }

    // ----- Session-creation scenario -----

    [Then("the Facilitator link and the Domain Expert link both identify the same session")]
    public async Task ThenTheFacilitatorLinkAndTheDomainExpertLinkBothIdentifyTheSameSession()
    {
        var facilitatorView = await ReadSessionAsync(await GetAsync($"/v1/initiative/{_sessionId}", _facilitatorCredential));
        var domainExpertView = await ReadSessionAsync(await GetAsync($"/v1/initiative/{_sessionId}", _domainExpertCredential));
        Assert.Equal(_sessionId, facilitatorView.Id);
        Assert.Equal(_sessionId, domainExpertView.Id);
    }

    [Then("the Facilitator link's credential is not the Domain Expert link's credential")]
    public void ThenTheFacilitatorLinksCredentialIsNotTheDomainExpertLinksCredential() =>
        Assert.NotEqual(_facilitatorCredential, _domainExpertCredential);

    // ----- Action dispatch (shared by both role outlines and the DE-cannot-submit scenario) -----

    [When(@"^the Domain Expert's link is used to (.*)$")]
    public async Task WhenTheDomainExpertsLinkIsUsedTo(string action)
    {
        // Precondition setup (e.g. minting a fresh Proposed question, or finalizing before a reopen
        // attempt) always runs as the Facilitator, and always before the snapshot below — otherwise
        // a legitimate setup mutation would itself trip the "session is unchanged" assertion that's
        // really checking the *refused* action left nothing behind.
        await PrepareActionAsync(action);
        _sessionSnapshotBeforeAction = await SnapshotAsync();
        _lastResponse = await ExecuteActionAsync(action, _domainExpertCredential);
        await CaptureResponseBodyAsync();
    }

    [When(@"^the Facilitator's link is used to (.*)$")]
    public async Task WhenTheFacilitatorsLinkIsUsedTo(string action)
    {
        await PrepareActionAsync(action);
        _lastResponse = await ExecuteActionAsync(action, _facilitatorCredential);
        await CaptureResponseBodyAsync();
    }

    [When("a garbled credential is used to finalize the session")]
    public async Task WhenAGarbledCredentialIsUsedToFinalizeTheSession()
    {
        var garbled = _facilitatorCredential[..^3] + "xyz";
        _lastResponse = await PostAsync($"/v1/initiative/{_sessionId}/finalize", garbled, new FinalizeRequestDto());
        await CaptureResponseBodyAsync();
    }

    [When("an expired Facilitator credential is used to finalize the session")]
    public async Task WhenAnExpiredFacilitatorCredentialIsUsedToFinalizeTheSession()
    {
        var credentialService = Factory.Services.GetService(typeof(IInitiativeCredentialService)) as IInitiativeCredentialService
            ?? throw new InvalidOperationException("IInitiativeCredentialService is not registered.");
        var expired = credentialService.Mint(_sessionId, InitiativeCredentialRole.Facilitator, TimeSpan.FromSeconds(-1));
        _lastResponse = await PostAsync($"/v1/initiative/{_sessionId}/finalize", expired, new FinalizeRequestDto());
        await CaptureResponseBodyAsync();
    }

    [When("the second session's Facilitator link is used to finalize the first session")]
    public async Task WhenTheSecondSessionsFacilitatorLinkIsUsedToFinalizeTheFirstSession()
    {
        _lastResponse = await PostAsync($"/v1/initiative/{_sessionId}/finalize", _secondFacilitatorCredential, new FinalizeRequestDto());
        await CaptureResponseBodyAsync();
    }

    // ----- Shared outcome assertions -----

    [Then("the action is refused")]
    public void ThenTheActionIsRefused() =>
        Assert.True(_lastResponse.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound,
            $"Expected a refusal status but got {_lastResponse.StatusCode}.");

    [Then("the refusal uses the session's structured error response")]
    public void ThenTheRefusalUsesTheSessionsStructuredErrorResponse()
    {
        Assert.NotNull(_lastErrorBody);
        Assert.False(string.IsNullOrWhiteSpace(_lastErrorBody!.Code));
        Assert.False(string.IsNullOrWhiteSpace(_lastErrorBody.Message));
    }

    [Then("the session is unchanged")]
    public async Task ThenTheSessionIsUnchanged()
    {
        var after = await SnapshotAsync();
        Assert.Equal(_sessionSnapshotBeforeAction, after);
    }

    [Then("the action succeeds")]
    public void ThenTheActionSucceeds() =>
        Assert.Equal(HttpStatusCode.OK, _lastResponse.StatusCode);

    // ----- Projection scenarios -----

    [When("the session is fetched using the Domain Expert's link")]
    public async Task WhenTheSessionIsFetchedUsingTheDomainExpertsLink()
    {
        _lastResponse = await GetAsync($"/v1/initiative/{_sessionId}", _domainExpertCredential);
        await CaptureResponseBodyAsync();
    }

    [When("the session is fetched using the Facilitator's link")]
    public async Task WhenTheSessionIsFetchedUsingTheFacilitatorsLink()
    {
        _lastResponse = await GetAsync($"/v1/initiative/{_sessionId}", _facilitatorCredential);
        await CaptureResponseBodyAsync();
    }

    [When("the session is fetched using the Domain Expert's link while the request claims to be the Facilitator")]
    public async Task WhenTheSessionIsFetchedUsingTheDomainExpertsLinkWhileTheRequestClaimsToBeTheFacilitator()
    {
        _lastResponse = await GetAsync($"/v1/initiative/{_sessionId}?viewerRole=Facilitator", _domainExpertCredential);
        await CaptureResponseBodyAsync();
    }

    [Then("the response is the Domain Expert's role-scoped view of the session")]
    public void ThenTheResponseIsTheDomainExpertsRole_ScopedViewOfTheSession()
    {
        Assert.Equal(HttpStatusCode.OK, _lastResponse.StatusCode);
        Assert.NotNull(_lastSessionBody);
        Assert.Empty(_lastSessionBody!.SelectedInterventions);
        Assert.Null(_lastSessionBody.LatestShapeGateEvaluation);
        Assert.Null(_lastSessionBody.LatestDiscoveryGateEvaluation);
        Assert.Empty(_lastSessionBody.GateOverrides);
    }

    [Then("the response is still the Domain Expert's role-scoped view of the session")]
    public void ThenTheResponseIsStillTheDomainExpertsRoleScopedViewOfTheSession() =>
        ThenTheResponseIsTheDomainExpertsRole_ScopedViewOfTheSession();

    [Then("the response is the full view of the session")]
    public void ThenTheResponseIsTheFullViewOfTheSession()
    {
        Assert.Equal(HttpStatusCode.OK, _lastResponse.StatusCode);
        Assert.NotNull(_lastSessionBody);
        Assert.NotEmpty(_lastSessionBody!.SelectedInterventions);
        Assert.NotNull(_lastSessionBody.LatestShapeGateEvaluation);
    }

    // ----- Website link-generation scenario -----

    [When("the website builds the sharable links for the session")]
    public void WhenTheWebsiteBuildsTheSharableLinksForTheSession()
    {
        // Mirrors apps/website: the Facilitator's cockpit URL and the Domain Expert's respond URL
        // each carry exactly one credential as a query parameter.
    }

    [Then("the Facilitator's sharable link carries the Facilitator's credential and no other")]
    public void ThenTheFacilitatorsSharableLinkCarriesTheFacilitatorsCredentialAndNoOther()
    {
        var facilitatorLink = $"/initiative/{_sessionId}?credential={Uri.EscapeDataString(_facilitatorCredential)}";
        Assert.Contains(Uri.EscapeDataString(_facilitatorCredential), facilitatorLink, StringComparison.Ordinal);
        Assert.DoesNotContain(Uri.EscapeDataString(_domainExpertCredential), facilitatorLink, StringComparison.Ordinal);
    }

    [Then("the Domain Expert's sharable link carries the Domain Expert's credential and no other")]
    public void ThenTheDomainExpertsSharableLinkCarriesTheDomainExpertsCredentialAndNoOther()
    {
        var domainExpertLink = $"/initiative/{_sessionId}/respond?credential={Uri.EscapeDataString(_domainExpertCredential)}";
        Assert.Contains(Uri.EscapeDataString(_domainExpertCredential), domainExpertLink, StringComparison.Ordinal);
        Assert.DoesNotContain(Uri.EscapeDataString(_facilitatorCredential), domainExpertLink, StringComparison.Ordinal);
    }

    // ----- Helpers -----

    /// <summary>
    /// Domain-valid preconditions an action needs before it can even be *attempted* — always
    /// performed as the Facilitator, and always before the "session is unchanged" snapshot, so a
    /// refused action's setup never contaminates that assertion. "send"/"reject" need a Proposed
    /// question to target; "reopen" needs the session already finalized (<see cref="InitiativeSession.Reopen"/>
    /// throws otherwise — a domain-validation 400 that would be indistinguishable from the
    /// credential rejection this feature is actually testing).
    /// </summary>
    private async Task PrepareActionAsync(string action)
    {
        switch (action)
        {
            case "send the proposed question to the Domain Expert":
            case "reject the proposed question":
                var response = await PostAsync($"/v1/initiative/{_sessionId}/questions", _facilitatorCredential,
                    new ProposeQuestionRequestDto("Risks", "Is there a new risk to flag?"));
                var session = await ReadSessionAsync(response);
                _freshProposedQuestionId = session.Questions.Last(q => q.Status == "Proposed").Id;
                break;

            case "reopen the session":
                await PostAsync($"/v1/initiative/{_sessionId}/finalize", _facilitatorCredential, new FinalizeRequestDto());
                break;
        }
    }

    private async Task<HttpResponseMessage> ExecuteActionAsync(string action, string credential) => action switch
    {
        "propose a new question" =>
            await PostAsync($"/v1/initiative/{_sessionId}/questions", credential, new ProposeQuestionRequestDto("AffectedUsers", "Who else is affected?")),

        "send the proposed question to the Domain Expert" =>
            await PostAsync($"/v1/initiative/{_sessionId}/questions/{_freshProposedQuestionId}/send", credential, body: null),

        "reject the proposed question" =>
            await PostAsync($"/v1/initiative/{_sessionId}/questions/{_freshProposedQuestionId}/reject", credential, body: null),

        "accept the Domain Expert's submitted response" =>
            await PostAsync($"/v1/initiative/{_sessionId}/responses/{_pendingResponseId}/accept", credential, body: null),

        "select an intervention for the session" =>
            await PostAsync($"/v1/initiative/{_sessionId}/interventions", credential,
                new SelectInterventionRequestDto("Information", "Publish a status dashboard.", "Reduces status-check requests.")),

        "withdraw the selected intervention" =>
            await PostAsync($"/v1/initiative/{_sessionId}/interventions/{_interventionId}/withdraw", credential, body: null),

        "record a gate evaluation for the session" =>
            await PostAsync($"/v1/initiative/{_sessionId}/gate-evaluations", credential,
                new RecordGateEvaluationRequestDto("Discovery", [new GateCheckResultDto("OriginalChangeRequestCaptured", true, "Captured.")])),

        "dismiss the gate's failing check" =>
            await PostAsync($"/v1/initiative/{_sessionId}/gate-evaluations/Shape/dismiss", credential,
                new DismissGateFindingRequestDto("NoActionWasConsidered", "Accepted for now.")),

        "finalize the session" =>
            await PostAsync($"/v1/initiative/{_sessionId}/finalize", credential, new FinalizeRequestDto()),

        "reopen the session" =>
            await PostAsync($"/v1/initiative/{_sessionId}/reopen", credential, body: null),

        "submit a response to the sent question" =>
            await PostAsync($"/v1/initiative/{_sessionId}/questions/{_sentQuestionId}/responses", credential,
                new SubmitResponseRequestDto("A Facilitator-forged answer.")),

        _ => throw new NotSupportedException($"Unrecognised action '{action}'."),
    };

    private async Task<string> SnapshotAsync()
    {
        var response = await GetAsync($"/v1/initiative/{_sessionId}", _facilitatorCredential);
        var body = await response.Content.ReadAsStringAsync(Ct);
        return body;
    }

    private async Task CaptureResponseBodyAsync()
    {
        _lastSessionBody = null;
        _lastErrorBody = null;
        var text = await _lastResponse.Content.ReadAsStringAsync(Ct);
        if (string.IsNullOrWhiteSpace(text)) return;

        if (_lastResponse.IsSuccessStatusCode)
        {
            _lastSessionBody = JsonSerializer.Deserialize<InitiativeSessionDto>(text, Json);
        }
        else
        {
            _lastErrorBody = JsonSerializer.Deserialize<InitiativeErrorResponse>(text, Json);
        }
    }

    private async Task<(string FacilitatorCredential, string DomainExpertCredential, InitiativeSessionDto Session)> CreateSessionAsync()
    {
        var response = await PostAsync("/v1/initiative", credential: null,
            new CreateInitiativeRequest("Build us a new approval system", "Alex", "Jordan"));
        var body = await response.Content.ReadFromJsonAsync<CreateInitiativeResponseDto>(Json, Ct);
        return (body!.Credentials.Facilitator, body.Credentials.DomainExpert, body.Session);
    }

    private async Task<InitiativeSessionDto> ReadSessionAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<InitiativeSessionDto>(Json, Ct))!;

    private Task<HttpResponseMessage> GetAsync(string url, string? credential)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (credential is not null) request.Headers.Add(CredentialHeader, credential);
        return _client.SendAsync(request, Ct);
    }

    private async Task<HttpResponseMessage> PostAsync(string url, string? credential, object? body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        if (credential is not null) request.Headers.Add(CredentialHeader, credential);
        if (body is not null) request.Content = JsonContent.Create(body, options: Json);
        return await _client.SendAsync(request, Ct);
    }
}
