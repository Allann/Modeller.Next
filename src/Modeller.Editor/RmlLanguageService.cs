using System.Collections.Immutable;
using Modeller.Model;
using Modeller.Parsing;

namespace Modeller.Editor;

public sealed record RmlWorkspaceDocument(Uri Uri, long Version, string Text);
public sealed record RmlLocation(Uri Uri, EditorRange Range);
public sealed record RmlSymbol(string Id, string Name, string Kind, RmlLocation Declaration, ImmutableArray<RmlLocation> References);
public sealed record RmlCompletion(string Label, string Kind, string Detail);
public sealed record RmlHover(string Title, string Detail, EditorRange Range);
public sealed record RmlSemanticToken(Uri Uri, EditorRange Range, string Kind);
public sealed record RmlLocatedDiagnostic(Uri Uri, EditorDiagnostic Diagnostic);
public sealed record RmlWorkspaceAnalysis(
    ImmutableArray<EditorDiagnostic> Diagnostics,
    ImmutableArray<RmlLocatedDiagnostic> LocatedDiagnostics,
    ImmutableArray<RmlSymbol> Symbols,
    ImmutableArray<RmlSemanticToken> Tokens,
    bool IsCancelled);

public static class RmlLanguageService
{
    private static readonly ImmutableArray<RmlCompletion> Keywords =
    [
        new("context", "keyword", "Declare the owning bounded context."),
        new("entity", "keyword", "Declare a domain concept with stable identity."),
        new("field", "keyword", "Declare typed data owned by an Entity."),
        new("relationship", "keyword", "Declare a cardinality-aware reference between Entities."),
        new("enumeration", "keyword", "Declare a closed set of named values."),
        new("member", "keyword", "Declare an Enumeration value."),
        new("lifecycle", "keyword", "Declare the governed stages of an Entity."),
        new("stage", "keyword", "Declare a Lifecycle stage."),
        new("fact", "keyword", "Declare typed information used by Rules."),
        new("rule", "keyword", "Declare a reusable explained decision."),
        new("behaviour", "keyword", "Declare an action and its Outcomes."),
        new("outcome", "keyword", "Declare a business result of a Behaviour."),
        new("transition", "keyword", "Declare an Outcome-linked Lifecycle change."),
        new("requires", "keyword", "Bind a Rule as a Behaviour requirement.")
    ];

    public static RmlWorkspaceAnalysis Analyze(
        IEnumerable<RmlWorkspaceDocument> documents,
        CancellationToken cancellationToken = default)
    {
        var sourceDocuments = documents.OrderBy(document => document.Uri.AbsoluteUri, StringComparer.Ordinal)
            .Select(document => new SourceDocument(Name(document.Uri), document.Text)).ToArray();
        var byName = documents.ToDictionary(document => Name(document.Uri), StringComparer.Ordinal);
        var parsed = DefinitionParser.Parse(sourceDocuments, ParseOptions.EditorLanguage1, cancellationToken);
        if (parsed.IsCancelled) return new([], [], [], [], true);
        var locatedDiagnostics = parsed.Diagnostics.Select(diagnostic => new RmlLocatedDiagnostic(
            diagnostic.Location is not null && byName.TryGetValue(diagnostic.Location.Document, out var source) ? source.Uri : documents.First().Uri,
            ToDiagnostic(diagnostic))).ToImmutableArray();
        var diagnostics = locatedDiagnostics.Select(item => item.Diagnostic).ToImmutableArray();
        if (parsed.Package is null) return new(diagnostics, locatedDiagnostics, [], Tokens(documents), false);
        var concepts = Concepts(parsed.Package.AuthoredRevision).ToArray();
        var symbols = concepts.Select(concept =>
        {
            var provenance = parsed.Provenance.First(item => item.SemanticId == concept.Id.ToString() && item.SemanticPath is null);
            var document = byName[provenance.Span.Document];
            var references = FindReferences(concept.Name.Value, documents, provenance.Span).ToImmutableArray();
            return new RmlSymbol(concept.Id.ToString(), concept.Name.Value, concept.GetType().Name.Replace("Definition", string.Empty, StringComparison.Ordinal),
                new(document.Uri, Range(provenance.Span)), references);
        }).ToImmutableArray();
        return new(diagnostics, locatedDiagnostics, symbols, Tokens(documents), false);
    }

    public static ImmutableArray<RmlCompletion> Complete(RmlWorkspaceAnalysis analysis, string linePrefix)
    {
        var semantic = analysis.Symbols.Select(symbol => new RmlCompletion(symbol.Name, symbol.Kind, $"Resolved {symbol.Kind} in this workspace."));
        return Keywords.Concat(semantic).Where(item => linePrefix.Length == 0 || item.Label.StartsWith(linePrefix, StringComparison.OrdinalIgnoreCase))
            .DistinctBy(item => item.Label, StringComparer.OrdinalIgnoreCase).OrderBy(item => item.Label, StringComparer.OrdinalIgnoreCase).ToImmutableArray();
    }

