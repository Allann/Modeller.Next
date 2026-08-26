using System.Text.Json;
using Modeller.Api.Initiative;

namespace Modeller.Api.Endpoints;

/// <summary>The route list itself — thin and declarative; all logic lives in
/// <see cref="InitiativePipeline"/>.</summary>
public static class InitiativeEndpoints
{
    private static readonly InitiativeErrorResponse MalformedRequestResponse =
        new("initiative.request.malformed", "The request body could not be parsed as JSON matching the expected shape.");

    public static WebApplication MapInitiativeEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/v1/initiative").WithTags("Initiative");

        group.MapGet("/agent-status", (AgentAdvisorStatusResponse status) => Results.Ok(status))
            .WithName("GetAgentAdvisorStatus")
            .WithSummary("Report whether AI-assisted actions are available.")
            .WithDescription(
                "Public, secret-free — safe to call before authenticating. Every Discover/Frame/Shape action " +
                "this API exposes can proceed human-only when Available is false; nothing requires AI assistance.")
            .Produces<AgentAdvisorStatusResponse>(StatusCodes.Status200OK);

        group.MapPost("/", (HttpContext context, InitiativePipeline pipeline, CancellationToken cancellationToken) =>
            RespondToBody<CreateInitiativeRequest>(context, cancellationToken, request => pipeline.CreateAsync(request, cancellationToken)))
            .WithName("CreateInitiative")
            .Accepts<CreateInitiativeRequest>("application/json")
            .WithSummary("Start a new Initiative session.")
            .WithDescription("Creates the session and registers the Facilitator and Domain Expert as its first two participants.")
            .Produces<InitiativeSessionDto>(StatusCodes.Status200OK)
            .Produces<InitiativeErrorResponse>(StatusCodes.Status400BadRequest);

