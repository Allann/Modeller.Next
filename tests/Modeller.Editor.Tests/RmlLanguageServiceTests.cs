using Modeller.Editor;
using Xunit;

namespace Modeller.Editor.Tests;

public sealed class RmlLanguageServiceTests
{
    [Fact]
    public async Task Child_care_analysis_provides_semantic_completion_navigation_references_and_hover()
    {
        var document = await ChildCare();

        var analysis = RmlLanguageService.Analyze([document], TestContext.Current.CancellationToken);

        Assert.False(analysis.IsCancelled);
        Assert.Empty(analysis.Diagnostics);
        Assert.Contains(RmlLanguageService.Complete(analysis, "Determine"), item => item.Label == "Determine ACCS eligibility" && item.Kind == "Rule");
        var reference = Position(document.Text, "\"Determine ACCS eligibility\"");
        var definition = RmlLanguageService.Definition(analysis, document.Uri, reference);
        Assert.NotNull(definition);
        Assert.Equal(Position(document.Text, "rule Determine ACCS eligibility" ).Line, definition.Range.Start.Line);
        Assert.True(RmlLanguageService.References(analysis, document.Uri, reference).Length >= 2);
        var hover = RmlLanguageService.Hover(analysis, document.Uri, reference);
        Assert.Contains("semantic identity", hover!.Detail);
    }

    [Fact]
    public async Task Rename_preserves_identity_metadata_and_updates_readable_references()
    {
        var document = await ChildCare();
        var analysis = RmlLanguageService.Analyze([document], TestContext.Current.CancellationToken);
        var reference = Position(document.Text, "\"Determine ACCS eligibility\"");

        var edit = RmlLanguageService.Rename(document, analysis, reference, "Assess ACCS eligibility");

        Assert.True(edit.Changed);
        Assert.Contains("# @id=0191f6d4-4ea0-7000-8000-000000000008", edit.Updated);
        Assert.Contains("rule Assess ACCS eligibility", edit.Updated);
        Assert.Contains("requires \"Assess ACCS eligibility\"", edit.Updated);
        var reparsed = RmlLanguageService.Analyze([document with { Text = edit.Updated }], TestContext.Current.CancellationToken);
        Assert.Empty(reparsed.Diagnostics);
        Assert.Equal("0191f6d4-4ea0-7000-8000-000000000008", reparsed.Symbols.Single(symbol => symbol.Name == "Assess ACCS eligibility").Id);
    }

    [Fact]
    public async Task Uuid_free_business_source_keeps_source_mapped_editor_features()
    {
        var original = await ChildCare();
        var text = System.Text.RegularExpressions.Regex.Replace(
            original.Text, "(?m)^\\s*# @id=[0-9a-fA-F-]{36}\\r?\\n", string.Empty);
        var document = original with { Text = text };

        var analysis = RmlLanguageService.Analyze([document], TestContext.Current.CancellationToken);

        Assert.Empty(analysis.Diagnostics);
        var reference = Position(text, "\"Determine ACCS eligibility\"");
        var definition = RmlLanguageService.Definition(analysis, document.Uri, reference);
        Assert.NotNull(definition);
        Assert.Equal(Position(text, "rule Determine ACCS eligibility").Line, definition.Range.Start.Line);
    }

    private static async Task<RmlWorkspaceDocument> ChildCare() => new(
        new("file:///workspace/accs-eligibility.modeller"),
        1,
        await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "child-care-accs.modeller"), TestContext.Current.CancellationToken));

    private static EditorPosition Position(string source, string value)
    {
        var lines = source.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var line = Array.FindIndex(lines, item => item.Contains(value, StringComparison.Ordinal));
        return new(line, lines[line].IndexOf(value, StringComparison.Ordinal) + (value.StartsWith('"') ? 1 : 0));
    }
}