    public static RmlHover? Hover(RmlWorkspaceAnalysis analysis, Uri uri, EditorPosition position)
    {
        var symbol = analysis.Symbols.FirstOrDefault(item => Contains(item.Declaration, uri, position) || item.References.Any(reference => Contains(reference, uri, position)));
        return symbol is null ? null : new(symbol.Name, $"{symbol.Kind} · semantic identity {symbol.Id}",
            Contains(symbol.Declaration, uri, position) ? symbol.Declaration.Range : symbol.References.First(reference => Contains(reference, uri, position)).Range);
    }

    public static RmlLocation? Definition(RmlWorkspaceAnalysis analysis, Uri uri, EditorPosition position) =>
        analysis.Symbols.FirstOrDefault(item => Contains(item.Declaration, uri, position) || item.References.Any(reference => Contains(reference, uri, position)))?.Declaration;

    public static ImmutableArray<RmlLocation> References(RmlWorkspaceAnalysis analysis, Uri uri, EditorPosition position, bool includeDeclaration = true)
    {
        var symbol = analysis.Symbols.FirstOrDefault(item => Contains(item.Declaration, uri, position) || item.References.Any(reference => Contains(reference, uri, position)));
        return symbol is null ? [] : includeDeclaration ? [symbol.Declaration, .. symbol.References] : symbol.References;
    }

    public static RmlSourceEdit Rename(RmlWorkspaceDocument document, RmlWorkspaceAnalysis analysis, EditorPosition position, string newName)
    {
        var symbol = analysis.Symbols.FirstOrDefault(item => Contains(item.Declaration, document.Uri, position) || item.References.Any(reference => Contains(reference, document.Uri, position)))
            ?? throw new InvalidOperationException("No semantic symbol exists at the requested position.");
        return RmlCompiler.Rename(document.Text, symbol.Name, newName);
    }

    private static IEnumerable<ISemanticConcept> Concepts(AuthoredContextRevision revision)
    {
        foreach (var definition in revision.Definitions)
        {
            yield return definition;
            if (definition is EntityDefinition entity)
            {
                if (entity.Lifecycle is not null)
                {
                    yield return entity.Lifecycle;
                    foreach (var stage in entity.Lifecycle.Stages) yield return stage;
                }
                foreach (var field in entity.Fields) yield return field;
                foreach (var relationship in entity.Relationships) yield return relationship;
            }
            if (definition is EnumerationDefinition enumeration) foreach (var member in enumeration.Members) yield return member;
            if (definition is RuleDefinition rule) foreach (var conclusion in rule.Conclusions) yield return conclusion;
            if (definition is BehaviourDefinition behaviour)
            {
                foreach (var outcome in behaviour.Outcomes) yield return outcome;
                foreach (var transition in behaviour.Transitions) yield return transition;
            }
        }
    }

    private static IEnumerable<RmlLocation> FindReferences(string name, IEnumerable<RmlWorkspaceDocument> documents, SourceSpan declaration)
    {
        var quoted = $"\"{name}\"";
        foreach (var document in documents)
        {
            var lines = document.Text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
            for (var line = 0; line < lines.Length; line++)
            {
                var start = 0;
                while ((start = lines[line].IndexOf(quoted, start, StringComparison.OrdinalIgnoreCase)) >= 0)
                {
                    yield return new(document.Uri, new(new(line, start + 1), new(line, start + 1 + name.Length)));
                    start += quoted.Length;
                }
            }
        }
    }

    private static ImmutableArray<RmlSemanticToken> Tokens(IEnumerable<RmlWorkspaceDocument> documents)
    {
        var tokens = ImmutableArray.CreateBuilder<RmlSemanticToken>();
        foreach (var document in documents)
        {
            var lines = document.Text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
            for (var line = 0; line < lines.Length; line++)
            {
                var trimmed = lines[line].TrimStart();
                if (trimmed.Length == 0) continue;
                var column = lines[line].Length - trimmed.Length;
                var length = trimmed.IndexOf(' ') is var separator && separator >= 0 ? separator : trimmed.Length;
                tokens.Add(new(document.Uri, new(new(line, column), new(line, column + length)), trimmed.StartsWith('#') ? "comment" : "keyword"));
            }
        }
        return tokens.ToImmutable();
    }

    private static bool Contains(RmlLocation location, Uri uri, EditorPosition position) => location.Uri == uri &&
        position.Line >= location.Range.Start.Line && position.Line <= location.Range.End.Line &&
        (position.Line != location.Range.Start.Line || position.Character >= location.Range.Start.Character) &&
        (position.Line != location.Range.End.Line || position.Character <= location.Range.End.Character);
    private static string Name(Uri uri) => Uri.UnescapeDataString(uri.Segments[^1]);
    private static EditorDiagnostic ToDiagnostic(ParseDiagnostic diagnostic)
    {
        var span = diagnostic.Location;
        var start = new EditorPosition(Math.Max(0, (span?.Line ?? 1) - 1), Math.Max(0, (span?.Column ?? 1) - 1));
        return new(diagnostic.Code, diagnostic.Message, EditorDiagnosticSeverity.Error, new(start, new(start.Line, start.Character + (span?.Length ?? 0))));
    }
    private static EditorRange Range(SourceSpan span)
    {
        var start = new EditorPosition(span.Line - 1, span.Column - 1);
        return new(start, new(start.Line, start.Character + span.Length));
    }
}
