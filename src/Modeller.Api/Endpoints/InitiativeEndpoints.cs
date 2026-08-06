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

        group.MapPost("/", (HttpContext context, InitiativePipeline pipeline, CancellationToken cancellationToken) =>
            RespondToBody<CreateInitiativeRequest>(context, cancellationToken, request => pipeline.CreateAsync(request, cancellationToken)));

        group.MapGet("/{id:guid}", (Guid id, string? viewerRole, InitiativePipeline pipeline, CancellationToken cancellationToken) =>
            Respond(pipeline.GetAsync(id, viewerRole, cancellationToken)));

        group.MapPost("/{id:guid}/questions", (Guid id, HttpContext context, InitiativePipeline pipeline, CancellationToken cancellationToken) =>
            RespondToBody<ProposeQuestionRequestDto>(context, cancellationToken, request => pipeline.ProposeQuestionAsync(id, request, cancellationToken)));

        group.MapPost("/{id:guid}/questions/{questionId:guid}/send", (Guid id, Guid questionId, InitiativePipeline pipeline, CancellationToken cancellationToken) =>
            Respond(pipeline.SendQuestionAsync(id, questionId, cancellationToken)));

        group.MapPost("/{id:guid}/questions/{questionId:guid}/reject", (Guid id, Guid questionId, InitiativePipeline pipeline, CancellationToken cancellationToken) =>
            Respond(pipeline.RejectQuestionAsync(id, questionId, cancellationToken)));

        group.MapPost("/{id:guid}/questions/{questionId:guid}/responses",
            (Guid id, Guid questionId, HttpContext context, InitiativePipeline pipeline, CancellationToken cancellationToken) =>
                RespondToBody<SubmitResponseRequestDto>(context, cancellationToken, request => pipeline.SubmitResponseAsync(id, questionId, request, cancellationToken)));

        group.MapPost("/{id:guid}/responses/{responseId:guid}/accept", (Guid id, Guid responseId, InitiativePipeline pipeline, CancellationToken cancellationToken) =>
            Respond(pipeline.AcceptResponseAsync(id, responseId, cancellationToken)));

        group.MapGet("/{id:guid}/interventions/suggestions", (Guid id, InitiativePipeline pipeline, CancellationToken cancellationToken) =>
            Respond(pipeline.GetInterventionSuggestionsAsync(id, cancellationToken)));

        group.MapPost("/{id:guid}/interventions", (Guid id, HttpContext context, InitiativePipeline pipeline, CancellationToken cancellationToken) =>
            RespondToBody<SelectInterventionRequestDto>(context, cancellationToken, request => pipeline.SelectInterventionAsync(id, request, cancellationToken)));

        // POST, not DELETE — the CORS policy below only allows GET/POST for the same preflight
        // reasons WorkspaceEndpoints already restricts to those two methods.
        group.MapPost("/{id:guid}/interventions/{interventionId:guid}/withdraw", (Guid id, Guid interventionId, InitiativePipeline pipeline, CancellationToken cancellationToken) =>
            Respond(pipeline.WithdrawInterventionAsync(id, interventionId, cancellationToken)));

        group.MapPost("/{id:guid}/interventions/{interventionId:guid}/design-workspace",
            (Guid id, Guid interventionId, HttpContext context, InitiativePipeline pipeline, CancellationToken cancellationToken) =>
                RespondToBody<LinkDesignWorkspaceRequestDto>(context, cancellationToken, request => pipeline.LinkDesignWorkspaceAsync(id, interventionId, request, cancellationToken)));

        group.MapPost("/{id:guid}/gate-evaluations", (Guid id, HttpContext context, InitiativePipeline pipeline, CancellationToken cancellationToken) =>
            RespondToBody<RecordGateEvaluationRequestDto>(context, cancellationToken, request => pipeline.RecordGateEvaluationAsync(id, request, cancellationToken)));

        group.MapPost("/{id:guid}/gate-evaluations/{kind}/dismiss",
            (Guid id, string kind, HttpContext context, InitiativePipeline pipeline, CancellationToken cancellationToken) =>
                RespondToBody<DismissGateFindingRequestDto>(context, cancellationToken, request => pipeline.DismissGateFindingAsync(id, kind, request, cancellationToken)));

        group.MapPost("/{id:guid}/finalize", (Guid id, HttpContext context, InitiativePipeline pipeline, CancellationToken cancellationToken) =>
            RespondToBody<FinalizeRequestDto>(context, cancellationToken, request => pipeline.FinalizeAsync(id, request, cancellationToken)));

        group.MapPost("/{id:guid}/reopen", (Guid id, InitiativePipeline pipeline, CancellationToken cancellationToken) =>
            Respond(pipeline.ReopenAsync(id, cancellationToken)));

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
