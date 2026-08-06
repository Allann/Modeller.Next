using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Modeller.Api.Initiative;
using Xunit;

namespace Modeller.Api.Tests.Initiative;

public sealed class InitiativeEndpointsTests : IDisposable
{
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

        var created = await PostAsync("/v1/initiative", new CreateInitiativeRequest("Build us a new approval system", "Alex", "Jordan"), ct);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var session = await created.Content.ReadFromJsonAsync<InitiativeSessionDto>(ApiJson.Options, ct);
        var facilitatorId = session!.Participants.Single(p => p.Role == "Facilitator").Id;

        var proposed = await PostAsync($"/v1/initiative/{session.Id}/questions",
            new ProposeQuestionRequestDto(facilitatorId, "Facilitator", "PainPoints", "What's painful today?"), ct);
        Assert.Equal(HttpStatusCode.OK, proposed.StatusCode);
        var afterPropose = await proposed.Content.ReadFromJsonAsync<InitiativeSessionDto>(ApiJson.Options, ct);
        var questionId = afterPropose!.Questions.Single().Id;

        var sent = await PostAsync($"/v1/initiative/{session.Id}/questions/{questionId}/send", body: null, ct);
        Assert.Equal(HttpStatusCode.OK, sent.StatusCode);

        var responded = await PostAsync($"/v1/initiative/{session.Id}/questions/{questionId}/responses",
            new SubmitResponseRequestDto("Decisions take twelve days."), ct);
        Assert.Equal(HttpStatusCode.OK, responded.StatusCode);
        var afterRespond = await responded.Content.ReadFromJsonAsync<InitiativeSessionDto>(ApiJson.Options, ct);
        var responseId = afterRespond!.Responses.Single().Id;

        var accepted = await PostAsync($"/v1/initiative/{session.Id}/responses/{responseId}/accept", body: null, ct);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);

        var selected = await PostAsync($"/v1/initiative/{session.Id}/interventions",
            new SelectInterventionRequestDto("Process", "Remove a duplicate approval", "Cuts two days."), ct);
        Assert.Equal(HttpStatusCode.OK, selected.StatusCode);

        var gateEvaluated = await PostAsync($"/v1/initiative/{session.Id}/gate-evaluations",
            new RecordGateEvaluationRequestDto("Shape", [new GateCheckResultDto("NoActionWasConsidered", false, "Not discussed.")]), ct);
        Assert.Equal(HttpStatusCode.OK, gateEvaluated.StatusCode);

        var finalized = await PostAsync($"/v1/initiative/{session.Id}/finalize", new FinalizeRequestDto("Proceeding despite the open finding."), ct);
        Assert.Equal(HttpStatusCode.OK, finalized.StatusCode);
        var final = await finalized.Content.ReadFromJsonAsync<InitiativeSessionDto>(ApiJson.Options, ct);
        Assert.NotNull(final!.Finalization);
        Assert.Equal("WithOpenGateFindings", final.Finalization!.Status);
        Assert.Single(final.GateOverrides);

        var fetched = await _client.GetAsync($"/v1/initiative/{session.Id}", ct);
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
    }

    [Fact]
    public async Task Get_UnknownInitiative_Returns404WithStructuredEnvelope()
    {
        var response = await _client.GetAsync($"/v1/initiative/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<InitiativeErrorResponse>(ApiJson.Options, TestContext.Current.CancellationToken);
        Assert.Equal("initiative.not_found", error!.Code);
    }

    [Fact]
    public async Task Create_MissingRequiredField_Returns400WithStructuredEnvelope()
    {
        var response = await PostAsync("/v1/initiative", new CreateInitiativeRequest("", "Alex", "Jordan"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<InitiativeErrorResponse>(ApiJson.Options, TestContext.Current.CancellationToken);
        Assert.Equal("initiative.request.invalid", error!.Code);
    }

    [Fact]
    public async Task ProposeQuestion_WithNoTextAndNoAgentConfigured_Returns422IdentifyingTheDegradedStatus()
    {
        // No Agent:BaseUrl configured in this test host, so IAgentAdvisor resolves to
        // HumanOnlyAgentAdvisor (Program.cs) — this proves the "always able to proceed without AI"
        // requirement fails loudly and identifiably at the HTTP boundary rather than silently, when
        // the caller omits text and expects AI to fill it in.
        var ct = TestContext.Current.CancellationToken;
        var created = await PostAsync("/v1/initiative", new CreateInitiativeRequest("Build us a new approval system", "Alex", "Jordan"), ct);
        var session = await created.Content.ReadFromJsonAsync<InitiativeSessionDto>(ApiJson.Options, ct);
        var facilitatorId = session!.Participants.Single(p => p.Role == "Facilitator").Id;

        var response = await PostAsync($"/v1/initiative/{session.Id}/questions",
            new ProposeQuestionRequestDto(facilitatorId, "Facilitator", "PainPoints", Text: null), ct);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<InitiativeErrorResponse>(ApiJson.Options, ct);
        Assert.Equal("initiative.agent.NotConfigured", error!.Code);
    }

    [Fact]
    public async Task ProposeQuestion_UnrecognisedField_Returns400_NotAnUnhandled500()
    {
        var ct = TestContext.Current.CancellationToken;
        var created = await PostAsync("/v1/initiative", new CreateInitiativeRequest("Build us a new approval system", "Alex", "Jordan"), ct);
        var session = await created.Content.ReadFromJsonAsync<InitiativeSessionDto>(ApiJson.Options, ct);
        var facilitatorId = session!.Participants.Single(p => p.Role == "Facilitator").Id;

        var response = await PostAsync($"/v1/initiative/{session.Id}/questions",
            new ProposeQuestionRequestDto(facilitatorId, "Facilitator", "NotARealField", "Some text"), ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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
    public async Task Get_WithDomainExpertViewerRole_HidesFacilitatorOnlyContent()
    {
        var ct = TestContext.Current.CancellationToken;
        var created = await PostAsync("/v1/initiative", new CreateInitiativeRequest("Build us a new approval system", "Alex", "Jordan"), ct);
        var session = await created.Content.ReadFromJsonAsync<InitiativeSessionDto>(ApiJson.Options, ct);
        var facilitatorId = session!.Participants.Single(p => p.Role == "Facilitator").Id;

        // A proposed-but-never-sent question must not be visible to the Domain Expert.
        await PostAsync($"/v1/initiative/{session.Id}/questions",
            new ProposeQuestionRequestDto(facilitatorId, "Facilitator", "PainPoints", "Not sent yet"), ct);

        var response = await _client.GetAsync($"/v1/initiative/{session.Id}?viewerRole=DomainExpert", ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var domainExpertView = await response.Content.ReadFromJsonAsync<InitiativeSessionDto>(ApiJson.Options, ct);

        Assert.Empty(domainExpertView!.Questions);

        var facilitatorView = await _client.GetAsync($"/v1/initiative/{session.Id}", ct);
        var facilitatorDto = await facilitatorView.Content.ReadFromJsonAsync<InitiativeSessionDto>(ApiJson.Options, ct);
        Assert.Single(facilitatorDto!.Questions);
    }

    private async Task<HttpResponseMessage> PostAsync(string url, object? body, CancellationToken cancellationToken) =>
        body is null
            ? await _client.PostAsync(url, content: null, cancellationToken)
            : await _client.PostAsJsonAsync(url, body, ApiJson.Options, cancellationToken);

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        if (Directory.Exists(_storageRoot)) Directory.Delete(_storageRoot, recursive: true);
    }
}
