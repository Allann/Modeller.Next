using System.Collections.Immutable;

namespace Modeller.Parsing;

public sealed record RmlDeclaredSymbol(string Name, string Kind);
public sealed record RmlCompletionCandidate(string Label, string Kind, string Detail, string InsertText, int ReplacementStartColumn);

public static class RmlGrammar
{
    private static readonly ImmutableDictionary<string, ImmutableArray<string>> Statements =
        new Dictionary<string, ImmutableArray<string>>(StringComparer.Ordinal)
        {
            [""] = ["rml", "context", "entity", "enumeration", "fact", "rule", "behaviour"],
            ["context"] = ["version", "import"],
            ["import"] = ["from"],
            ["entity"] = ["lifecycle", "field", "relationship"],
            ["lifecycle"] = ["stage"],
            ["field"] = ["type", "optional"],
            ["relationship"] = ["target", "cardinality", "optional"],
            ["enumeration"] = ["member"],
            ["member"] = ["value"],
            ["fact"] = ["type", "export"],
            ["rule"] = ["input", "when", "conclusion", "finding", "export"],
            ["when"] = ["fact"],
            ["behaviour"] = ["for", "requires", "outcome", "transition", "event", "effect"],
            ["transition"] = ["lifecycle", "from", "to", "outcome"],
        }.ToImmutableDictionary(StringComparer.Ordinal);

    public static ImmutableArray<string> AllowedStatements(string? parent) =>
        Statements.GetValueOrDefault(parent ?? "", []);

    public static bool IsAllowedStatement(string keyword, string? parent) =>
        AllowedStatements(parent).Contains(keyword, StringComparer.Ordinal);

    public static string? ParentAt(string source, int oneBasedLine)
    {
        var stack = new Stack<string>();
        var lines = source.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        for (var index = 0; index < Math.Min(oneBasedLine - 1, lines.Length); index++)
        {
            var text = lines[index].Trim();
            if (text.Length == 0 || text.StartsWith('#')) continue;
            if (text == "end") { if (stack.Count > 0) stack.Pop(); continue; }
            var keyword = text.Split(' ', 2)[0];
            if (BlockKeywords.Contains(keyword))
                stack.Push(keyword);
        }
        return stack.TryPeek(out var parent) ? parent : null;
    }

    private static readonly HashSet<string> BlockKeywords = new(StringComparer.Ordinal)
    {
        "context", "entity", "lifecycle", "field", "relationship", "enumeration", "member",
        "fact", "rule", "when", "behaviour", "transition", "import", "event", "effect",
    };

