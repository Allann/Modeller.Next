using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Modeller.Contexts;
using Modeller.Validation;

namespace Modeller.Parsing;

public static partial class DefinitionParser
{
    public static ParseResult Parse(
        IEnumerable<SourceDocument> documents,
        ParseOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(options);
        var sources = documents.OrderBy(document => document.Name, StringComparer.Ordinal).ToArray();
        if (cancellationToken.IsCancellationRequested)
        {
            return new ParseResult(null, [], [], true);
        }

        if (sources.Length == 0)
        {
            return Failure("parse.document.required", "At least one source document is required.");
        }

        if (sources.Any(source => !IsPackageRelative(source.Name)))
        {
            return Failure("parse.path.invalid", "Source document names must remain within the package.");
        }

        if (sources.Sum(source => source.Content.Length) > options.MaximumCharacters)
        {
            return Failure("parse.limit.size", "Readable source exceeds the configured character limit.");
        }

        var statements = new List<Statement>();
        var provenance = ImmutableArray.CreateBuilder<SourceProvenance>();
        var tokenCount = 0;
        foreach (var source in sources)
        {
            var lines = source.Content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
            for (var index = 0; index < lines.Length; index++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return new ParseResult(null, [], [], true);
                }

                var text = lines[index].Trim();
                if (text.Length == 0 || text.StartsWith('#'))
                {
                    continue;
                }

                var separator = text.IndexOf(' ');
                var kind = separator < 0 ? text : text[..separator];
                var matches = Attributes().Matches(text);
                tokenCount += matches.Count;
                if (tokenCount > options.MaximumTokens)
                {
                    return Failure("parse.limit.tokens", "Readable source exceeds the configured token limit.");
                }

                var values = matches
                    .ToDictionary(
                        match => match.Groups["key"].Value,
                        match => match.Groups["quoted"].Success
                            ? match.Groups["quoted"].Value
                            : match.Groups["bare"].Value,
                        StringComparer.Ordinal);
                statements.Add(new Statement(kind, values, source.Name, index + 1, lines[index]));
                if (values.TryGetValue("id", out var id))
                {
                    provenance.Add(new SourceProvenance(id, new SourceSpan(source.Name, index + 1, 1, lines[index].Length)));
                }
            }
        }

        if (statements.Count > options.MaximumStatements)
        {
            return Failure("parse.limit.statements", "Readable source exceeds the configured statement limit.");
        }

