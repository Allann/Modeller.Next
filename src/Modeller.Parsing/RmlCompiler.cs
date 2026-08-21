using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace Modeller.Parsing;

public static partial class RmlCompiler
{
    public static bool IsRml(IEnumerable<SourceDocument> documents) => documents.Any(document =>
        document.Content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n')
            .Any(line => line.Trim().StartsWith("rml ", StringComparison.Ordinal)));

    public static ParseResult Compile(
        IEnumerable<SourceDocument> documents,
        ParseOptions options,
        CancellationToken cancellationToken = default)
    {
        var sources = documents.OrderBy(document => document.Name, StringComparer.Ordinal).ToArray();
        if (cancellationToken.IsCancellationRequested) return new(null, [], [], true);
        var parsed = ParseNodes(sources, options, cancellationToken);
        if (cancellationToken.IsCancellationRequested) return new(null, [], [], true);
        if (parsed.Diagnostic is not null) return new(null, [], [parsed.Diagnostic], false);
        try
        {
            var model = Build(parsed.Roots);
            var saf = new SourceDocument(".modeller/compiled.rml.saf", model.Saf);
            var result = DefinitionParser.Parse([saf], options, cancellationToken);
            var provenance = model.Symbols.Select(symbol => new SourceProvenance(
                symbol.Id,
                new SourceSpan(symbol.Document, symbol.Line, symbol.Column, symbol.Length),
                symbol.SemanticPath)).ToImmutableArray();
            var diagnostics = result.Diagnostics.Select(diagnostic =>
            {
                var generated = result.Provenance.LastOrDefault(item => item.Span.Line == diagnostic.Location?.Line);
                var source = generated is null ? null : provenance.LastOrDefault(item =>
                    item.SemanticId == generated.SemanticId && item.SemanticPath == generated.SemanticPath)
                    ?? provenance.FirstOrDefault(item => item.SemanticId == generated.SemanticId);
                return diagnostic with { Location = source?.Span };
            }).ToImmutableArray();
            return result with { Provenance = provenance, Diagnostics = diagnostics };
        }
        catch (RmlException exception)
        {
            return new(null, [], [new(exception.Code, exception.Message, Span(exception.Node))], false);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return new(null, [], [new("rml.source.invalid", "RML source is malformed or incomplete.", null)], false);
        }
    }

