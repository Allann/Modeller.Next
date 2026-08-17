using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Modeller.Initiative;
using Modeller.Initiative.OpenAICompatible;
using Xunit;

namespace Modeller.Initiative.OpenAICompatible.Tests;

public class OpenAiCompatibleAgentAdvisorTests
{
    private static readonly AgentAdvisorOptions Options = new(new Uri("http://localhost:1234/v1/"), "local-model", "test-key");

    [Fact]
    public async Task ProposeQuestionAsync_SuccessfulCompletion_ReturnsSuggestion()
    {
        var advisor = CreateAdvisor(FakeChatCompletion("""{"text": "What is painful about the current process?"}"""));

        var result = await advisor.ProposeQuestionAsync(
            new ProposeQuestionRequest("Build a new system", EmptyFields(), InitiativeField.PainPoints), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal("What is painful about the current process?", result.Value!.Text);
        Assert.Equal(InitiativeField.PainPoints, result.Value.Field);
    }

    [Fact]
    public async Task ProposeQuestionAsync_RequestIncludesConfiguredOutputLimit()
    {
        JsonElement? requestBody = null;
        var handler = new FakeHandler(request =>
        {
            requestBody = request.Content!.ReadFromJsonAsync<JsonElement>().GetAwaiter().GetResult();
            return FakeChatCompletion("""{"text": "What is painful?"}""").Response(request);
        });
        var advisor = new OpenAiCompatibleAgentAdvisor(new HttpClient(handler), Options with { MaxOutputTokens = 321 });

        await advisor.ProposeQuestionAsync(
            new ProposeQuestionRequest("Build a new system", EmptyFields(), InitiativeField.PainPoints), TestContext.Current.CancellationToken);

        Assert.Equal(321, requestBody!.Value.GetProperty("max_tokens").GetInt32());
    }

    [Fact]
    public async Task ProposeQuestionAsync_ContextAboveConfiguredLimit_DoesNotCallProvider()
    {
        var called = false;
        var handler = new FakeHandler(_ =>
        {
            called = true;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var advisor = new OpenAiCompatibleAgentAdvisor(new HttpClient(handler), Options with { MaxPromptCharacters = 100 });

        var result = await advisor.ProposeQuestionAsync(
            new ProposeQuestionRequest("Build a new system", EmptyFields(), InitiativeField.PainPoints), TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(AgentEvaluationStatus.RequestFailed, result.Status);
        Assert.False(called);
    }

    [Fact]
    public async Task ProposeQuestionAsync_MissingRequiredRequestKey_DoesNotCallProvider()
    {
        var called = false;
        var handler = new FakeHandler(_ =>
        {
            called = true;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var advisor = new OpenAiCompatibleAgentAdvisor(
            new HttpClient(handler),
            Options with { ApiKey = null, RequestApiKeyProvider = () => null });

        var result = await advisor.ProposeQuestionAsync(
            new ProposeQuestionRequest("Build a new system", EmptyFields(), InitiativeField.PainPoints), TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(AgentEvaluationStatus.NotConfigured, result.Status);
        Assert.False(called);
    }

    [Fact]
    public async Task ProposeQuestionAsync_WithoutCallerKey_UsesOnlyConfiguredFreeModelAndHostKey()
    {
        JsonElement? requestBody = null;
        string? authorization = null;
        var handler = new FakeHandler(request =>
        {
            requestBody = request.Content!.ReadFromJsonAsync<JsonElement>().GetAwaiter().GetResult();
            authorization = request.Headers.Authorization?.Parameter;
            return FakeChatCompletion("""{"text": "What is painful?"}""").Response(request);
        });
        var advisor = new OpenAiCompatibleAgentAdvisor(new HttpClient(handler), Options with
        {
            ApiKey = null,
            RequestApiKeyProvider = () => null,
            HostApiKeyProvider = () => "host-oidc",
            FreeModel = "alibaba/qwen3.8-27b",
        });

        var result = await advisor.ProposeQuestionAsync(
            new ProposeQuestionRequest("Build a new system", EmptyFields(), InitiativeField.PainPoints), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal("alibaba/qwen3.8-27b", requestBody!.Value.GetProperty("model").GetString());
        Assert.Equal("host-oidc", authorization);
    }

    [Fact]
    public async Task ProposeQuestionAsync_WithCallerKey_UsesPremiumModelAndNeverHostKey()
    {
        JsonElement? requestBody = null;
        string? authorization = null;
        var handler = new FakeHandler(request =>
        {
            requestBody = request.Content!.ReadFromJsonAsync<JsonElement>().GetAwaiter().GetResult();
            authorization = request.Headers.Authorization?.Parameter;
            return FakeChatCompletion("""{"text": "What is painful?"}""").Response(request);
        });
        var advisor = new OpenAiCompatibleAgentAdvisor(new HttpClient(handler), Options with
        {
            ApiKey = null,
            RequestApiKeyProvider = () => "caller-key",
            HostApiKeyProvider = () => "host-oidc",
            FreeModel = "alibaba/qwen3.8-27b",
        });

        var result = await advisor.ProposeQuestionAsync(
            new ProposeQuestionRequest("Build a new system", EmptyFields(), InitiativeField.PainPoints), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal("local-model", requestBody!.Value.GetProperty("model").GetString());
        Assert.Equal("caller-key", authorization);
    }

    [Fact]
    public async Task ProposeQuestionAsync_FencedCodeBlockResponse_IsUnwrapped()
    {
        var advisor = CreateAdvisor(FakeChatCompletion("```json\n{\"text\": \"Who is affected?\"}\n```"));

        var result = await advisor.ProposeQuestionAsync(
            new ProposeQuestionRequest("Build a new system", EmptyFields(), InitiativeField.AffectedUsers), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal("Who is affected?", result.Value!.Text);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, AgentEvaluationStatus.ConfigurationError)]
    [InlineData(HttpStatusCode.RequestTimeout, AgentEvaluationStatus.TimedOut)]
    [InlineData(HttpStatusCode.TooManyRequests, AgentEvaluationStatus.ModelError)]
    [InlineData(HttpStatusCode.InternalServerError, AgentEvaluationStatus.ModelError)]
    [InlineData(HttpStatusCode.BadRequest, AgentEvaluationStatus.RequestFailed)]
    public async Task ProposeQuestionAsync_HttpFailure_MapsToExpectedFailureKind(HttpStatusCode statusCode, AgentEvaluationStatus expected)
    {
        var advisor = CreateAdvisor(new FakeHandler(_ => new HttpResponseMessage(statusCode)));

        var result = await advisor.ProposeQuestionAsync(
            new ProposeQuestionRequest("Build a new system", EmptyFields(), InitiativeField.PainPoints), TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(expected, result.Status);
    }

    [Fact]
    public async Task ProposeQuestionAsync_MissingMessageContent_ReturnsInvalidResponse()
    {
        var advisor = CreateAdvisor(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { choices = Array.Empty<object>() }),
        }));

        var result = await advisor.ProposeQuestionAsync(
            new ProposeQuestionRequest("Build a new system", EmptyFields(), InitiativeField.PainPoints), TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(AgentEvaluationStatus.InvalidResponse, result.Status);
    }

    [Fact]
    public async Task ProposeQuestionAsync_MalformedJsonPayload_ReturnsInvalidResponse()
    {
        var advisor = CreateAdvisor(FakeChatCompletion("this is not json"));

        var result = await advisor.ProposeQuestionAsync(
            new ProposeQuestionRequest("Build a new system", EmptyFields(), InitiativeField.PainPoints), TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(AgentEvaluationStatus.InvalidResponse, result.Status);
    }

    [Fact]
    public async Task ProposeQuestionAsync_ResponseMissingRequiredField_ReturnsInvalidResponse()
    {
        var advisor = CreateAdvisor(FakeChatCompletion("""{"somethingElse": "value"}"""));

        var result = await advisor.ProposeQuestionAsync(
            new ProposeQuestionRequest("Build a new system", EmptyFields(), InitiativeField.PainPoints), TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(AgentEvaluationStatus.InvalidResponse, result.Status);
    }

    [Fact]
    public async Task ProposeInterventionsAsync_ParsesEveryInterventionType()
    {
        var advisor = CreateAdvisor(FakeChatCompletion("""
            {"suggestions": [
                {"type": "Process", "description": "Remove a duplicate approval", "rationale": "Cuts two days."},
                {"type": "NoAction", "description": "Do nothing yet", "rationale": "Baseline for comparison."}
            ]}
            """));

        var result = await advisor.ProposeInterventionsAsync(new ProposeInterventionsRequest(EmptyFields()), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Value!.Suggestions.Count);
        Assert.Contains(result.Value.Suggestions, s => s.Type == InterventionType.NoAction);
    }

    [Fact]
    public async Task EvaluateGateAsync_ParsesChecksAndRecommendedQuestion()
    {
        var advisor = CreateAdvisor(FakeChatCompletion("""
            {"checks": [{"check": "NoActionWasConsidered", "passed": false, "reason": "Never discussed."}],
             "recommendedQuestion": {"text": "Should we consider doing nothing?", "field": "OpenQuestions"}}
            """));

        var result = await advisor.EvaluateGateAsync(new GateEvaluationRequest(GateKind.Shape, EmptyFields()), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Single(result.Value!.Results);
        Assert.False(result.Value.Results[0].Passed);
        Assert.Equal("Should we consider doing nothing?", result.Value.RecommendedQuestionText);
        Assert.Equal(InitiativeField.OpenQuestions, result.Value.RecommendedQuestionField);
    }

    [Fact]
    public async Task EvaluateGateAsync_NullRecommendedQuestion_LeavesRecommendationEmpty()
    {
        var advisor = CreateAdvisor(FakeChatCompletion("""
            {"checks": [{"check": "NoActionWasConsidered", "passed": true, "reason": "Discussed and rejected."}],
             "recommendedQuestion": null}
            """));

        var result = await advisor.EvaluateGateAsync(new GateEvaluationRequest(GateKind.Shape, EmptyFields()), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Null(result.Value!.RecommendedQuestionText);
        Assert.Null(result.Value.RecommendedQuestionField);
    }

    private static OpenAiCompatibleAgentAdvisor CreateAdvisor(HttpMessageHandler handler) =>
        new(new HttpClient(handler), Options);

    private static FakeHandler FakeChatCompletion(string messageContent) => new(_ => new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(new { choices = new[] { new { message = new { content = messageContent } } } }),
    });

    private static InitiativeStructuredFields EmptyFields() =>
        new("Build a new system", [], [], [], [], [], [], [], [], [], [], []);

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpResponseMessage Response(HttpRequestMessage request) => respond(request);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