        group.MapGet("/{id:guid}", (Guid id, string? viewerRole, InitiativePipeline pipeline, CancellationToken cancellationToken) =>
            Respond(pipeline.GetAsync(id, viewerRole, cancellationToken)))
            .WithName("GetInitiative")
            .WithSummary("Fetch an Initiative session.")
            .WithDescription(
                "viewerRole scopes the response to what that role may see — omit it for the full Facilitator " +
                "view, pass \"DomainExpert\" for the filtered projection that keeps Business Statement's " +
                "role-scoped visibility rule.")
            .Produces<InitiativeSessionDto>(StatusCodes.Status200OK)
            .Produces<InitiativeErrorResponse>(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/questions", (Guid id, HttpContext context, InitiativePipeline pipeline, CancellationToken cancellationToken) =>
            RespondToBody<ProposeQuestionRequestDto>(context, cancellationToken, request => pipeline.ProposeQuestionAsync(id, request, cancellationToken)))
            .WithName("ProposeQuestion")
            .Accepts<ProposeQuestionRequestDto>("application/json")
            .WithSummary("Propose a question for the session.")
            .WithDescription(
                "Omit Text to have the configured Agent Advisor propose wording; if none is configured (or it " +
                "fails), the request is rejected so the caller can prompt for manual text instead.")
            .Produces<InitiativeSessionDto>(StatusCodes.Status200OK)
            .Produces<InitiativeErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<InitiativeErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<InitiativeErrorResponse>(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/{id:guid}/questions/{questionId:guid}/send", (Guid id, Guid questionId, InitiativePipeline pipeline, CancellationToken cancellationToken) =>
            Respond(pipeline.SendQuestionAsync(id, questionId, cancellationToken)))
            .WithName("SendQuestion")
            .WithSummary("Send a proposed question to the Domain Expert.")
            .Produces<InitiativeSessionDto>(StatusCodes.Status200OK)
            .Produces<InitiativeErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<InitiativeErrorResponse>(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/questions/{questionId:guid}/reject", (Guid id, Guid questionId, InitiativePipeline pipeline, CancellationToken cancellationToken) =>
            Respond(pipeline.RejectQuestionAsync(id, questionId, cancellationToken)))
            .WithName("RejectQuestion")
            .WithSummary("Reject a proposed question instead of sending it.")
            .Produces<InitiativeSessionDto>(StatusCodes.Status200OK)
            .Produces<InitiativeErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<InitiativeErrorResponse>(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/questions/{questionId:guid}/responses",
            (Guid id, Guid questionId, HttpContext context, InitiativePipeline pipeline, CancellationToken cancellationToken) =>
                RespondToBody<SubmitResponseRequestDto>(context, cancellationToken, request => pipeline.SubmitResponseAsync(id, questionId, request, cancellationToken)))
            .WithName("SubmitResponse")
            .Accepts<SubmitResponseRequestDto>("application/json")
            .WithSummary("Submit the Domain Expert's response to a sent question.")
            .Produces<InitiativeSessionDto>(StatusCodes.Status200OK)
            .Produces<InitiativeErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<InitiativeErrorResponse>(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/responses/{responseId:guid}/accept", (Guid id, Guid responseId, InitiativePipeline pipeline, CancellationToken cancellationToken) =>
            Respond(pipeline.AcceptResponseAsync(id, responseId, cancellationToken)))
            .WithName("AcceptResponse")
            .WithSummary("Accept a submitted response as final.")
            .Produces<InitiativeSessionDto>(StatusCodes.Status200OK)
            .Produces<InitiativeErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<InitiativeErrorResponse>(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/interventions/suggestions", (Guid id, InitiativePipeline pipeline, CancellationToken cancellationToken) =>
            Respond(pipeline.GetInterventionSuggestionsAsync(id, cancellationToken)))
            .WithName("GetInterventionSuggestions")
            .WithSummary("Ask the Agent Advisor to suggest interventions for this session.")
            .WithDescription("Suggestions only — nothing is recorded until a caller selects one via POST /{id}/interventions.")
            .Produces<AgentInterventionSuggestionsResponse>(StatusCodes.Status200OK)
            .Produces<InitiativeErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<InitiativeErrorResponse>(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/{id:guid}/interventions", (Guid id, HttpContext context, InitiativePipeline pipeline, CancellationToken cancellationToken) =>
            RespondToBody<SelectInterventionRequestDto>(context, cancellationToken, request => pipeline.SelectInterventionAsync(id, request, cancellationToken)))
            .WithName("SelectIntervention")
            .Accepts<SelectInterventionRequestDto>("application/json")
            .WithSummary("Record the intervention chosen for this session.")
            .WithDescription("ContinuesToDesignWorkspace marks that this intervention will later be linked to a design workspace via the /design-workspace endpoint.")
            .Produces<InitiativeSessionDto>(StatusCodes.Status200OK)
            .Produces<InitiativeErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<InitiativeErrorResponse>(StatusCodes.Status404NotFound);

