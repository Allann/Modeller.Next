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

        group.MapPost("/complete", (WorkspaceCompletionRequest request, WorkspaceAnalysisPipeline pipeline, CancellationToken cancellationToken) =>
        {
            var source = request.Workspace.Documents.FirstOrDefault(item => item.Path == request.Path)?.Content ?? string.Empty;
            var parent = RmlGrammar.ParentAt(source, request.Line);
            var analyzed = pipeline.Handle(request.Workspace with { Projections = [] }, cancellationToken).Body;
            var keywords = RmlGrammar.AllowedStatements(parent)
                .Select(label => new CompletionItemDto(label, "keyword", $"Valid inside {parent ?? "the document root"}."));
            var symbols = analyzed.Outline.Select(item => new CompletionItemDto(item.Name, item.Kind, $"Resolved {item.Kind} in this workspace."));
            var items = keywords.Concat(symbols)
                .Where(item => request.Prefix.Length == 0 || item.Label.StartsWith(request.Prefix, StringComparison.OrdinalIgnoreCase))
                .DistinctBy(item => item.Label, StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item.Label, StringComparer.OrdinalIgnoreCase).ToArray();
            return Results.Ok(new WorkspaceCompletionResponse("1.0", items));
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
}
