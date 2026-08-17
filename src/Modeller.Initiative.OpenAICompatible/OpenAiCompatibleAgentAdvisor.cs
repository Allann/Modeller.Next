using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;

namespace Modeller.Initiative.OpenAICompatible;

/// <summary>
/// A real <see cref="IAgentAdvisor"/> against any OpenAI-compatible chat-completions endpoint (LM
/// Studio, Ollama's OpenAI shim, or a hosted OpenAI-compatible provider). Adapted from Business
/// Statement's <c>OpenAiCompatibleAgentAdvisor</c>
/// (M:\business-statement\src\BusinessStatement.Adapters.Llm.OpenAICompatible\OpenAiCompatibleAgentAdvisor.cs),
/// simplified: no Azure OpenAI / managed-identity path (out of scope for issue #89 — LM Studio or any
/// OpenAI-compatible endpoint only, per the ADR this repo's decision is drawn from). The adapter
/// does not request a provider-specific JSON response mode because some Gateway models reject it;
/// the system prompt specifies the shape and every response is parsed and validated locally.
///
/// Every public method returns an <see cref="AgentAdvisorResult{T}"/>, never throws for an
/// AI-availability failure — <see cref="AgentAdvisorException"/> is caught internally and translated,
/// consistent with issue #83/#86's "AI must be a pluggable add-on, never a hard dependency" requirement.
/// </summary>
public sealed class OpenAiCompatibleAgentAdvisor(HttpClient httpClient, AgentAdvisorOptions options) : IAgentAdvisor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly ActivitySource ActivitySource = new("Modeller.Initiative.OpenAICompatible");

    private const string QuestionSystemPrompt =
        """
        You are the Agent Participant helping a Facilitator run an Initiative through Modeller.
        Propose exactly one short, specific Prompted Question that would elicit the target field's
        content. Respond only as JSON, no commentary: {"text": "Question text?"}
        """;

    private const string FieldUpdateSystemPrompt =
        """
        You are the Agent Participant. Draft a concise, third-person structured-field entry from the
        Domain Expert's accepted response — do not invent facts not present in the response. Respond
        only as JSON, no commentary: {"draftText": "Entry text."}
        """;

    private const string InterventionSystemPrompt =
        """
        You are the Agent Participant helping Shape an Initiative. Propose two to five candidate
        interventions against the current outcomes and constraints, drawn only from these types:
        Process, People, Organisation, Policy, Information, Technology, Experiment, NoAction. Always
        include a NoAction baseline. Respond only as JSON, no commentary:
        {"suggestions": [{"type": "Process", "description": "...", "rationale": "..."}]}
        """;

    private const string GateSystemPrompt =
        """
        You are the Agent Participant evaluating a gate. For Discovery, use exactly these checks:
        OriginalChangeRequestCaptured, ProblemStatementDescribesBusinessProblem, AffectedUsersNamed,
        PainPointsAreConcrete, OutcomesAreObservable, SuccessCriteriaAreUnderstandable,
        NonGoalsAreListed, ConstraintsAreListed, AssumptionsAreListed, OpenQuestionsAreListed,
        RisksAreListed, NoUnresolvedSolutionLedLanguage. For Shape, use exactly:
        SelectedTechnologyInterventionsHaveRationale, NoActionWasConsidered. The gate is strictly
        advisory: findings only ever inform the Facilitator, never block them. Respond only as JSON,
        no commentary:
        {"checks": [{"check": "AffectedUsersNamed", "passed": true, "reason": "Short reason."}],
         "recommendedQuestion": null}
        Use null for recommendedQuestion when every check passes, otherwise
        {"text": "Question text?", "field": "AffectedUsers"}.
        """;

    public Task<AgentAdvisorResult<AgentQuestionSuggestion>> ProposeQuestionAsync(
        ProposeQuestionRequest request, CancellationToken cancellationToken = default) =>
        CompleteAsync(
            "propose_question",
            QuestionSystemPrompt,
            $"""
            Original change request:
            {request.OriginalChangeRequest}

            Target field: {request.TargetField}

            Current structured fields:
            {request.CurrentFields.ToMarkdown()}
            """,
            root => new AgentQuestionSuggestion(RequireString(root, "text"), request.TargetField),
            cancellationToken);

    public Task<AgentAdvisorResult<AgentFieldUpdateSuggestion>> DraftFieldUpdateAsync(
        DraftFieldUpdateRequest request, CancellationToken cancellationToken = default) =>
        CompleteAsync(
            "draft_field_update",
            FieldUpdateSystemPrompt,
            $"""
            Field: {request.Field}

            Accepted response:
            {request.AcceptedResponseText}

            Existing entries for this field:
            {string.Join(Environment.NewLine, request.ExistingEntries.Select(entry => $"- {entry}"))}
            """,
            root => new AgentFieldUpdateSuggestion(request.Field, RequireString(root, "draftText")),
            cancellationToken);

    public Task<AgentAdvisorResult<AgentInterventionSuggestions>> ProposeInterventionsAsync(
        ProposeInterventionsRequest request, CancellationToken cancellationToken = default) =>
        CompleteAsync(
            "propose_interventions",
            InterventionSystemPrompt,
            $"""
            Current structured fields:
            {request.CurrentFields.ToMarkdown()}
            """,
            ParseInterventionSuggestions,
            cancellationToken);

    public Task<AgentAdvisorResult<AgentGateEvaluationSuggestion>> EvaluateGateAsync(
        GateEvaluationRequest request, CancellationToken cancellationToken = default) =>
        CompleteAsync(
            "evaluate_gate",
            GateSystemPrompt,
            $"""
            Gate: {request.Kind}

            Current structured fields:
            {request.CurrentFields.ToMarkdown()}
            """,
            ParseGateEvaluationSuggestion,
            cancellationToken);

    private async Task<AgentAdvisorResult<T>> CompleteAsync<T>(
        string operationName, string systemPrompt, string userPrompt, Func<JsonElement, T> parse, CancellationToken cancellationToken)
    {
        var requestApiKey = options.RequestApiKeyProvider?.Invoke();
        var usesCallerKey = !string.IsNullOrWhiteSpace(requestApiKey);
        var usesFreeModel = !usesCallerKey && !string.IsNullOrWhiteSpace(options.FreeModel);
        var model = usesFreeModel ? options.FreeModel! : options.Model;
        var apiKey = requestApiKey ?? (usesFreeModel ? options.HostApiKeyProvider?.Invoke() : options.ApiKey);
        if (options.RequireApiKey && string.IsNullOrWhiteSpace(apiKey))
        {
            return AgentAdvisorResult<T>.Failure(
                AgentEvaluationStatus.NotConfigured,
                "Enter your own Vercel AI Gateway API key to use AI assistance.");
        }

        if (systemPrompt.Length + userPrompt.Length > options.MaxPromptCharacters)
        {
            return AgentAdvisorResult<T>.Failure(
                AgentEvaluationStatus.RequestFailed,
                "The Initiative context is too large for the configured Agent Advisor limit.");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.Timeout);
        using var activity = ActivitySource.StartActivity($"agent.{operationName}", ActivityKind.Client);
        activity?.SetTag("agent.model", model);
        activity?.SetTag("agent.caller_funded", usesCallerKey);
        activity?.SetTag("server.address", options.BaseUrl.Host);

        try
        {
            var httpRequest = BuildRequest(systemPrompt, userPrompt, model, apiKey);
            var response = await httpClient.SendAsync(httpRequest, timeout.Token);
            activity?.SetTag("http.response.status_code", (int)response.StatusCode);
            if (!response.IsSuccessStatusCode)
            {
                throw new AgentAdvisorException(
                    ToFailureKind(response.StatusCode), $"Agent advisor request failed with HTTP {(int)response.StatusCode}.");
            }

            var completion = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(JsonOptions, timeout.Token);
            activity?.SetTag("gen_ai.usage.input_tokens", completion?.Usage?.PromptTokens);
            activity?.SetTag("gen_ai.usage.output_tokens", completion?.Usage?.CompletionTokens);
            var content = completion?.Choices.FirstOrDefault()?.Message.Content;
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new AgentAdvisorException(AgentEvaluationStatus.InvalidResponse, "Agent advisor response did not include message content.");
            }

            using var document = JsonDocument.Parse(ExtractJsonPayload(content));
            var value = parse(document.RootElement);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return AgentAdvisorResult<T>.Success(value);
        }
        catch (AgentAdvisorException ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return AgentAdvisorResult<T>.Failure(ex.FailureKind, ex.Message);
        }
        catch (OperationCanceledException)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Timed out");
            return AgentAdvisorResult<T>.Failure(AgentEvaluationStatus.TimedOut, "Agent advisor request timed out.");
        }
        catch (HttpRequestException ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return AgentAdvisorResult<T>.Failure(AgentEvaluationStatus.EndpointUnavailable, ex.Message);
        }
        catch (JsonException ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return AgentAdvisorResult<T>.Failure(AgentEvaluationStatus.InvalidResponse, ex.Message);
        }
    }

    private HttpRequestMessage BuildRequest(string systemPrompt, string userPrompt, string model, string? apiKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, BuildEndpointUri())
        {
            Content = JsonContent.Create(new
            {
                model,
                temperature = 0,
                max_tokens = options.MaxOutputTokens,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt },
                },
            }, options: JsonOptions),
        };

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            request.Headers.Authorization = new("Bearer", apiKey);
        }

        return request;
    }

    private Uri BuildEndpointUri()
    {
        var baseUrl = options.BaseUrl.ToString();
        if (!baseUrl.EndsWith('/')) baseUrl += "/";
        return new Uri(new Uri(baseUrl), "chat/completions");
    }

    private static AgentEvaluationStatus ToFailureKind(System.Net.HttpStatusCode statusCode) => (int)statusCode switch
    {
        404 => AgentEvaluationStatus.ConfigurationError,
        408 or 504 => AgentEvaluationStatus.TimedOut,
        429 => AgentEvaluationStatus.ModelError,
        >= 500 => AgentEvaluationStatus.ModelError,
        _ => AgentEvaluationStatus.RequestFailed,
    };

    private static string RequireString(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!
            : throw new AgentAdvisorException(AgentEvaluationStatus.InvalidResponse, $"Agent advisor response was missing a non-empty '{propertyName}'.");

    private static AgentInterventionSuggestions ParseInterventionSuggestions(JsonElement root)
    {
        if (!root.TryGetProperty("suggestions", out var suggestions) || suggestions.ValueKind != JsonValueKind.Array)
        {
            throw new AgentAdvisorException(AgentEvaluationStatus.InvalidResponse, "Agent advisor response did not include a 'suggestions' array.");
        }

        var parsed = new List<AgentInterventionSuggestion>();
        foreach (var item in suggestions.EnumerateArray())
        {
            if (!Enum.TryParse<InterventionType>(RequireString(item, "type"), ignoreCase: true, out var type))
            {
                throw new AgentAdvisorException(AgentEvaluationStatus.InvalidResponse, "Agent advisor response contained an unrecognised intervention type.");
            }

            parsed.Add(new AgentInterventionSuggestion(type, RequireString(item, "description"), RequireString(item, "rationale")));
        }

        if (parsed.Count == 0)
        {
            throw new AgentAdvisorException(AgentEvaluationStatus.InvalidResponse, "Agent advisor returned no intervention suggestions.");
        }

        return new AgentInterventionSuggestions(parsed);
    }

    private static AgentGateEvaluationSuggestion ParseGateEvaluationSuggestion(JsonElement root)
    {
        if (!root.TryGetProperty("checks", out var checks) || checks.ValueKind != JsonValueKind.Array)
        {
            throw new AgentAdvisorException(AgentEvaluationStatus.InvalidResponse, "Agent advisor response did not include a 'checks' array.");
        }

        var results = new List<GateCheckResult>();
        foreach (var item in checks.EnumerateArray())
        {
            if (!Enum.TryParse<GateCheck>(RequireString(item, "check"), ignoreCase: true, out var check)
                || !item.TryGetProperty("passed", out var passed) || passed.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                throw new AgentAdvisorException(AgentEvaluationStatus.InvalidResponse, "Agent advisor response contained a malformed gate check.");
            }

            results.Add(new GateCheckResult(check, passed.GetBoolean(), RequireString(item, "reason")));
        }

        if (results.Count == 0)
        {
            throw new AgentAdvisorException(AgentEvaluationStatus.InvalidResponse, "Agent advisor response did not contain any recognisable gate checks.");
        }

        string? recommendedText = null;
        InitiativeField? recommendedField = null;
        if (root.TryGetProperty("recommendedQuestion", out var recommended) && recommended.ValueKind == JsonValueKind.Object)
        {
            recommendedText = RequireString(recommended, "text");
            recommendedField = Enum.TryParse<InitiativeField>(RequireString(recommended, "field"), ignoreCase: true, out var field)
                ? field
                : throw new AgentAdvisorException(AgentEvaluationStatus.InvalidResponse, "Agent advisor recommended question had an unrecognised field.");
        }

        return new AgentGateEvaluationSuggestion(results, recommendedText, recommendedField);
    }

    private static string ExtractJsonPayload(string content)
    {
        var trimmed = content.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal)) return trimmed;

        var firstLineBreak = trimmed.IndexOf('\n', StringComparison.Ordinal);
        if (firstLineBreak < 0) return trimmed;

        var payload = trimmed[(firstLineBreak + 1)..].Trim();
        return payload.EndsWith("```", StringComparison.Ordinal) ? payload[..^3].Trim() : payload;
    }

    private sealed record ChatCompletionResponse(IReadOnlyList<ChatChoice> Choices, ChatUsage? Usage = null);
    private sealed record ChatChoice(ChatMessage Message);
    private sealed record ChatMessage(string Content);
    private sealed record ChatUsage(int PromptTokens, int CompletionTokens);
}