        // POST, not DELETE — the CORS policy below only allows GET/POST for the same preflight
        // reasons WorkspaceEndpoints already restricts to those two methods.
        group.MapPost("/{id:guid}/interventions/{interventionId:guid}/withdraw", (Guid id, Guid interventionId, InitiativePipeline pipeline, CancellationToken cancellationToken) =>
            Respond(pipeline.WithdrawInterventionAsync(id, interventionId, cancellationToken)))
            .WithName("WithdrawIntervention")
            .WithSummary("Withdraw a previously selected intervention.")
            .Produces<InitiativeSessionDto>(StatusCodes.Status200OK)
            .Produces<InitiativeErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<InitiativeErrorResponse>(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/interventions/{interventionId:guid}/design-workspace",
            (Guid id, Guid interventionId, HttpContext context, InitiativePipeline pipeline, CancellationToken cancellationToken) =>
                RespondToBody<LinkDesignWorkspaceRequestDto>(context, cancellationToken, request => pipeline.LinkDesignWorkspaceAsync(id, interventionId, request, cancellationToken)))
            .WithName("LinkDesignWorkspace")
            .Accepts<LinkDesignWorkspaceRequestDto>("application/json")
            .WithSummary("Link a selected intervention to the design workspace built for it.")
            .WithDescription("Reference is an opaque pointer to that workspace — e.g. a playground share link — this API does not resolve or validate it.")
            .Produces<InitiativeSessionDto>(StatusCodes.Status200OK)
            .Produces<InitiativeErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<InitiativeErrorResponse>(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/gate-evaluations", (Guid id, HttpContext context, InitiativePipeline pipeline, CancellationToken cancellationToken) =>
            RespondToBody<RecordGateEvaluationRequestDto>(context, cancellationToken, request => pipeline.RecordGateEvaluationAsync(id, request, cancellationToken)))
            .WithName("RecordGateEvaluation")
            .Accepts<RecordGateEvaluationRequestDto>("application/json")
            .WithSummary("Evaluate (or record a manual evaluation of) a Discovery/Shape gate.")
            .WithDescription("Omit ManualResults to have the Agent Advisor evaluate the gate instead of the caller supplying results directly.")
            .Produces<InitiativeSessionDto>(StatusCodes.Status200OK)
            .Produces<InitiativeErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<InitiativeErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<InitiativeErrorResponse>(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/{id:guid}/gate-evaluations/{kind}/dismiss",
            (Guid id, string kind, HttpContext context, InitiativePipeline pipeline, CancellationToken cancellationToken) =>
                RespondToBody<DismissGateFindingRequestDto>(context, cancellationToken, request => pipeline.DismissGateFindingAsync(id, kind, request, cancellationToken)))
            .WithName("DismissGateFinding")
            .Accepts<DismissGateFindingRequestDto>("application/json")
            .WithSummary("Override a single failing gate check with a documented reason.")
            .WithDescription("kind is the gate name (e.g. \"Discovery\"); Check in the request body names the specific finding being dismissed.")
            .Produces<InitiativeSessionDto>(StatusCodes.Status200OK)
            .Produces<InitiativeErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<InitiativeErrorResponse>(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/finalize", (Guid id, HttpContext context, InitiativePipeline pipeline, CancellationToken cancellationToken) =>
            RespondToBody<FinalizeRequestDto>(context, cancellationToken, request => pipeline.FinalizeAsync(id, request, cancellationToken)))
            .WithName("FinalizeInitiative")
            .Accepts<FinalizeRequestDto>("application/json")
            .WithSummary("Close the session and snapshot it to Markdown.")
            .WithDescription("Fails with a 400 InitiativeErrorResponse if a required gate has not passed and has no override recorded.")
            .Produces<InitiativeSessionDto>(StatusCodes.Status200OK)
            .Produces<InitiativeErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<InitiativeErrorResponse>(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/reopen", (Guid id, InitiativePipeline pipeline, CancellationToken cancellationToken) =>
            Respond(pipeline.ReopenAsync(id, cancellationToken)))
            .WithName("ReopenInitiative")
            .WithSummary("Reopen a finalized session.")
            .Produces<InitiativeSessionDto>(StatusCodes.Status200OK)
            .Produces<InitiativeErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<InitiativeErrorResponse>(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> Respond(Task<ApiResult> resultTask)
    {
        var result = await resultTask;
        return Results.Json(result.Body, statusCode: result.StatusCode);
    }

    /// <summary>
    /// Reads and validates the request body before handing it to the pipeline, so a malformed or
    /// missing body always returns this API's own structured <see cref="InitiativeErrorResponse"/>
    /// envelope — never the framework's default empty-bodied 400 — matching the convention
    /// <c>WorkspaceEndpoints</c> already establishes for the workspace-analyze request.
    /// </summary>
    private static async Task<IResult> RespondToBody<TRequest>(HttpContext context, CancellationToken cancellationToken, Func<TRequest, Task<ApiResult>> handle)
        where TRequest : class
    {
        var request = await TryReadRequestAsync<TRequest>(context.Request, cancellationToken);
        if (request is null)
            return Results.Json(MalformedRequestResponse, statusCode: StatusCodes.Status400BadRequest);

        var result = await handle(request);
        return Results.Json(result.Body, statusCode: result.StatusCode);
    }

    private static async Task<TRequest?> TryReadRequestAsync<TRequest>(HttpRequest request, CancellationToken cancellationToken) where TRequest : class
    {
        try
        {
            return await request.ReadFromJsonAsync<TRequest>(cancellationToken);
        }
        catch (Exception exception) when (exception is JsonException or BadHttpRequestException)
        {
            return null;
        }
    }
}
