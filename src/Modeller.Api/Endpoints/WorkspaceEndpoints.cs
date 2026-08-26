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

    private static readonly WorkspaceGenerateResponse MalformedGenerateRequestResponse = new(
        "1.0", [new("api.request.malformed", "The request body could not be parsed as a workspace generate request.")], []);

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
            var request = await TryReadJsonAsync<WorkspaceAnalyzeRequest>(context.Request, cancellationToken);
            if (request is null)
                return Results.Json(MalformedRequestResponse, statusCode: StatusCodes.Status400BadRequest);

            var result = pipeline.Handle(request, cancellationToken);
            return Results.Json(result.Body, statusCode: result.StatusCode);
        })
        .WithName("AnalyzeWorkspace")
        .Accepts<WorkspaceAnalyzeRequest>("application/json")
        .WithSummary("Analyze a workspace draft.")
        .WithDescription(
            "Parses and validates the submitted documents, returning diagnostics, a semantic outline, " +
            "semantic-kind counts, discoverable projection roots, and one projection result per requested " +
            "view. Stateless: nothing is persisted server-side. An ephemeral request (Identity.kind = " +
            "\"ephemeral\") gets back the identities the server minted for this analysis in the response's " +
            "Identity field — send that back as a durable Identity on a follow-up request to keep root IDs " +
            "stable across calls. A parse/validation failure still returns 200 with Diagnostics populated " +
            "and Roots/Outline/Summary/Projections empty — 400 is reserved for a request that violates this " +
            "API's own shape limits (too many documents, an invalid path, and similar), and 503 for a " +
            "downstream analysis failure unrelated to the submitted content.")
        .Produces<WorkspaceAnalyzeResponse>(StatusCodes.Status200OK)
        .Produces<WorkspaceAnalyzeResponse>(StatusCodes.Status400BadRequest)
        .Produces<WorkspaceAnalyzeResponse>(StatusCodes.Status503ServiceUnavailable);

        group.MapPost("/export", async (HttpContext context, WorkspaceAnalysisPipeline pipeline, CancellationToken cancellationToken) =>
        {
            var request = await TryReadJsonAsync<WorkspaceAnalyzeRequest>(context.Request, cancellationToken);
            if (request is null)
                return Results.Json(MalformedExportRequestResponse, statusCode: StatusCodes.Status400BadRequest);

            var result = pipeline.HandleExport(request, cancellationToken);
            return Results.Json(result.Body, statusCode: result.StatusCode);
        })
        .WithName("ExportWorkspace")
        .Accepts<WorkspaceAnalyzeRequest>("application/json")
        .WithSummary("Apply durable identities and export the workspace.")
        .WithDescription(
            "Analyzes the submitted documents the same way /analyze does, then harvests a durable identity " +
            "for every definition and rewrites the returned document text to carry it (as a \"# @id=...\" " +
            "comment). Used to turn an ephemeral playground draft into a workspace with stable IDs before " +
            "downloading it — Identity is null only when Diagnostics is non-empty, i.e. analysis failed. As " +
            "with /analyze, 400 is reserved for a request that violates this API's own shape limits, and " +
            "503 for a downstream analysis failure unrelated to the submitted content.")
        .Produces<WorkspaceExportResponse>(StatusCodes.Status200OK)
        .Produces<WorkspaceExportResponse>(StatusCodes.Status400BadRequest)
        .Produces<WorkspaceExportResponse>(StatusCodes.Status503ServiceUnavailable);

        group.MapPost("/generate", async (HttpContext context, WorkspaceGenerationPreviewPipeline pipeline, CancellationToken cancellationToken) =>
        {
            var request = await TryReadJsonAsync<WorkspaceGenerateRequest>(context.Request, cancellationToken);
            if (request is null)
                return Results.Json(MalformedGenerateRequestResponse, statusCode: StatusCodes.Status400BadRequest);

            var result = await pipeline.HandleAsync(request, cancellationToken);
            return Results.Json(result.Body, statusCode: result.StatusCode);
        })
        .WithName("GenerateWorkspacePreview")
        .Accepts<WorkspaceGenerateRequest>("application/json")
        .WithSummary("Preview the artifacts a template pack would generate from a workspace draft.")
        .WithDescription(
            "Analyzes the submitted documents the same way /analyze does, then plans and renders the named " +
            "template pack's artifacts entirely in-memory — nothing is ever written to a filesystem. A " +
            "parse/validation/plan/render content failure (including an unrecognized template pack id or an " +
            "incompatible generation contract version) still returns 200 with Diagnostics populated and " +
            "Artifacts empty, exactly as /analyze already does — 400 is reserved for a request that violates " +
            "this API's own shape limits, and 503 for a downstream failure unrelated to the submitted content.")
        .Produces<WorkspaceGenerateResponse>(StatusCodes.Status200OK)
        .Produces<WorkspaceGenerateResponse>(StatusCodes.Status400BadRequest)
        .Produces<WorkspaceGenerateResponse>(StatusCodes.Status503ServiceUnavailable);

        group.MapGet("/supported-views", () =>
            Results.Ok(new SupportedViewsResponse("1.0", [.. ModellerWorkspace.SupportedViewKinds])))
        .WithName("GetSupportedViews")
        .WithSummary("List the diagram view kinds this server can project.")
        .WithDescription("Lets a client build its view-kind picker without hard-coding the enum on the client side.")
        .Produces<SupportedViewsResponse>(StatusCodes.Status200OK);

        group.MapPost("/complete", async (HttpContext context, CancellationToken cancellationToken) =>
        {
            var request = await TryReadJsonAsync<WorkspaceCompletionRequest>(context.Request, cancellationToken);
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
        })
        .WithName("CompleteWorkspace")
        .Accepts<WorkspaceCompletionRequest>("application/json")
        .WithSummary("Get completion suggestions at a position in one document.")
        .WithDescription(
            "Line and Column are 1-based. Path must name a document present in Workspace.Documents. " +
            "Diagnostics is non-empty (and Items empty) when the request itself is invalid — an unknown " +
            "path or a non-positive position — rather than when the position simply has no completions.")
        .Produces<WorkspaceCompletionResponse>(StatusCodes.Status200OK)
        .Produces<WorkspaceCompletionResponse>(StatusCodes.Status400BadRequest);

        return app;
    }

    /// <summary>Shared by every route's body-read step above (analyze/export/completion/generate):
    /// minimal API's default body-binding failure path returns an empty 400 rather than this API's
    /// own structured diagnostic envelope, so each route reads and deserializes explicitly instead,
    /// via this one generic helper, so a malformed/missing body maps to that route's own response
    /// shape rather than duplicating the same try/catch once per request type.</summary>
    private static async Task<T?> TryReadJsonAsync<T>(HttpRequest request, CancellationToken cancellationToken) where T : class
    {
        try
        {
            return await request.ReadFromJsonAsync<T>(cancellationToken);
        }
        catch (Exception exception) when (exception is JsonException or BadHttpRequestException)
        {
            return null;
        }
    }
}
