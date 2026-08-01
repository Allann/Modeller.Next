using System.Collections.Immutable;
using Modeller.Parsing;
using Modeller.Model;
using Modeller.Projections;
using Modeller.GenerationWorkflow;
using Modeller.Rendering;
using Modeller.Output;

namespace Modeller.Editor;

public sealed record EditorDocument(Uri Uri, long Version, string Text);
public sealed record EditorRequest(EditorDocument Document, long ExpectedVersion, EditorWorkflow Workflow);
public abstract record EditorWorkflow;
public sealed record PublishDiagnostics : EditorWorkflow;
public sealed record NavigateToConcept(SemanticId ConceptId) : EditorWorkflow;
public sealed record ProjectDiagram(ViewDefinition View, LayoutState? Layout = null) : EditorWorkflow;
public sealed record TranslateDiagramEdit(EditRequest Edit, long ExpectedSemanticRevision) : EditorWorkflow;
public sealed record ExecuteGeneration(GenerationExecutionRequest Request, IRendererAdapter Renderer, IOutputFileSystem FileSystem) : EditorWorkflow;

public abstract record EditorResponse;
public sealed record DiagnosticsResponse(ImmutableArray<EditorDiagnostic> Diagnostics) : EditorResponse;
public sealed record EditorConflict(string Code, string Message, long CurrentVersion) : EditorResponse;
public sealed record EditorCancelled : EditorResponse;
public sealed record NavigationTarget(Uri Uri, EditorRange Range);
public sealed record NavigationResponse(NavigationTarget Target) : EditorResponse;
public sealed record ProjectionResponse(ProjectionResult Projection) : EditorResponse;
public sealed record EditResponse(EditTranslation Translation) : EditorResponse;
public sealed record GenerationResponse(GenerationExecutionResult Result) : EditorResponse;

public enum EditorDiagnosticSeverity { Information, Warning, Error }
public sealed record EditorPosition(int Line, int Character);
public sealed record EditorRange(EditorPosition Start, EditorPosition End);
public sealed record EditorDiagnostic(string Code, string Message, EditorDiagnosticSeverity Severity, EditorRange Range);

public static class EditorIntegration
{
    public static ValueTask<EditorResponse> ExecuteAsync(EditorRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (cancellationToken.IsCancellationRequested)
            return ValueTask.FromResult<EditorResponse>(new EditorCancelled());
        if (request.ExpectedVersion != request.Document.Version)
            return ValueTask.FromResult<EditorResponse>(new EditorConflict(
                "editor.document.stale", "The document changed; retry the workflow against the current version.", request.Document.Version));
        if (!request.Document.Uri.IsAbsoluteUri || request.Document.Uri.Scheme != Uri.UriSchemeFile)
            return ValueTask.FromResult<EditorResponse>(new DiagnosticsResponse([
                new("editor.document.uri-invalid", "Editor documents require an absolute file URI.", EditorDiagnosticSeverity.Error,
                    new(new(0, 0), new(0, 0))) ]));

        return request.Workflow switch
        {
            PublishDiagnostics => ValueTask.FromResult<EditorResponse>(Diagnostics(request.Document, cancellationToken)),
            NavigateToConcept navigation => ValueTask.FromResult(Navigate(request.Document, navigation, cancellationToken)),
            ProjectDiagram projection => ValueTask.FromResult(Project(request.Document, projection, cancellationToken)),
            TranslateDiagramEdit edit => ValueTask.FromResult(Translate(request.Document, edit, cancellationToken)),
            ExecuteGeneration generation => Generate(generation, cancellationToken),
            _ => ValueTask.FromResult<EditorResponse>(new DiagnosticsResponse([
                new("editor.workflow.unsupported", "The editor workflow is not supported.", EditorDiagnosticSeverity.Error,
                    new(new(0, 0), new(0, 0))) ]))
        };
    }

    private static async ValueTask<EditorResponse> Generate(ExecuteGeneration request, CancellationToken cancellationToken) =>
        new GenerationResponse(await GenerationExecution.ExecuteAsync(request.Request, request.Renderer, request.FileSystem, cancellationToken));

    private static EditorResponse Navigate(EditorDocument document, NavigateToConcept request, CancellationToken cancellationToken)
    {
        var parsed = Parse(document, cancellationToken);
        if (parsed.IsCancelled) return new EditorCancelled();
        var source = parsed.Provenance.FirstOrDefault(item => item.SemanticId == request.ConceptId.ToString() && item.SemanticPath is null);
        if (source is null)
            return new DiagnosticsResponse([new("editor.navigation.target-not-found", "The semantic navigation target was not found.",
                EditorDiagnosticSeverity.Error, new(new(0, 0), new(0, 0)))]);
        return new NavigationResponse(new(document.Uri, Range(source.Span)));
    }

    private static EditorResponse Project(EditorDocument document, ProjectDiagram request, CancellationToken cancellationToken)
    {
        var parsed = Parse(document, cancellationToken);
        if (parsed.IsCancelled) return new EditorCancelled();
        if (parsed.Package is null || parsed.Diagnostics.Length > 0) return DiagnosticsResponse(parsed);
        return new ProjectionResponse(DiagramProjector.Project(parsed.Package.AuthoredRevision, request.View, request.Layout));
    }

    private static EditorResponse Translate(EditorDocument document, TranslateDiagramEdit request, CancellationToken cancellationToken)
    {
        var parsed = Parse(document, cancellationToken);
        if (parsed.IsCancelled) return new EditorCancelled();
        if (parsed.Package is null || parsed.Diagnostics.Length > 0) return DiagnosticsResponse(parsed);
        var revision = parsed.Package.AuthoredRevision;
        if (request.ExpectedSemanticRevision != revision.Revision)
            return new EditorConflict("editor.semantic-revision.stale", "The semantic revision changed; reproject before editing.", revision.Revision);
        return new EditResponse(ProjectionEditor.Translate(revision, request.Edit));
    }

    private static EditorResponse Diagnostics(EditorDocument document, CancellationToken cancellationToken)
    {
        var parsed = Parse(document, cancellationToken);
        if (parsed.IsCancelled) return new EditorCancelled();
        return DiagnosticsResponse(parsed);
    }

    private static ParseResult Parse(EditorDocument document, CancellationToken cancellationToken) => DefinitionParser.Parse(
        [new SourceDocument(Uri.UnescapeDataString(document.Uri.Segments[^1]), document.Text)], ParseOptions.Language1, cancellationToken);

    private static DiagnosticsResponse DiagnosticsResponse(ParseResult parsed) =>
        new(parsed.Diagnostics.Select(ToEditorDiagnostic).ToImmutableArray());

    private static EditorDiagnostic ToEditorDiagnostic(ParseDiagnostic diagnostic)
    {
        var span = diagnostic.Location;
        var start = new EditorPosition(Math.Max(0, (span?.Line ?? 1) - 1), Math.Max(0, (span?.Column ?? 1) - 1));
        return new(diagnostic.Code, diagnostic.Message, EditorDiagnosticSeverity.Error,
            new(start, new(start.Line, checked(start.Character + (span?.Length ?? 0)))));
    }


    private static EditorRange Range(SourceSpan span)
    {
        var start = new EditorPosition(Math.Max(0, span.Line - 1), Math.Max(0, span.Column - 1));
        return new(start, new(start.Line, checked(start.Character + span.Length)));
    }
}
