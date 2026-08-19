using System.Collections.Immutable;

namespace Modeller.Parsing;

public static class RmlGrammar
{
    private static readonly ImmutableDictionary<string, ImmutableArray<string>> Statements =
        new Dictionary<string, ImmutableArray<string>>(StringComparer.Ordinal)
        {
            [""] = ["rml", "context", "entity", "enumeration", "fact", "rule", "behaviour"],
            ["context"] = ["version"],
            ["entity"] = ["lifecycle", "field", "relationship"],
            ["lifecycle"] = ["stage"],
            ["field"] = ["type", "optional"],
            ["relationship"] = ["target", "cardinality", "optional"],
            ["enumeration"] = ["member"],
            ["member"] = ["value"],
            ["fact"] = ["type", "export"],
            ["rule"] = ["input", "when", "conclusion", "finding", "export"],
            ["when"] = ["fact"],
            ["behaviour"] = ["for", "requires", "outcome", "transition"],
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
            if (keyword is "context" or "entity" or "lifecycle" or "field" or "relationship" or "enumeration" or "member" or "fact" or "rule" or "when" or "behaviour" or "transition")
                stack.Push(keyword);
        }
        return stack.TryPeek(out var parent) ? parent : null;
    }
}
