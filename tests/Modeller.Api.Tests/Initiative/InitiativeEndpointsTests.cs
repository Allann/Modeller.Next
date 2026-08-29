using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Modeller.Api.Initiative;
using Xunit;

namespace Modeller.Api.Tests.Initiative;

public sealed class InitiativeEndpointsTests : IDisposable
{
    private const string CredentialHeader = "X-Initiative-Credential";

    private readonly string _storageRoot = Path.Combine(Path.GetTempPath(), "modeller-initiative-endpoint-tests", Guid.NewGuid().ToString("N"));
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public InitiativeEndpointsTests()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseSetting("Initiative:StorageRoot", _storageRoot));
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task FullHappyPath_CreateThroughFinalize_Returns200AtEveryStep()
    {
        var ct = TestContext.Current.CancellationToken;

        var (facilitatorCredential, domainExpertCredential, session) = await CreateSessionAsync(ct);

        var proposed = await PostAsync($"/v1/initiative/{session.Id}/questions", facilitatorCredential,
            new ProposeQuestionRequestDto("PainPoints", "What's painful today?"), ct);
        Assert.Equal(HttpStatusCode.OK, proposed.StatusCode);
        var afterPropose = await proposed.Content.ReadFromJsonAsync<InitiativeSessionDto>(ApiJson.Options, ct);
        var questionId = afterPropose!.Questions.Single().Id;

        var sent = await PostAsync($"/v1/initiative/{session.Id}/questions/{questionId}/send", facilitatorCredential, body: null, ct);
        Assert.Equal(HttpStatusCode.OK, sent.StatusCode);

        var responded = await PostAsync($"/v1/initiative/{session.Id}/questions/{questionId}/responses", domainExpertCredential,
            new SubmitResponseRequestDto("Decisions take twelve days."), ct);
        Assert.Equal(HttpStatusCode.OK, responded.StatusCode);
        var afterRespond = await responded.Content.ReadFromJsonAsync<InitiativeSessionDto>(ApiJson.Options, ct);
        var responseId = afterRespond!.Responses.Single().Id;

        var accepted = await PostAsync($"/v1/initiative/{session.Id}/responses/{responseId}/accept", facilitatorCredential, body: null, ct);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);

        var selected = await PostAsync($"/v1/initiative/{session.Id}/interventions", facilitatorCredential,
            new SelectInterventionRequestDto("Process", "Remove a duplicate approval", "Cuts two days."), ct);
        Assert.Equal(HttpStatusCode.OK, selected.StatusCode);

        var gateEvaluated = await PostAsync($"/v1/initiative/{session.Id}/gate-evaluations", facilitatorCredential,
            new RecordGateEvaluationRequestDto("Shape", [new GateCheckResultDto("NoActionWasConsidered", false, "Not discussed.")]), ct);
        Assert.Equal(HttpStatusCode.OK, gateEvaluated.StatusCode);

        var finalized = await PostAsync($"/v1/initiative/{session.Id}/finalize", facilitatorCredential, new FinalizeRequestDto("Proceeding despite the open finding."), ct);
        Assert.Equal(HttpStatusCode.OK, finalized.StatusCode);
        var final = await finalized.Content.ReadFromJsonAsync<InitiativeSessionDto>(ApiJson.Options, ct);
        Assert.NotNull(final!.Finalization);
        Assert.Equal("WithOpenGateFindings", final.Finalization!.Status);
        Assert.Single(final.GateOverrides);

        var fetched = await GetAsync($"/v1/initiative/{session.Id}", facilitatorCredential, ct);
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
    }

    [Fact]
    public async Task Get_UnknownInitiative_WithNoCredential_Returns400WithStructuredEnvelope()
    {
        var response = await _client.GetAsync($"/v1/initiative/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        // A missing/invalid credential is refused before the repository is even consulted, so an
        // unknown session ID never reaches the not-found branch — see InitiativePipeline.ExecuteAsync's
        // own remarks on validating the credential before ever loading the session.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<InitiativeErrorResponse>(ApiJson.Options, TestContext.Current.CancellationToken);
        Assert.Equal("initiative.credential.missing", error!.Code);
    }

    [Fact]
    public async Task Get_UnknownInitiative_WithCrossSessionCredential_Returns400WithStructuredEnvelope()
    {
        var ct = TestContext.Current.CancellationToken;
        var (facilitatorCredential, _, _) = await CreateSessionAsync(ct);

        var response = await GetAsync($"/v1/initiative/{Guid.NewGuid()}", facilitatorCredential, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<InitiativeErrorResponse>(ApiJson.Options, ct);
        Assert.Equal("initiative.credential.wrong_session", error!.Code);
    }

    [Fact]
    public async Task GetAgentStatus_WithNoAgentConfigured_ReturnsUnavailableWithoutSecrets()
    {
        var response = await _client.GetAsync("/v1/initiative/agent-status", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var status = await response.Content.ReadFromJsonAsync<AgentAdvisorStatusResponse>(ApiJson.Options, TestContext.Current.CancellationToken);
        Assert.False(status!.Available);
        Assert.Null(status.Model);
        Assert.True(status.RequiresApiKey);
        Assert.Null(status.FreeModel);
    }

    [Fact]
    public async Task Create_MissingRequiredField_Returns400WithStructuredEnvelope()
    {
        var response = await PostAsync("/v1/initiative", credential: null, new CreateInitiativeRequest("", "Alex", "Jordan"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<InitiativeErrorResponse>(ApiJson.Options, TestContext.Current.CancellationToken);
        Assert.Equal("initiative.request.invalid", error!.Code);
    }

    [Fact]
    public async Task Create_Succeeds_IssuesTwoDistinctCredentials()
    {
        var (facilitatorCredential, domainExpertCredential, _) = await CreateSessionAsync(TestContext.Current.CancellationToken);

        Assert.False(string.IsNullOrWhiteSpace(facilitatorCredential));
        Assert.False(string.IsNullOrWhiteSpace(domainExpertCredential));
        Assert.NotEqual(facilitatorCredential, domainExpertCredential);
    }

    [Fact]
    public async Task ProposeQuestion_WithNoTextAndNoAgentConfigured_Returns422IdentifyingTheDegradedStatus()
    {
        // No Agent:BaseUrl configured in this test host, so IAgentAdvisor resolves to
        // HumanOnlyAgentAdvisor (Program.cs) — this proves the "always able to proceed without AI"
        // requirement fails loudly and identifiably at the HTTP boundary rather than silently, when
        // the caller omits text and expects AI to fill it in.
        var ct = TestContext.Current.CancellationToken;
        var (facilitatorCredential, _, session) = await CreateSessionAsync(ct);

        var response = await PostAsync($"/v1/initiative/{session.Id}/questions", facilitatorCredential,
            new ProposeQuestionRequestDto("PainPoints", Text: null), ct);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<InitiativeErrorResponse>(ApiJson.Options, ct);
        Assert.Equal("initiative.agent.NotConfigured", error!.Code);
    }

    [Fact]
    public async Task ProposeQuestion_UnrecognisedField_Returns400_NotAnUnhandled500()
    {
        var ct = TestContext.Current.CancellationToken;
        var (facilitatorCredential, _, session) = await CreateSessionAsync(ct);

        var response = await PostAsync($"/v1/initiative/{session.Id}/questions", facilitatorCredential,
            new ProposeQuestionRequestDto("NotARealField", "Some text"), ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ProposeQuestion_WithDomainExpertCredential_IsRefused()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, domainExpertCredential, session) = await CreateSessionAsync(ct);

        var response = await PostAsync($"/v1/initiative/{session.Id}/questions", domainExpertCredential,
            new ProposeQuestionRequestDto("PainPoints", "What's painful today?"), ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<InitiativeErrorResponse>(ApiJson.Options, ct);
        Assert.Equal("initiative.credential.wrong_role", error!.Code);
    }

    [Fact]
    public async Task Create_MalformedJsonBody_Returns400WithStructuredEnvelope_NotFrameworkDefault()
    {
        var ct = TestContext.Current.CancellationToken;
        using var content = new StringContent("this is not json", System.Text.Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/v1/initiative", content, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<InitiativeErrorResponse>(ApiJson.Options, ct);
        Assert.Equal("initiative.request.malformed", error!.Code);
    }

    [Fact]
    public async Task Create_EmptyBody_Returns400WithStructuredEnvelope_NotFrameworkDefault()
    {
        var ct = TestContext.Current.CancellationToken;
        using var content = new StringContent(string.Empty, System.Text.Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/v1/initiative", content, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<InitiativeErrorResponse>(ApiJson.Options, ct);
        Assert.Equal("initiative.request.malformed", error!.Code);
    }

    [Fact]
    public async Task Get_WithDomainExpertCredential_HidesFacilitatorOnlyContent()
    {
        var ct = TestContext.Current.CancellationToken;
        var (facilitatorCredential, domainExpertCredential, session) = await CreateSessionAsync(ct);

        // A proposed-but-never-sent question must not be visible to the Domain Expert.
        await PostAsync($"/v1/initiative/{session.Id}/questions", facilitatorCredential,
            new ProposeQuestionRequestDto("PainPoints", "Not sent yet"), ct);

        var response = await GetAsync($"/v1/initiative/{session.Id}", domainExpertCredential, ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var domainExpertView = await response.Content.ReadFromJsonAsync<InitiativeSessionDto>(ApiJson.Options, ct);

        Assert.Empty(domainExpertView!.Questions);

        var facilitatorView = await GetAsync($"/v1/initiative/{session.Id}", facilitatorCredential, ct);
        var facilitatorDto = await facilitatorView.Content.ReadFromJsonAsync<InitiativeSessionDto>(ApiJson.Options, ct);
        Assert.Single(facilitatorDto!.Questions);
    }

    [Fact]
    public async Task Get_WithDomainExpertCredential_IgnoresAClaimedFacilitatorRole()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, domainExpertCredential, session) = await CreateSessionAsync(ct);

        // viewerRole is no longer bound by the endpoint at all — a caller cannot override the
        // credential's real role by supplying one. See InitiativeEndpoints.MapGet("/{id:guid}").
        var response = await GetAsync($"/v1/initiative/{session.Id}?viewerRole=Facilitator", domainExpertCredential, ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var domainExpertView = await response.Content.ReadFromJsonAsync<InitiativeSessionDto>(ApiJson.Options, ct);

        Assert.Empty(domainExpertView!.GateOverrides);
        Assert.Empty(domainExpertView.SelectedInterventions);
    }

    [Fact]
    public async Task Finalize_WithGarbledCredential_Returns400WithStructuredEnvelope()
    {
        var ct = TestContext.Current.CancellationToken;
        var (facilitatorCredential, _, session) = await CreateSessionAsync(ct);
        var garbled = facilitatorCredential[..^3] + "xyz";

        var response = await PostAsync($"/v1/initiative/{session.Id}/finalize", garbled, new FinalizeRequestDto(), ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<InitiativeErrorResponse>(ApiJson.Options, ct);
        Assert.Equal("initiative.credential.malformed", error!.Code);
    }

    [Fact]
    public async Task Finalize_WithExpiredCredential_Returns400WithStructuredEnvelope()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, _, session) = await CreateSessionAsync(ct);
        var credentialService = _factory.Services.GetRequiredService<IInitiativeCredentialService>();
        var expired = credentialService.Mint(session.Id, InitiativeCredentialRole.Facilitator, TimeSpan.FromSeconds(-1));

        var response = await PostAsync($"/v1/initiative/{session.Id}/finalize", expired, new FinalizeRequestDto(), ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<InitiativeErrorResponse>(ApiJson.Options, ct);
        Assert.Equal("initiative.credential.expired", error!.Code);
    }

    [Fact]
    public async Task GetInterventionSuggestions_WithDomainExpertCredential_Returns400WrongRole()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, domainExpertCredential, session) = await CreateSessionAsync(ct);

        var response = await GetAsync($"/v1/initiative/{session.Id}/interventions/suggestions", domainExpertCredential, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<InitiativeErrorResponse>(ApiJson.Options, ct);
        Assert.Equal("initiative.credential.wrong_role", error!.Code);
    }

    [Fact]
    public async Task GetInterventionSuggestions_WithFacilitatorCredentialAndNoAgentConfigured_Returns422IdentifyingTheDegradedStatus()
    {
        // Mirrors ProposeQuestion_WithNoTextAndNoAgentConfigured_Returns422IdentifyingTheDegradedStatus:
        // no Agent:BaseUrl configured in this test host, so IAgentAdvisor resolves to
        // HumanOnlyAgentAdvisor, which this exercises past the credential check into the advisor call.
        var ct = TestContext.Current.CancellationToken;
        var (facilitatorCredential, _, session) = await CreateSessionAsync(ct);

        var response = await GetAsync($"/v1/initiative/{session.Id}/interventions/suggestions", facilitatorCredential, ct);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<InitiativeErrorResponse>(ApiJson.Options, ct);
        Assert.Equal("initiative.agent.NotConfigured", error!.Code);
    }

    [Fact]
    public async Task Finalize_WithAnotherSessionsCredential_Returns400WithStructuredEnvelope()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, _, firstSession) = await CreateSessionAsync(ct);
        var (secondFacilitatorCredential, _, _) = await CreateSessionAsync(ct);

        var response = await PostAsync($"/v1/initiative/{firstSession.Id}/finalize", secondFacilitatorCredential, new FinalizeRequestDto(), ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<InitiativeErrorResponse>(ApiJson.Options, ct);
        Assert.Equal("initiative.credential.wrong_session", error!.Code);
    }

    private async Task<(string FacilitatorCredential, string DomainExpertCredential, InitiativeSessionDto Session)> CreateSessionAsync(CancellationToken ct)
    {
        var created = await PostAsync("/v1/initiative", credential: null,
            new CreateInitiativeRequest("Build us a new approval system", "Alex", "Jordan"), ct);
        var body = await created.Content.ReadFromJsonAsync<CreateInitiativeResponseDto>(ApiJson.Options, ct);
        return (body!.Credentials.Facilitator, body.Credentials.DomainExpert, body.Session);
    }

    private Task<HttpResponseMessage> GetAsync(string url, string? credential, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (credential is not null) request.Headers.Add(CredentialHeader, credential);
        return _client.SendAsync(request, cancellationToken);
    }

    private async Task<HttpResponseMessage> PostAsync(string url, string? credential, object? body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        if (credential is not null) request.Headers.Add(CredentialHeader, credential);
        if (body is not null) request.Content = JsonContent.Create(body, options: ApiJson.Options);
        return await _client.SendAsync(request, cancellationToken);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        if (Directory.Exists(_storageRoot)) Directory.Delete(_storageRoot, recursive: true);
    }
}
