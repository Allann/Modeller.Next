using Modeller.Api.Initiative;

namespace Modeller.Api.Endpoints;

/// <summary>The route list itself — thin and declarative; all logic lives in
/// <see cref="InitiativePipeline"/>.</summary>
public static class InitiativeEndpoints
{
    public static WebApplication MapInitiativeEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/v1/initiative").WithTags("Initiative");

        group.MapPost("/", (CreateInitiativeRequest request, InitiativePipeline pipeline, CancellationToken cancellationToken) =>
            Respond(pipeline.CreateAsync(request, cancellationToken)));

        group.MapGet("/{id:guid}", (Guid id, InitiativePipeline pipeline, CancellationToken cancellationToken) =>
            Respond(pipeline.GetAsync(id, cancellationToken)));

        group.MapPost("/{id:guid}/questions", (Guid id, ProposeQuestionRequestDto request, InitiativePipeline pipeline, CancellationToken cancellationToken) =>
            Respond(pipeline.ProposeQuestionAsync(id, request, cancellationToken)));

        group.MapPost("/{id:guid}/questions/{questionId:guid}/send", (Guid id, Guid questionId, InitiativePipeline pipeline, CancellationToken cancellationToken) =>
            Respond(pipeline.SendQuestionAsync(id, questionId, cancellationToken)));

        group.MapPost("/{id:guid}/questions/{questionId:guid}/reject", (Guid id, Guid questionId, InitiativePipeline pipeline, CancellationToken cancellationToken) =>
            Respond(pipeline.RejectQuestionAsync(id, questionId, cancellationToken)));

        group.MapPost("/{id:guid}/questions/{questionId:guid}/responses",
            (Guid id, Guid questionId, SubmitResponseRequestDto request, InitiativePipeline pipeline, CancellationToken cancellationToken) =>
                Respond(pipeline.SubmitResponseAsync(id, questionId, request, cancellationToken)));

        group.MapPost("/{id:guid}/responses/{responseId:guid}/accept", (Guid id, Guid responseId, InitiativePipeline pipeline, CancellationToken cancellationToken) =>
            Respond(pipeline.AcceptResponseAsync(id, responseId, cancellationToken)));

        group.MapGet("/{id:guid}/interventions/suggestions", (Guid id, InitiativePipeline pipeline, CancellationToken cancellationToken) =>
            Respond(pipeline.GetInterventionSuggestionsAsync(id, cancellationToken)));

        group.MapPost("/{id:guid}/interventions", (Guid id, SelectInterventionRequestDto request, InitiativePipeline pipeline, CancellationToken cancellationToken) =>
            Respond(pipeline.SelectInterventionAsync(id, request, cancellationToken)));

        // POST, not DELETE — the CORS policy below only allows GET/POST for the same preflight
        // reasons WorkspaceEndpoints already restricts to those two methods.
        group.MapPost("/{id:guid}/interventions/{interventionId:guid}/withdraw", (Guid id, Guid interventionId, InitiativePipeline pipeline, CancellationToken cancellationToken) =>
            Respond(pipeline.WithdrawInterventionAsync(id, interventionId, cancellationToken)));

        group.MapPost("/{id:guid}/interventions/{interventionId:guid}/design-workspace",
            (Guid id, Guid interventionId, LinkDesignWorkspaceRequestDto request, InitiativePipeline pipeline, CancellationToken cancellationToken) =>
                Respond(pipeline.LinkDesignWorkspaceAsync(id, interventionId, request, cancellationToken)));

        group.MapPost("/{id:guid}/gate-evaluations", (Guid id, RecordGateEvaluationRequestDto request, InitiativePipeline pipeline, CancellationToken cancellationToken) =>
            Respond(pipeline.RecordGateEvaluationAsync(id, request, cancellationToken)));

        group.MapPost("/{id:guid}/gate-evaluations/{kind}/dismiss",
            (Guid id, string kind, DismissGateFindingRequestDto request, InitiativePipeline pipeline, CancellationToken cancellationToken) =>
                Respond(pipeline.DismissGateFindingAsync(id, kind, request, cancellationToken)));

        group.MapPost("/{id:guid}/finalize", (Guid id, FinalizeRequestDto request, InitiativePipeline pipeline, CancellationToken cancellationToken) =>
            Respond(pipeline.FinalizeAsync(id, request, cancellationToken)));

        group.MapPost("/{id:guid}/reopen", (Guid id, InitiativePipeline pipeline, CancellationToken cancellationToken) =>
            Respond(pipeline.ReopenAsync(id, cancellationToken)));

        return app;
    }

    private static async Task<IResult> Respond(Task<ApiResult> resultTask)
    {
        var result = await resultTask;
        return Results.Json(result.Body, statusCode: result.StatusCode);
    }
}