        try
        {
            var language = Single(statements, "language");
            var languageVersion = language.Text[(language.Text.IndexOf(' ') + 1)..].Trim();
            if (languageVersion != options.LanguageVersion)
            {
                return LocatedFailure(
                    "parse.language.unsupported",
                    $"Language version '{languageVersion}' is not supported.",
                    language);
            }

            var context = Single(statements, "context");
            var definitions = new JsonArray();
            var exports = new JsonArray();
            foreach (var entity in statements.Where(statement => statement.Kind == "entity"))
            {
                var stages = statements.Where(statement =>
                        statement.Kind == "stage" && Required(statement, "owner") == Required(entity, "id"))
                    .Select(statement => Identity(statement))
                    .ToArray();
                definitions.Add(new JsonObject
                {
                    ["kind"] = "Entity",
                    ["id"] = Required(entity, "id"),
                    ["name"] = Required(entity, "name"),
                    ["slug"] = Required(entity, "slug"),
                    ["lifecycle"] = new JsonObject
                    {
                        ["id"] = Required(entity, "lifecycle-id"),
                        ["name"] = Required(entity, "lifecycle-name"),
                        ["slug"] = Required(entity, "lifecycle-slug"),
                        ["stages"] = new JsonArray(stages)
                    }
                });
            }

            foreach (var fact in statements.Where(statement => statement.Kind == "fact"))
            {
                definitions.Add(new JsonObject
                {
                    ["kind"] = "Fact",
                    ["id"] = Required(fact, "id"),
                    ["name"] = Required(fact, "name"),
                    ["slug"] = Required(fact, "slug"),
                    ["type"] = Required(fact, "type")
                });
                AddExport(fact, exports);
            }

            foreach (var rule in statements.Where(statement => statement.Kind == "rule"))
            {
                var inputs = Split(Required(rule, "inputs"));
                for (var inputIndex = 0; inputIndex < inputs.Length; inputIndex++)
                {
                    var path = $"/definitions/{Required(rule, "id")}/inputFacts/{inputIndex}";
                    provenance.Add(new SourceProvenance(
                        Required(rule, "id"),
                        new SourceSpan(
                            rule.Document,
                            rule.Line,
                            rule.Text.IndexOf(inputs[inputIndex], StringComparison.Ordinal) + 1,
                            inputs[inputIndex].Length),
                        path));
                }

                var expression = Required(rule, "expression");
                if (!expression.StartsWith("and(", StringComparison.Ordinal) || !expression.EndsWith(')'))
                {
                    throw new ParseException("parse.expression.unsupported", "Only the explicit and(...) expression is supported in language 1.0.", rule);
                }

                var trueFindings = Mappings(rule.Values.GetValueOrDefault("true-findings"));
                var falseFindings = Mappings(rule.Values.GetValueOrDefault("false-findings"));
                var missingFindings = Mappings(rule.Values.GetValueOrDefault("missing-findings"));

                definitions.Add(new JsonObject
                {
                    ["kind"] = "Rule",
                    ["id"] = Required(rule, "id"),
                    ["name"] = Required(rule, "name"),
                    ["slug"] = Required(rule, "slug"),
                    ["inputFacts"] = new JsonArray(inputs.Select(id => JsonValue.Create(id)).ToArray()),
                    ["expression"] = new JsonObject
                    {
                        ["kind"] = "And",
                        ["operands"] = new JsonArray(Split(expression[4..^1]).Select(id =>
                        {
                            var operand = new JsonObject { ["kind"] = "Fact", ["factId"] = id };
                            AddOptional(operand, "trueFindingCode", trueFindings.GetValueOrDefault(id));
                            AddOptional(operand, "falseFindingCode", falseFindings.GetValueOrDefault(id));
                            AddOptional(operand, "missingFindingCode", missingFindings.GetValueOrDefault(id));
                            return operand;
                        }).ToArray())
                    },
                    ["conclusions"] = new JsonArray(new JsonObject
                    {
                        ["id"] = Required(rule, "conclusion-id"),
                        ["name"] = Required(rule, "conclusion-name"),
                        ["slug"] = Required(rule, "conclusion-slug")
                    })
                });
                AddExport(rule, exports);
            }

            foreach (var decision in statements.Where(statement => statement.Kind == "decision"))
            {
                var owner = Required(decision, "id");
                var conclusions = statements.Where(item =>
                        item.Kind == "decision-conclusion" && Required(item, "owner") == owner)
                    .Select(Identity).ToArray();
                var rows = statements.Where(item =>
                        item.Kind == "decision-row" && Required(item, "owner") == owner)
                    .Select(DecisionRow).ToArray();
                definitions.Add(new JsonObject
                {
                    ["kind"] = "Decision",
                    ["id"] = owner,
                    ["name"] = Required(decision, "name"),
                    ["slug"] = Required(decision, "slug"),
                    ["inputFacts"] = new JsonArray(Split(Required(decision, "inputs")).Select(id => JsonValue.Create(id)).ToArray()),
                    ["conclusions"] = new JsonArray(conclusions),
                    ["table"] = new JsonObject
                    {
                        ["hitPolicy"] = Required(decision, "hit-policy"),
                        ["rows"] = new JsonArray(rows)
                    }
                });
                AddExport(decision, exports);
            }

            foreach (var behaviour in statements.Where(statement => statement.Kind == "behaviour"))
            {
                var owner = Required(behaviour, "id");
                var outcomes = statements.Where(item => item.Kind == "outcome" && Required(item, "owner") == owner)
                    .Select(Identity).ToArray();
                var bindings = statements.Where(item => item.Kind == "binding" && Required(item, "owner") == owner)
                    .Select(Binding).ToArray();
                var transitions = statements.Where(item => item.Kind == "transition" && Required(item, "owner") == owner)
                    .Select(Transition).ToArray();
                definitions.Add(new JsonObject
                {
                    ["kind"] = "Behaviour",
                    ["id"] = owner,
                    ["name"] = Required(behaviour, "name"),
                    ["slug"] = Required(behaviour, "slug"),
                    ["entity"] = Required(behaviour, "entity"),
                    ["outcomes"] = new JsonArray(outcomes),
                    ["ruleBindings"] = new JsonArray(bindings),
                    ["transitions"] = new JsonArray(transitions)
                });
            }

            var root = new JsonObject
            {
                ["schemaVersion"] = "1.0",
                ["languageVersion"] = languageVersion,
                ["context"] = new JsonObject
                {
                    ["id"] = Required(context, "id"),
                    ["name"] = Required(context, "name"),
                    ["slug"] = Required(context, "slug"),
                    ["version"] = Required(context, "version")
                },
                ["imports"] = new JsonArray(),
                ["exports"] = exports,
                ["definitions"] = definitions
            };
            var loaded = ContextPackageSystem.Load(JsonSerializer.SerializeToUtf8Bytes(root));
            if (!loaded.IsSuccess)
            {
                return Failure("parse.model.invalid", "Parsed source could not form a canonical authored model.");
            }

            var validation = SemanticValidation.Validate(
                ValidationRequest.For(loaded.Package!, ValidationProfile.Canonical),
                cancellationToken);
            var parsedDiagnostics = validation.Diagnostics.Select(diagnostic =>
            {
                var source = provenance.LastOrDefault(item => item.SemanticPath == diagnostic.Path) ??
                    provenance.FirstOrDefault(item => item.SemanticId == diagnostic.SubjectId);
                return new ParseDiagnostic(diagnostic.Code, diagnostic.Message, source?.Span);
            }).ToImmutableArray();
            return new ParseResult(loaded.Package, provenance.ToImmutable(), parsedDiagnostics, validation.IsCancelled);
        }
        catch (ParseException exception)
        {
            return LocatedFailure(exception.Code, exception.Message, exception.Statement);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure("parse.source.invalid", "Readable source is malformed or incomplete.");
        }
    }

    private static JsonObject Identity(Statement statement) => new()
    {
        ["id"] = Required(statement, "id"),
        ["name"] = Required(statement, "name"),
        ["slug"] = Required(statement, "slug")
    };

    private static JsonObject Binding(Statement statement)
    {
        var facts = new JsonObject();
        foreach (var pair in Split(Required(statement, "facts")))
        {
            var values = pair.Split(':');
            facts[values[0]] = values[1];
        }

        return new JsonObject
        {
            ["rule"] = Required(statement, "rule"),
            ["purpose"] = Required(statement, "purpose"),
            ["factBindings"] = facts
        };
    }

    private static JsonObject DecisionRow(Statement statement)
    {
        var missingFindings = Mappings(statement.Values.GetValueOrDefault("missing-findings"));
        var conditions = Split(Required(statement, "conditions")).Select(item =>
        {
            var pair = item.Split(':', 2);
            var condition = new JsonObject
            {
                ["factId"] = pair[0],
                ["expected"] = pair[1] switch
                {
                    "true" => JsonValue.Create(true),
                    "false" => JsonValue.Create(false),
                    "any" => JsonValue.Create("Any"),
                    _ => throw new ParseException("parse.decision.condition-invalid", "Decision conditions must use true, false, or any.", statement)
                }
            };
            AddOptional(condition, "missingFindingCode", missingFindings.GetValueOrDefault(pair[0]));
            return condition;
        }).ToArray();
        return new JsonObject
        {
            ["id"] = Required(statement, "id"),
            ["name"] = Required(statement, "name"),
            ["slug"] = Required(statement, "slug"),
            ["conditions"] = new JsonArray(conditions),
            ["conclusion"] = Required(statement, "conclusion"),
            ["findingCode"] = Required(statement, "finding-code")
        };
    }

    private static JsonObject Transition(Statement statement) => new()
    {
        ["id"] = Required(statement, "id"),
        ["name"] = Required(statement, "name"),
        ["slug"] = Required(statement, "slug"),
        ["lifecycle"] = Required(statement, "lifecycle"),
        ["sourceStage"] = Required(statement, "source"),
        ["targetStage"] = Required(statement, "target"),
        ["outcome"] = Required(statement, "outcome")
    };

    private static void AddExport(Statement statement, JsonArray exports)
    {
        if (statement.Values.TryGetValue("export", out var value) && value == "true") exports.Add(Required(statement, "id"));
    }

    private static string[] Split(string value) => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    private static Dictionary<string, string> Mappings(string? value) =>
        value is null ? [] : Split(value).Select(item => item.Split(':', 2)).ToDictionary(item => item[0], item => item[1], StringComparer.Ordinal);
    private static void AddOptional(JsonObject target, string name, string? value)
    {
        if (value is not null) target[name] = value;
    }
    private static bool IsPackageRelative(string name)
    {
        var normalized = name.Replace('\\', '/');
        return !string.IsNullOrWhiteSpace(name) &&
            !Path.IsPathRooted(name) &&
            !normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).Contains("..", StringComparer.Ordinal);
    }

    private static Statement Single(IEnumerable<Statement> statements, string kind) =>
        statements.SingleOrDefault(statement => statement.Kind == kind) ?? throw new ParseException("parse.statement.required", $"One '{kind}' statement is required.", statements.First());
    private static string Required(Statement statement, string key) =>
        statement.Values.TryGetValue(key, out var value) && value.Length > 0 ? value : throw new ParseException("parse.attribute.required", $"'{key}' is required on '{statement.Kind}'.", statement);
    private static ParseResult Failure(string code, string message) => new(null, [], [new ParseDiagnostic(code, message, null)], false);
    private static ParseResult LocatedFailure(string code, string message, Statement statement) =>
        new(null, [], [new ParseDiagnostic(code, message, new SourceSpan(statement.Document, statement.Line, 1, statement.Text.Length))], false);

    [GeneratedRegex("(?<key>[a-z][a-z-]*)=(?:\"(?<quoted>[^\"]*)\"|(?<bare>\\S+))", RegexOptions.CultureInvariant)]
    private static partial Regex Attributes();
    private sealed record Statement(string Kind, Dictionary<string, string> Values, string Document, int Line, string Text);
    private sealed class ParseException(string code, string message, Statement statement) : Exception(message) { public string Code { get; } = code; public Statement Statement { get; } = statement; }
}

public sealed record SourceDocument(string Name, string Content);
public sealed record ParseOptions
{
    public ParseOptions(string languageVersion, int maximumCharacters, int maximumStatements, int maximumTokens)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(languageVersion);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumCharacters);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumStatements);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumTokens);
        LanguageVersion = languageVersion;
        MaximumCharacters = maximumCharacters;
        MaximumStatements = maximumStatements;
        MaximumTokens = maximumTokens;
    }

    public string LanguageVersion { get; }
    public int MaximumCharacters { get; }
    public int MaximumStatements { get; }
    public int MaximumTokens { get; }
    public static ParseOptions Language1 { get; } = new("1.0", 1_048_576, 10_000, 100_000);
}
public sealed record SourceSpan(string Document, int Line, int Column, int Length);
public sealed record SourceProvenance(string SemanticId, SourceSpan Span, string? SemanticPath = null);
public sealed record ParseDiagnostic(string Code, string Message, SourceSpan? Location);
public sealed record ParseResult(LoadedContextPackage? Package, ImmutableArray<SourceProvenance> Provenance, ImmutableArray<ParseDiagnostic> Diagnostics, bool IsCancelled)
{
    public bool IsSuccess => Package is not null && Diagnostics.IsEmpty && !IsCancelled;
}
