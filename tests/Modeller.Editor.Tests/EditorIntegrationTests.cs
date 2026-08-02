using Modeller.Editor;
using Modeller.Model;
using Modeller.Projections;
using Xunit;

namespace Modeller.Editor.Tests;

public sealed class EditorIntegrationTests
{
    [Fact]
    public async Task Opening_invalid_accs_source_reports_the_same_located_diagnostic_as_the_parser()
    {
        var content = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "child-care-accs.modeller"), TestContext.Current.CancellationToken);
        const string unknown = "Unknown eligibility fact";
        content = content.Replace("fact \"Active enrolment exists\"", $"fact \"{unknown}\"", StringComparison.Ordinal);
        var document = new EditorDocument(new("file:///workspace/child-care-accs.modeller"), 7, content);

        var result = await EditorIntegration.ExecuteAsync(new EditorRequest(document, 7, new PublishDiagnostics()), TestContext.Current.CancellationToken);

        var diagnostic = Assert.Single(Assert.IsType<DiagnosticsResponse>(result).Diagnostics);
        Assert.Equal("rml.reference.unresolved", diagnostic.Code);
        Assert.Equal(EditorDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal(30, diagnostic.Range.Start.Line);
    }

    [Fact]
    public async Task Stale_document_request_returns_a_conflict_without_parsing_old_text()
    {
        var document = new EditorDocument(new("file:///workspace/child-care-accs.modeller"), 8, "hostile stale text");

        var result = await EditorIntegration.ExecuteAsync(new EditorRequest(document, 7, new PublishDiagnostics()), TestContext.Current.CancellationToken);

        var conflict = Assert.IsType<EditorConflict>(result);
        Assert.Equal("editor.document.stale", conflict.Code);
        Assert.Equal(8, conflict.CurrentVersion);
    }

    [Fact]
    public async Task Navigation_follows_a_fact_reference_to_its_declaration()
    {
        var content = await AccsSource();
        var document = new EditorDocument(new("file:///workspace/child-care-accs.modeller"), 3, content);

        var result = await EditorIntegration.ExecuteAsync(new EditorRequest(document, 3,
            new NavigateToConcept(Id("0191f6d4-4ea0-7000-8000-000000000006"))), TestContext.Current.CancellationToken);

        var navigation = Assert.IsType<NavigationResponse>(result);
        Assert.Equal(16, navigation.Target.Range.Start.Line);
        Assert.Equal(document.Uri, navigation.Target.Uri);
    }

    [Fact]
    public async Task Lifecycle_projection_and_layout_edit_use_the_stable_modules()
    {
        var document = new EditorDocument(new("file:///workspace/child-care-accs.modeller"), 4, await AccsSource());
        var view = new ViewDefinition("accs-lifecycle", 1, ViewKind.Lifecycle,
            [Id("0191f6d4-4ea0-7000-8000-000000000002")]);

        var projected = await EditorIntegration.ExecuteAsync(new EditorRequest(document, 4, new ProjectDiagram(view)), TestContext.Current.CancellationToken);
        var graph = Assert.IsType<ProjectionResponse>(projected).Projection.Graph!;
        Assert.Equal(["Draft", "Submitted"], graph.Nodes.Select(node => node.Label));

        var edited = await EditorIntegration.ExecuteAsync(new EditorRequest(document, 4,
            new TranslateDiagramEdit(new MoveElement(graph.Nodes[0].Id, 25, 40), graph.SourceRevision)), TestContext.Current.CancellationToken);
        Assert.IsType<LayoutEdit>(Assert.IsType<EditResponse>(edited).Translation);
    }

    [Fact]
    public async Task Pre_cancelled_workflow_returns_no_partial_editor_result()
    {
        var document = new EditorDocument(new("file:///workspace/child-care-accs.modeller"), 1, await AccsSource());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var result = await EditorIntegration.ExecuteAsync(new EditorRequest(document, 1, new PublishDiagnostics()), cancellation.Token);

        Assert.IsType<EditorCancelled>(result);
    }

    [Theory]
    [InlineData("https://example.test/child-care.modeller")]
    [InlineData("untitled:child-care.modeller")]
    public async Task Non_file_documents_are_rejected_before_source_is_processed(string uri)
    {
        var result = await EditorIntegration.ExecuteAsync(
            new EditorRequest(new(new(uri), 1, "private hostile content"), 1, new PublishDiagnostics()),
            TestContext.Current.CancellationToken);

        Assert.Equal("editor.document.uri-invalid", Assert.Single(Assert.IsType<DiagnosticsResponse>(result).Diagnostics).Code);
    }

    private static async Task<string> AccsSource() => await File.ReadAllTextAsync(
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "child-care-accs.modeller"), TestContext.Current.CancellationToken);

    private static SemanticId Id(string value) => SemanticId.Parse(value);
}
