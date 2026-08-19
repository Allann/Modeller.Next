using System.Text.Json;
using Modeller.Api.Contracts;
using Modeller.Workspace;
using Modeller.Parsing;

namespace Modeller.Api.Endpoints;

/// <summary>The route list itself — thin and declarative; all logic lives in
/// <see cref="WorkspaceAnalysisPipeline"/> and the DTO mapping extensions in
/// <see cref="Modeller.Api.Contracts"/>.</summary>
public static class WorkspaceEndpoints
{
    private static readonly WorkspaceAnalyzeResponse MalformedRequestResponse = new(
        "1.0", [new("api.request.malformed", "The request body could not be parsed as a workspace analyze request.")], [], [], [], [], null);

    private static readonly WorkspaceExportResponse MalformedExportRequestResponse = new(
        "1.0", [new("api.request.malformed", "The request body could not be parsed as a workspace export request.")], [], null);

    private static readonly WorkspaceCompletionResponse MalformedCompletionRequestResponse = new(
        "1.0", [], [new("api.request.malformed", "The request body could not be parsed as a workspace completion request.")]);

    public static WebApplication MapWorkspaceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/v1/workspace").WithTags("Workspace");

        group.MapPost("/analyze", async (HttpContext context, WorkspaceAnalysisPipeline pipeline, CancellationToken cancellationToken) =>
        {
            // Minimal API's default body-binding failure path returns an empty 400 rather than the
            // API's own structured diagnostic envelope (issue #71 requires every invalid request to
            // fail with a stable, structured response) — read and deserialize explicitly instead so
            // a malformed/missing body maps to the same WorkspaceAnalyzeResponse shape as every
            // other rejection.
            var request = await TryReadRequestAsync(context.Request, cancellationToken);
            if (request is null)
                return Results.Json(MalformedRequestResponse, statusCode: StatusCodes.Status400BadRequest);

            var result = pipeline.Handle(request, cancellationToken);
            return Results.Json(result.Body, statusCode: result.StatusCode);
        });

        group.MapPost("/export", async (HttpContext context, WorkspaceAnalysisPipeline pipeline, CancellationToken cancellationToken) =>
        {
            var request = await TryReadRequestAsync(context.Request, cancellationToken);
            if (request is null)
                return Results.Json(MalformedExportRequestResponse, statusCode: StatusCodes.Status400BadRequest);

            var result = pipeline.HandleExport(request, cancellationToken);
            return Results.Json(result.Body, statusCode: result.StatusCode);
        });

        group.MapGet("/supported-views", () =>
            Results.Ok(new SupportedViewsResponse("1.0", [.. ModellerWorkspace.SupportedViewKinds])));

        group.MapPost("/complete", async (HttpContext context, CancellationToken cancellationToken) =>
        {
            var request = await TryReadCompletionRequestAsync(context.Request, cancellationToken);
            if (request is null)
                return Results.Json(MalformedCompletionRequestResponse, statusCode: StatusCodes.Status400BadRequest);

            var diagnostics = RequestLimits.Validate(request.Workspace).ToList();
            var document = request.Workspace.Documents.FirstOrDefault(item => item.Path == request.Path);
            if (document is null)
                diagnostics.Add(new("api.request.completion.path-invalid", "The completion document must exist in the submitted workspace."));
            if (request.Line < 1 || request.Column < 1)
                diagnostics.Add(new("api.request.completion.position-invalid", "The completion position must use positive line and column values."));
            if (diagnostics.Count > 0)
                return Results.Json(new WorkspaceCompletionResponse("1.0", [], diagnostics), statusCode: StatusCodes.Status400BadRequest);

            var sources = request.Workspace.Documents.Select(item => new SourceDocument(item.Path, item.Content));
            var items = RmlGrammar.Complete(sources, request.Path, request.Line, request.Column, cancellationToken)
                .Select(item => new CompletionItemDto(item.Label, item.Kind, item.Detail, item.InsertText, item.ReplacementStartColumn)).ToArray();
            return Results.Ok(new WorkspaceCompletionResponse("1.0", items, []));
        });

        return app;
    }

    private static async Task<WorkspaceAnalyzeRequest?> TryReadRequestAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return await request.ReadFromJsonAsync<WorkspaceAnalyzeRequest>(cancellationToken);
        }
        catch (Exception exception) when (exception is JsonException or BadHttpRequestException)
        {
            return null;
        }
    }


    private static async Task<WorkspaceCompletionRequest?> TryReadCompletionRequestAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return await request.ReadFromJsonAsync<WorkspaceCompletionRequest>(cancellationToken);
        }
        catch (Exception exception) when (exception is JsonException or BadHttpRequestException)
        {
            return null;
        }
    }
}