    public static RmlSourceEdit Rename(string source, string oldName, string newName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(oldName);
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);
        var declaration = new Regex($"^(?<indent>\\s*)(?<kind>context|entity|lifecycle|stage|fact|rule|conclusion|behaviour|outcome|transition)\\s+{Regex.Escape(oldName)}\\s*$",
            RegexOptions.Multiline | RegexOptions.CultureInvariant);
        var updated = declaration.Replace(source, match => $"{match.Groups["indent"].Value}{match.Groups["kind"].Value} {newName}");
        updated = updated.Replace($"\"{oldName}\"", $"\"{newName}\"", StringComparison.Ordinal);
        return new(source, updated, !string.Equals(source, updated, StringComparison.Ordinal));
    }

    public static RmlSourceEdit EnsureIdentities(string source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        var lines = source.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var output = new List<string>(lines.Length);
        for (var index = 0; index < lines.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = lines[index]; var trimmed = line.TrimStart();
            var separator = trimmed.IndexOf(' '); var keyword = separator < 0 ? trimmed : trimmed[..separator];
            var value = separator < 0 ? string.Empty : trimmed[(separator + 1)..].TrimStart();
            var declaration = IdentityDeclarations.Contains(keyword) && !value.StartsWith('"');
            var hasIdentity = output.LastOrDefault(item => item.Trim().Length > 0)?.TrimStart().StartsWith("# @id=", StringComparison.Ordinal) == true;
            if (declaration && !hasIdentity)
            {
                var indentation = line[..(line.Length - trimmed.Length)];
                output.Add($"{indentation}# @id={Guid.CreateVersion7()}");
            }
            output.Add(line);
        }
        var updated = string.Join('\n', output);
        return new(source, updated, !string.Equals(source, updated, StringComparison.Ordinal));
    }

    public static RmlSourceEdit ApplyIdentities(string source, IReadOnlyList<string> identities, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(identities);
        var lines = source.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var output = new List<string>(lines.Length + identities.Count);
        var identityIndex = 0;
        foreach (var line in lines)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var trimmed = line.TrimStart();
            var separator = trimmed.IndexOf(' ');
            var keyword = separator < 0 ? trimmed : trimmed[..separator];
            var value = separator < 0 ? string.Empty : trimmed[(separator + 1)..].TrimStart();
            var declaration = IdentityDeclarations.Contains(keyword) && !value.StartsWith('"');
            var hasIdentity = output.LastOrDefault(item => item.Trim().Length > 0)?.TrimStart().StartsWith("# @id=", StringComparison.Ordinal) == true;
            if (declaration)
            {
                if (identityIndex >= identities.Count) throw new ArgumentException("The identity registry does not cover every RML declaration.", nameof(identities));
                if (!Guid.TryParse(identities[identityIndex], out var identity) || identity.Version != 7)
                    throw new ArgumentException("The identity registry contains an invalid identity.", nameof(identities));
                if (!hasIdentity)
                {
                    var indentation = line[..(line.Length - trimmed.Length)];
                    output.Add($"{indentation}# @id={identity}");
                }
                identityIndex++;
            }
            output.Add(line);
        }
        if (identityIndex != identities.Count) throw new ArgumentException("The identity registry contains unused identities.", nameof(identities));
        var updated = string.Join('\n', output);
        return new(source, updated, !string.Equals(source, updated, StringComparison.Ordinal));
    }

    /// <summary>
    /// Reads the ordered "# @id=" sequence already present in <paramref name="source"/> — the
    /// inverse of <see cref="ApplyIdentities"/>. Used by workspace export to materialize a durable
    /// registry from a document that has already had identities minted (via
    /// <see cref="EnsureIdentities"/>) or applied (via <see cref="ApplyIdentities"/>).
    /// </summary>
    /// <exception cref="ArgumentException">
    /// An identity-bearing declaration lacks a preceding "# @id=" comment, or that comment's value
    /// is not a valid UUIDv7 — the source must already be fully identified before harvesting.
    /// </exception>
    public static ImmutableArray<string> HarvestIdentities(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var lines = source.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var identities = ImmutableArray.CreateBuilder<string>();
        string? lastNonBlank = null;
        foreach (var line in lines)
        {
            var trimmed = line.TrimStart();
            var separator = trimmed.IndexOf(' ');
            var keyword = separator < 0 ? trimmed : trimmed[..separator];
            var value = separator < 0 ? string.Empty : trimmed[(separator + 1)..].TrimStart();
            var declaration = IdentityDeclarations.Contains(keyword) && !value.StartsWith('"');
            if (declaration)
            {
                var identityMatch = lastNonBlank is null ? null : Identity().Match(lastNonBlank.TrimStart());
                if (identityMatch is not { Success: true })
                    throw new ArgumentException($"'{keyword} {value}' requires tooling-managed '# @id=<uuidv7>' metadata.", nameof(source));
                var identity = identityMatch.Groups["id"].Value;
                if (!Guid.TryParse(identity, out var parsed) || parsed.Version != 7)
                    throw new ArgumentException($"'{keyword} {value}' has an invalid identity.", nameof(source));
                identities.Add(identity);
            }
            if (trimmed.Trim().Length > 0) lastNonBlank = line;
        }
        return identities.ToImmutable();
    }

    private static (ImmutableArray<Node> Roots, ParseDiagnostic? Diagnostic) ParseNodes(
        SourceDocument[] sources, ParseOptions options, CancellationToken cancellationToken)
    {
        if (sources.Sum(source => source.Content.Length) > options.MaximumCharacters)
            return ([], new("parse.limit.size", "RML source exceeds the configured character limit.", null));
        if (sources.Any(source => !IsPackageRelative(source.Name)))
            return ([], new("parse.path.invalid", "RML document names must remain within the package.", null));
        var roots = ImmutableArray.CreateBuilder<Node>();
        var stack = new Stack<Node>();
        string? pendingId = null;
        var count = 0;
        var tokenCount = 0;
        foreach (var source in sources)
        {
            var lines = source.Content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
            for (var index = 0; index < lines.Length; index++)
            {
                if (cancellationToken.IsCancellationRequested) return ([], null);
                var raw = lines[index];
                var text = raw.Trim();
                if (text.Length == 0) continue;
                tokenCount += text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
                if (tokenCount > options.MaximumTokens)
                    return ([], new("parse.limit.tokens", "RML source exceeds the configured token limit.", new(source.Name, index + 1, 1, raw.Length)));
                var identity = Identity().Match(text);
                if (identity.Success) { pendingId = identity.Groups["id"].Value; continue; }
                if (text.StartsWith('#')) continue;
                if (++count > options.MaximumStatements)
                    return ([], new("parse.limit.statements", "RML source exceeds the configured statement limit.", new(source.Name, index + 1, 1, raw.Length)));
                if (text == "end")
                {
                    if (stack.Count == 0) return ([], new("rml.block.unexpected-end", "An 'end' has no open RML block.", new(source.Name, index + 1, 1, raw.Length)));
                    stack.Pop(); continue;
                }
                var split = text.IndexOf(' ');
                var keyword = split < 0 ? text : text[..split];
                var value = split < 0 ? string.Empty : Unquote(text[(split + 1)..].Trim());
                var parentKeyword = stack.TryPeek(out var parent) ? parent.Keyword : null;
                if (!RmlGrammar.IsAllowedStatement(keyword, parentKeyword))
                    return ([], new("rml.statement.unexpected", $"'{keyword}' is not valid inside '{parentKeyword ?? "the document root"}'.", new(source.Name, index + 1, 1, raw.Length)));
                var nodeId = pendingId;
                if (nodeId is null && options.AllowTransientRmlIdentities && IdentityDeclarations.Contains(keyword) && !value.StartsWith('"'))
                    nodeId = Guid.CreateVersion7().ToString();
                var node = new Node(keyword, value, nodeId, source.Name, index + 1,
                    raw.IndexOf(keyword, StringComparison.Ordinal) + 1, raw.Length, []);
                pendingId = null;
                if (parent is not null) parent.Children.Add(node); else roots.Add(node);
                if (OpensBlock(keyword, parentKeyword)) stack.Push(node);
            }
        }
        if (stack.Count > 0)
        {
            var node = stack.Peek();
            return ([], new("rml.block.unclosed", $"The '{node.Keyword}' block requires 'end'.", Span(node)));
        }
        return (roots.ToImmutable(), null);
    }

    private static Model Build(ImmutableArray<Node> roots)
    {
        var versions = roots.Where(item => item.Keyword == "rml").ToArray();
        var version = versions.FirstOrDefault() ?? throw new RmlException("rml.statement.required", "At least one 'rml' declaration is required.", roots.First());
        if (versions.Any(item => item.Value != "1.0")) throw new RmlException("rml.language.unsupported", $"RML version '{version.Value}' is not supported.", version);
        var context = Single(roots, "context");
        var symbols = new List<Symbol>();
        var byName = new Dictionary<string, Node>(StringComparer.OrdinalIgnoreCase);
        void Register(Node node, string? path = null)
        {
            RequiredId(node);
            if (byName.TryGetValue(node.Value, out var existing))
                throw new RmlException("rml.name.duplicate", $"'{node.Keyword} {node.Value}' has the same name as '{existing.Keyword} {existing.Value}' ({existing.Document}:{existing.Line}). Names must be unique across the document.", node);
            byName.Add(node.Value, node);
            symbols.Add(new(node.Id!, node.Document, node.Line, node.Column, node.TextLength, path));
        }
        Register(context);
        foreach (var node in roots.Where(item => item.Keyword is "entity" or "enumeration" or "fact" or "rule" or "behaviour"))
        {
            Register(node);
            if (node.Keyword == "entity")
            {
                var lifecycle = node.Children.SingleOrDefault(item => item.Keyword == "lifecycle");
                if (lifecycle is not null) { Register(lifecycle); foreach (var stage in lifecycle.Children.Where(item => item.Keyword == "stage")) Register(stage); }
                foreach (var child in node.Children.Where(item => item.Keyword is "field" or "relationship"))
                {
                    RequiredId(child); symbols.Add(new(child.Id!, child.Document, child.Line, child.Column, child.TextLength, null));
                }
            }
            if (node.Keyword == "enumeration") foreach (var member in node.Children.Where(item => item.Keyword == "member"))
            {
                RequiredId(member); symbols.Add(new(member.Id!, member.Document, member.Line, member.Column, member.TextLength, null));
            }
            if (node.Keyword == "rule") Register(Child(node, "conclusion"));
            if (node.Keyword == "behaviour")
                foreach (var child in node.Children.Where(item => item.Keyword is "outcome" or "transition")) Register(child);
        }
        string Id(string name, Node owner)
        {
            if (!byName.TryGetValue(name, out var node)) throw new RmlException("rml.reference.unresolved", $"RML reference '{name}' could not be resolved.", owner);
            return node.Id!;
        }
        var lines = new List<string> { "language 1.0", $"context id={context.Id} name=\"{context.Value}\" slug={Slug(context.Value)} version={Child(context, "version").Value}" };
        foreach (var entity in roots.Where(item => item.Keyword == "entity"))
        {
            var lifecycle = entity.Children.SingleOrDefault(item => item.Keyword == "lifecycle");
            var lifecycleText = lifecycle is null ? "" : $" lifecycle-id={lifecycle.Id} lifecycle-name=\"{lifecycle.Value}\" lifecycle-slug={Slug(lifecycle.Value)}";
            lines.Add($"entity id={entity.Id} name=\"{entity.Value}\" slug={Slug(entity.Value)}{lifecycleText}");
            if (lifecycle is not null) foreach (var stage in lifecycle.Children.Where(item => item.Keyword == "stage"))
                lines.Add($"stage owner={entity.Id} id={stage.Id} name=\"{stage.Value}\" slug={Slug(stage.Value)}");
            foreach (var field in entity.Children.Where(item => item.Keyword == "field"))
            {
                var type = Child(field, "type");
                var namedKind = new[] { "enumeration", "entity", "value" }.FirstOrDefault(kind => type.Value.StartsWith(kind + " ", StringComparison.Ordinal));
                var named = namedKind is not null ? $" named-type={Id(Unquote(type.Value[(namedKind.Length + 1)..]), type)}" : "";
                var primitive = namedKind switch { "enumeration" => "Enumeration", "entity" => "EntityReference", "value" => "ValueTypeReference", _ => CanonicalType(type.Value.Split('(')[0]) };
                var precision = DecimalPrecision(type.Value);
                lines.Add($"field owner={entity.Id} id={field.Id} name=\"{field.Value}\" slug={Slug(field.Value)} type={primitive}{named}{precision}{Flag(field, "optional")}");
            }
            foreach (var relationship in entity.Children.Where(item => item.Keyword == "relationship"))
                lines.Add($"relationship owner={entity.Id} id={relationship.Id} name=\"{relationship.Value}\" slug={Slug(relationship.Value)} target={Id(Child(relationship, "target").Value, relationship)} cardinality={Title(Child(relationship, "cardinality").Value)}{Flag(relationship, "optional")}");
        }
        foreach (var enumeration in roots.Where(item => item.Keyword == "enumeration"))
        {
            lines.Add($"enumeration id={enumeration.Id} name=\"{enumeration.Value}\" slug={Slug(enumeration.Value)}");
            foreach (var member in enumeration.Children.Where(item => item.Keyword == "member"))
                lines.Add($"enumeration-member owner={enumeration.Id} id={member.Id} name=\"{member.Value}\" slug={Slug(member.Value)} value={Child(member, "value").Value}");
        }
        foreach (var fact in roots.Where(item => item.Keyword == "fact"))
            lines.Add($"fact id={fact.Id} name=\"{fact.Value}\" slug={Slug(fact.Value)} type={Title(Child(fact, "type").Value)}{Flag(fact, "export")}");
        foreach (var rule in roots.Where(item => item.Keyword == "rule"))
        {
            var inputs = rule.Children.Where(item => item.Keyword == "input").Select(item => Id(item.Value, item)).ToArray();
            var when = Child(rule, "when");
            if (!when.Value.Equals("all", StringComparison.OrdinalIgnoreCase)) throw new RmlException("rml.expression.unsupported", "RML 1.0 supports only 'when all'.", when);
            var operands = when.Children.Where(item => item.Keyword == "fact").Select(item => Id(item.Value, item)).ToArray();
            var conclusion = Child(rule, "conclusion");
            var findings = rule.Children.Where(item => item.Keyword == "finding").Select(Finding).ToArray();
            string Findings(string disposition) => string.Join(',', findings.Where(item => item.Disposition == disposition).Select(item => $"{Id(item.Fact, item.Node)}:{item.Code}"));
            var optional = new[] { ("true-findings", Findings("true")), ("false-findings", Findings("false")), ("missing-findings", Findings("missing")) }
                .Where(item => item.Item2.Length > 0).Select(item => $" {item.Item1}={item.Item2}");
            lines.Add($"rule id={rule.Id} name=\"{rule.Value}\" slug={Slug(rule.Value)} inputs={string.Join(',', inputs)} expression=and({string.Join(',', operands)}){string.Concat(optional)} conclusion-id={conclusion.Id} conclusion-name=\"{conclusion.Value}\" conclusion-slug={Slug(conclusion.Value)}{Flag(rule, "export")}");
        }
        foreach (var behaviour in roots.Where(item => item.Keyword == "behaviour"))
        {
            lines.Add($"behaviour id={behaviour.Id} name=\"{behaviour.Value}\" slug={Slug(behaviour.Value)} entity={Id(Child(behaviour, "for").Value, behaviour)}");
            foreach (var outcome in behaviour.Children.Where(item => item.Keyword == "outcome"))
                lines.Add($"outcome owner={behaviour.Id} id={outcome.Id} name=\"{outcome.Value}\" slug={Slug(outcome.Value)}");
            foreach (var requires in behaviour.Children.Where(item => item.Keyword == "requires"))
            {
                var rule = byName[requires.Value];
                var facts = rule.Children.Where(item => item.Keyword == "input").Select(item => Id(item.Value, item)).ToArray();
                lines.Add($"binding owner={behaviour.Id} rule={rule.Id} purpose=Requirement facts={string.Join(',', facts.Select(id => $"{id}:{id}"))}");
            }
            foreach (var transition in behaviour.Children.Where(item => item.Keyword == "transition"))
                lines.Add($"transition owner={behaviour.Id} id={transition.Id} name=\"{transition.Value}\" slug={Slug(transition.Value)} lifecycle={Id(Child(transition, "lifecycle").Value, transition)} source={Id(Child(transition, "from").Value, transition)} target={Id(Child(transition, "to").Value, transition)} outcome={Id(Child(transition, "outcome").Value, transition)}");
        }
        return new(string.Join('\n', lines) + "\n", symbols.ToImmutableArray());
    }

    private static (string Fact, string Disposition, string Code, Node Node) Finding(Node node)
    {
        var parts = QuotedTokens().Matches(node.Value).Select(match => match.Groups["quoted"].Success ? match.Groups["quoted"].Value : match.Groups["bare"].Value).ToArray();
        if (parts.Length != 3) throw new RmlException("rml.finding.invalid", "A finding requires a Fact, disposition, and stable code.", node);
        return (parts[0], parts[1].ToLowerInvariant(), parts[2], node);
    }
    private static string Flag(Node node, string keyword) => node.Children.Any(item => item.Keyword == keyword) ? $" {keyword}=true" : string.Empty;
    private static string DecimalPrecision(string value)
    {
        var match = Regex.Match(value, "^decimal\\((?<precision>[0-9]+),(?<scale>[0-9]+)\\)$", RegexOptions.CultureInvariant);
        return match.Success ? $" precision={match.Groups["precision"].Value} scale={match.Groups["scale"].Value}" : "";
    }
    private static string CanonicalType(string value) => value.ToLowerInvariant() switch
    {
        "boolean" or "bool" => "Boolean",
        "text" or "string" => "String",
        "integer" or "int32" => "Int32",
        "byte" => "Byte", "int16" => "Int16", "int64" => "Int64",
        "date" => "Date", "time" => "Time", "datetime" => "DateTime",
        "datetimeoffset" => "DateTimeOffset", "identifier" or "uuid" => "UniqueIdentifier",
        "coordinate" => "GeographicCoordinate", "decimal" => "Decimal",
        _ => Title(value)
    };
    private static Node Single(IEnumerable<Node> nodes, string keyword) => nodes.SingleOrDefault(item => item.Keyword == keyword) ?? throw new RmlException("rml.statement.required", $"One '{keyword}' declaration is required.", nodes.First());
    private static Node Child(Node node, string keyword) => node.Children.SingleOrDefault(item => item.Keyword == keyword) ?? throw new RmlException("rml.statement.required", $"'{node.Keyword}' requires '{keyword}'.", node);
    private static string RequiredId(Node node)
    {
        if (node.Id is null) throw new RmlException("rml.identity.required", $"'{node.Keyword} {node.Value}' requires tooling-managed '# @id=<uuidv7>' metadata.", node);
        if (!Guid.TryParse(node.Id, out var identity) || identity.Version != 7)
            throw new RmlException("rml.identity.invalid", $"'{node.Keyword} {node.Value}' requires UUIDv7 identity metadata.", node);
        return node.Id;
    }
    private static string Slug(string value) => SlugCharacters().Replace(value.Trim().ToLowerInvariant(), "-").Trim('-');
    private static string Title(string value) => value.Length == 0 ? value : char.ToUpperInvariant(value[0]) + value[1..].ToLowerInvariant();
    private static string Unquote(string value) => value.Length >= 2 && value[0] == '"' && value[^1] == '"' ? value[1..^1] : value;
    private static bool IsPackageRelative(string name)
    {
        var normalized = name.Replace('\\', '/');
        return !string.IsNullOrWhiteSpace(name) && !IsRooted(name) &&
            !normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).Contains("..", StringComparer.Ordinal);
    }
    private static bool IsRooted(string value) =>
        Path.IsPathRooted(value) || value.StartsWith('/') || value.StartsWith('\\') ||
        (value.Length >= 2 && value[1] == ':' && char.IsAsciiLetter(value[0]));
    private static SourceSpan Span(Node node) => new(node.Document, node.Line, node.Column, node.TextLength);
    private static bool OpensBlock(string keyword, string? parent) =>
        parent is null && keyword is "context" or "entity" or "enumeration" or "fact" or "rule" or "behaviour" ||
        parent == "entity" && keyword is "lifecycle" or "field" or "relationship" ||
        parent == "enumeration" && keyword == "member" ||
        parent == "rule" && keyword is "when" or "conclusion" ||
        parent == "behaviour" && keyword is "outcome" or "transition";
    private static readonly HashSet<string> IdentityDeclarations = ["context", "entity", "lifecycle", "stage", "field", "relationship", "enumeration", "member", "fact", "rule", "conclusion", "behaviour", "outcome", "transition"];
    [GeneratedRegex("^#\\s*@id=(?<id>[0-9a-fA-F-]{36})\\s*$", RegexOptions.CultureInvariant)] private static partial Regex Identity();
    [GeneratedRegex("[^a-z0-9]+", RegexOptions.CultureInvariant)] private static partial Regex SlugCharacters();
    [GeneratedRegex("\"(?<quoted>[^\"]+)\"|(?<bare>\\S+)", RegexOptions.CultureInvariant)] private static partial Regex QuotedTokens();
    private sealed record Node(string Keyword, string Value, string? Id, string Document, int Line, int Column, int TextLength, List<Node> Children);
    private sealed record Symbol(string Id, string Document, int Line, int Column, int Length, string? SemanticPath);
    private sealed record Model(string Saf, ImmutableArray<Symbol> Symbols);
    private sealed class RmlException(string code, string message, Node node) : Exception(message) { public string Code { get; } = code; public Node Node { get; } = node; }
}

public sealed record RmlSourceEdit(string Original, string Updated, bool Changed);
