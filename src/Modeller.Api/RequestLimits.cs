using Modeller.Api.Contracts;
using Modeller.Model;
using Modeller.Workspace;

namespace Modeller.Api;

/// <summary>
/// Request-shape ceilings the public API enforces in front of <c>ModellerWorkspace.Analyze</c>.
/// Nothing below this layer bounds document count, per-document size, or path depth today —
/// <c>ParseOptions</c>/<c>ValidationProfile</c> only cap aggregate characters/tokens/statements/
/// diagnostics, identically for the CLI's local-trust callers and anonymous public traffic. This
/// is the one place those public-traffic ceilings belong.
///
/// Every check here is also a null-safety guard: <c>Nullable</c> annotations on the request DTOs
/// are not runtime-enforced for JSON payloads that omit or null out a "required" field, so a
/// malformed request must be caught here — via <see cref="Validate"/> returning diagnostics —
/// rather than reaching a null-dereference deeper in the pipeline.
/// </summary>
public static class RequestLimits
{
    public const int MaximumDocuments = 50;
    public const int MaximumDocumentCharacters = 200_000;
    public const int MaximumPathSegments = 8;
    public const int MaximumProjectionRequests = 10;
    public const int MaximumRootsPerProjection = 50;
    public const int MaximumGraphElements = 2_000;
    public const int MaximumAggregateGraphElements = 5_000;
    public const int MaximumRoots = 500;
    public const int MaximumOutlineItems = 10_000;

    /// <summary>The node-count ceiling on an <em>analyzed</em> workspace's own definitions,
    /// checked before any projection runs. <c>DiagramProjector</c> (Modeller.Projections) now
    /// observes cancellation mid-walk, but still has no size budget of its own — bounding how
    /// large a revision it is ever asked to project remains the primary lever this API has to keep
    /// a single projection call itself cheap and fast, rather than relying solely on cancellation
    /// or discovering after the fact (post-hoc, via <see cref="MaximumGraphElements"/>) that it
    /// produced too much.</summary>
    public const int MaximumDefinitions = 5_000;

    /// <summary>Returns the diagnostics a request violates, or an empty list when the request is
    /// within every bound and safe to convert into a <c>WorkspaceInput</c>.</summary>
    public static IReadOnlyList<ApiDiagnostic> Validate(WorkspaceAnalyzeRequest request)
    {
        var diagnostics = new List<ApiDiagnostic>();
        ValidateDocuments(request.Documents, diagnostics);
        ValidateIdentity(request.Identity, diagnostics);
        ValidateConfiguration(request.Configuration, diagnostics);
        ValidateProjections(request.Projections, diagnostics);
        return diagnostics;
    }

    private static void ValidateDocuments(List<WorkspaceDocumentDto>? documents, List<ApiDiagnostic> diagnostics)
    {
        if (documents is not { Count: > 0 })
        {
            diagnostics.Add(new("api.request.documents.required", "At least one document is required."));
            return;
        }
        if (documents.Count > MaximumDocuments)
            diagnostics.Add(new("api.request.documents.too-many", $"A request may declare at most {MaximumDocuments} documents."));

        foreach (var document in documents)
        {
            if (document is null || string.IsNullOrEmpty(document.Path) || document.Content is null)
            {
                diagnostics.Add(new("api.request.document.malformed", "A declared document is missing a path or content."));
                continue;
            }
            ValidatePath(document.Path, "api.request.document", diagnostics);
            if (document.Content.Length > MaximumDocumentCharacters)
                diagnostics.Add(new("api.request.document.too-large", $"'{document.Path}' exceeds the {MaximumDocumentCharacters}-character per-document limit."));
        }
    }

    private static void ValidateIdentity(IdentityDto? identity, List<ApiDiagnostic> diagnostics)
    {
        if (identity is null)
        {
            diagnostics.Add(new("api.request.identity.required", "An identity strategy is required."));
            return;
        }
        if (identity is not DurableIdentityDto durable) return;
        if (durable.Documents is null)
        {
            diagnostics.Add(new("api.request.identity-registry.malformed", "A durable identity registry must declare its documents."));
            return;
        }
        foreach (var (path, identities) in durable.Documents)
        {
            if (string.IsNullOrEmpty(path) || identities is null)
            {
                diagnostics.Add(new("api.request.identity-registry.malformed", "A durable identity registry entry is missing a path or identities."));
                continue;
            }
            ValidatePath(path, "api.request.identity-registry", diagnostics);
        }
    }

    private static void ValidateConfiguration(ConfigurationDto? configuration, List<ApiDiagnostic> diagnostics)
    {
        if (configuration is null)
        {
            diagnostics.Add(new("api.request.configuration.required", "Workspace configuration is required."));
            return;
        }
        if (string.IsNullOrEmpty(configuration.GenerationContractVersion) || string.IsNullOrEmpty(configuration.LogicalOutputRoot))
            diagnostics.Add(new("api.request.configuration.malformed", "Workspace configuration must declare a generation contract version and logical output root."));
    }

    private static void ValidateProjections(List<ProjectionRequestDto>? projections, List<ApiDiagnostic> diagnostics)
    {
        if (projections is null) return;
        if (projections.Count > MaximumProjectionRequests)
            diagnostics.Add(new("api.request.projections.too-many", $"A request may declare at most {MaximumProjectionRequests} projection requests."));

        foreach (var projection in projections)
        {
            if (projection is null || string.IsNullOrEmpty(projection.Id) || projection.Roots is null)
            {
                diagnostics.Add(new("api.request.projection.malformed", "A projection request is missing an id or roots."));
                continue;
            }
            if (projection.Roots.Count > MaximumRootsPerProjection)
                diagnostics.Add(new("api.request.projection.too-many-roots", $"A projection request may declare at most {MaximumRootsPerProjection} roots."));
            foreach (var root in projection.Roots)
            {
                if (string.IsNullOrEmpty(root))
                {
                    diagnostics.Add(new("api.request.projection.root-invalid", "A projection root identifier is missing."));
                    continue;
                }
                ValidateRoot(root, diagnostics);
            }
        }
    }

    private static void ValidatePath(string path, string codePrefix, List<ApiDiagnostic> diagnostics)
    {
        if (!LogicalPath.TryCreate(path, out var logical))
        {
            diagnostics.Add(new($"{codePrefix}.path-invalid", $"'{path}' is not a confined logical path."));
            return;
        }
        if (logical.Value.Split('/').Length > MaximumPathSegments)
            diagnostics.Add(new($"{codePrefix}.path-too-deep", $"'{path}' exceeds the {MaximumPathSegments}-segment path depth limit."));
    }

    private static void ValidateRoot(string root, List<ApiDiagnostic> diagnostics)
    {
        try { SemanticId.Parse(root); }
        catch (Exception exception) when (exception is FormatException or ArgumentException)
        {
            diagnostics.Add(new("api.request.projection.root-invalid", $"'{root}' is not a valid root identifier."));
        }
    }
}