    public static ImmutableArray<RmlCompletionCandidate> Complete(
        IEnumerable<SourceDocument> documents,
        string activeDocument,
        int oneBasedLine,
        int oneBasedColumn,
        CancellationToken cancellationToken = default)
    {
        var documentArray = documents.ToArray();
        var source = documentArray.FirstOrDefault(item => item.Name == activeDocument)?.Content ?? string.Empty;
        var lines = source.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var line = lines.ElementAtOrDefault(Math.Max(0, oneBasedLine - 1)) ?? string.Empty;
        var before = line[..Math.Min(Math.Max(0, oneBasedColumn - 1), line.Length)];
        var trimmed = before.TrimStart();
        var indentation = before.Length - trimmed.Length;
        var separator = trimmed.IndexOf(' ');
        var statement = separator < 0 ? trimmed : trimmed[..separator];
        var parent = ParentAt(source, oneBasedLine);
        var referenceKind = ReferenceKind(statement, parent, trimmed);

        IEnumerable<RmlCompletionCandidate> candidates;
        if (referenceKind is not null)
        {
            var quote = before.LastIndexOf('"');
            var hasOpenQuote = quote >= 0 && before[(quote + 1)..].IndexOf('"') < 0;
            var prefixStart = hasOpenQuote ? quote + 1 : before.LastIndexOf(' ') + 1;
            var prefix = before[prefixStart..];
            candidates = Declarations(documentArray, cancellationToken)
                .Where(item => item.Kind == referenceKind && item.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Select(item => new RmlCompletionCandidate(
                    item.Name, item.Kind, $"Reference the {Words(item.Kind)} '{item.Name}' from this workspace.",
                    hasOpenQuote ? item.Name : $"\"{item.Name}\"", prefixStart + 1));
        }
        else
        {
            var prefix = statement;
            candidates = AllowedStatements(parent)
                .Where(item => item.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Select(item => new RmlCompletionCandidate(
                    item, "keyword", $"Valid inside {parent ?? "the document root"}.", item, indentation + 1));
        }

        return candidates.DistinctBy(item => (item.Kind, item.Label))
            .OrderBy(item => item.Label, StringComparer.OrdinalIgnoreCase).ToImmutableArray();
    }

    public static ImmutableArray<RmlDeclaredSymbol> Declarations(
        IEnumerable<SourceDocument> documents,
        CancellationToken cancellationToken = default)
    {
        var symbols = ImmutableArray.CreateBuilder<RmlDeclaredSymbol>();
        foreach (var document in documents.OrderBy(item => item.Name, StringComparer.Ordinal))
        {
            foreach (var raw in document.Content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var text = raw.Trim();
                if (text.Length == 0 || text.StartsWith('#')) continue;
                var separator = text.IndexOf(' ');
                if (separator < 0) continue;
                var keyword = text[..separator];
                if (!DeclarationKinds.TryGetValue(keyword, out var kind)) continue;
                var name = text[(separator + 1)..].Trim().Trim('"');
                if (name.Length > 0) symbols.Add(new(name, kind));
            }
        }
        return symbols.Distinct().OrderBy(item => item.Kind, StringComparer.Ordinal)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToImmutableArray();
    }

    private static string? ReferenceKind(string statement, string? parent, string trimmed) =>
        parent == "field" && statement == "type"
            ? FieldTypeReferenceKind(trimmed)
            : parent is not null && ReferenceKinds.TryGetValue((parent, statement), out var kind) ? kind : null;

    private static string? FieldTypeReferenceKind(string trimmed) =>
        trimmed.StartsWith("type enumeration", StringComparison.Ordinal) ? "Enumeration" :
        trimmed.StartsWith("type entity", StringComparison.Ordinal) ? "Entity" : null;

    private static readonly Dictionary<(string Parent, string Statement), string> ReferenceKinds = new()
    {
        [("behaviour", "for")] = "Entity",
        [("behaviour", "requires")] = "Rule",
        [("transition", "lifecycle")] = "Lifecycle",
        [("transition", "from")] = "LifecycleStage",
        [("transition", "to")] = "LifecycleStage",
        [("transition", "outcome")] = "Outcome",
        [("relationship", "target")] = "Entity",
        [("import", "from")] = "Context",
        [("rule", "input")] = "Fact",
        [("rule", "finding")] = "Fact",
        [("when", "fact")] = "Fact",
    };

    private static string Words(string value) => string.Concat(value.Select((character, index) =>
        index > 0 && char.IsUpper(character) ? $" {char.ToLowerInvariant(character)}" : char.ToLowerInvariant(character).ToString()));

    private static readonly ImmutableDictionary<string, string> DeclarationKinds =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["context"] = "Context", ["entity"] = "Entity", ["lifecycle"] = "Lifecycle",
            ["stage"] = "LifecycleStage", ["field"] = "Field", ["relationship"] = "Relationship",
            ["enumeration"] = "Enumeration", ["member"] = "EnumerationMember", ["fact"] = "Fact",
            ["rule"] = "Rule", ["conclusion"] = "Conclusion", ["behaviour"] = "Behaviour",
            ["outcome"] = "Outcome", ["transition"] = "Transition",
            ["event"] = "Event", ["effect"] = "Effect",
        }.ToImmutableDictionary(StringComparer.Ordinal);
}
