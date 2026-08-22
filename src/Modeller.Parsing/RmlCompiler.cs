using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Modeller.Contexts;
using Modeller.Model;

namespace Modeller.Parsing;

public static partial class RmlCompiler
{
    public static bool IsRml(IEnumerable<SourceDocument> documents) => documents.Any(document =>
        document.Content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n')
            .Any(line => line.Trim().StartsWith("rml ", StringComparison.Ordinal)));

    /// <summary>Whether a workspace's RML source needs <see cref="CompileWorkspace"/> rather than
    /// the single-context <see cref="Compile"/> — a cheap textual scan, true when more than one
    /// 'context' is declared, or when an 'import' statement appears at all. The single-context
    /// path never processes 'import' children (there is no second context to import from), so an
    /// import naming an unresolvable context must still route through <see cref="CompileWorkspace"/>
    /// to be validated rather than silently ignored.</summary>
    public static bool RequiresWorkspaceCompilation(IEnumerable<SourceDocument> documents)
    {
        var contextCount = 0;
        foreach (var line in documents.SelectMany(document =>
            document.Content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n')).Select(line => line.Trim()))
        {
            if (line.StartsWith("context ", StringComparison.Ordinal) || line == "context") contextCount++;
            else if (line.StartsWith("import ", StringComparison.Ordinal)) return true;
        }
        return contextCount > 1;
    }

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
            var provenance = ToProvenance(model.Symbols);
            var diagnostics = RemapDiagnostics(result, provenance);
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

    /// <summary>
    /// Compiles a workspace that may declare more than one bounded context. Each declared
    /// declaration (entity, enumeration, fact, rule, behaviour) belongs to the nearest
    /// preceding 'context' declaration in source order. A 'context' may 'import' a fact
    /// exported by an earlier-declared context, producing a <see cref="ContextDependency"/>
    /// that a context-map projection can render as a real cross-context edge.
    /// </summary>
    public static WorkspaceParseResult CompileWorkspace(
        IEnumerable<SourceDocument> documents,
        ParseOptions options,
        CancellationToken cancellationToken = default)
    {
        var sources = documents.OrderBy(document => document.Name, StringComparer.Ordinal).ToArray();
        if (cancellationToken.IsCancellationRequested) return new([], [], [], [], true);
        var parsed = ParseNodes(sources, options, cancellationToken);
        if (cancellationToken.IsCancellationRequested) return new([], [], [], [], true);
        if (parsed.Diagnostic is not null) return new([], [], [], [parsed.Diagnostic], false);
        try
        {
            var built = BuildWorkspace(parsed.Roots);
            var contexts = ImmutableArray.CreateBuilder<LoadedContextPackage>();
            var diagnostics = ImmutableArray.CreateBuilder<ParseDiagnostic>();
            var provenance = ToProvenance(built.Symbols);
            var cancelled = false;
            foreach (var contextModel in built.Contexts)
            {
                if (cancellationToken.IsCancellationRequested) { cancelled = true; break; }
                var saf = new SourceDocument($".modeller/compiled.{contextModel.Slug}.rml.saf", contextModel.Saf);
                var result = DefinitionParser.Parse([saf], options, cancellationToken);
                diagnostics.AddRange(RemapDiagnostics(result, provenance));
                cancelled |= result.IsCancelled;
                if (result.Package is not null) contexts.Add(result.Package);
            }
            return new(contexts.ToImmutable(), built.Dependencies, provenance, diagnostics.ToImmutable(), cancelled);
        }
        catch (RmlException exception)
        {
            return new([], [], [], [new(exception.Code, exception.Message, Span(exception.Node))], false);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return new([], [], [], [new("rml.source.invalid", "RML source is malformed or incomplete.", null)], false);
        }
    }

    /// <summary>Maps each identity-bearing RML symbol to the source span its declaration occupies —
    /// shared by <see cref="Compile"/> and <see cref="CompileWorkspace"/> so a generated SAF
    /// diagnostic can be relocated back to the RML line that produced it.</summary>
    private static ImmutableArray<SourceProvenance> ToProvenance(ImmutableArray<Symbol> symbols) =>
        symbols.Select(symbol => new SourceProvenance(
            symbol.Id,
            new SourceSpan(symbol.Document, symbol.Line, symbol.Column, symbol.Length),
            symbol.SemanticPath)).ToImmutableArray();

    /// <summary>Relocates each diagnostic raised against the generated SAF back to the RML source
    /// span that produced the referenced semantic identity, via <paramref name="provenance"/>.</summary>
    private static ImmutableArray<ParseDiagnostic> RemapDiagnostics(ParseResult result, ImmutableArray<SourceProvenance> provenance) =>
        result.Diagnostics.Select(diagnostic =>
        {
            var generated = result.Provenance.LastOrDefault(item => item.Span.Line == diagnostic.Location?.Line);
            var source = generated is null ? null : provenance.LastOrDefault(item =>
                item.SemanticId == generated.SemanticId && item.SemanticPath == generated.SemanticPath)
                ?? provenance.FirstOrDefault(item => item.SemanticId == generated.SemanticId);
            return diagnostic with { Location = source?.Span };
        }).ToImmutableArray();

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
        RequireLanguageVersion1(roots);
        var context = Single(roots, "context");
        var symbols = new List<Symbol>();
        var byName = new Dictionary<string, Node>(StringComparer.OrdinalIgnoreCase);
        void Register(Node node) => RegisterSymbol(node, byName, symbols);
        void AddSymbol(Node node) => AppendSymbol(node, symbols);
        Register(context);
        foreach (var node in roots.Where(item => item.Keyword is "entity" or "enumeration" or "fact" or "rule" or "behaviour"))
            RegisterDeclaration(node, Register, AddSymbol);
        string Id(string name, Node owner) => ResolveId(name, owner, byName);

        var lines = new List<string> { "language 1.0", ContextHeader(context) };
        lines.AddRange(EmitDeclarations(
            roots.Where(item => item.Keyword == "entity"),
            roots.Where(item => item.Keyword == "enumeration"),
            roots.Where(item => item.Keyword == "fact"),
            roots.Where(item => item.Keyword == "rule"),
            roots.Where(item => item.Keyword == "behaviour"),
            Id, byName));
        return new(string.Join('\n', lines) + "\n", symbols.ToImmutableArray());
    }

    private static WorkspaceModel BuildWorkspace(ImmutableArray<Node> roots)
    {
        RequireLanguageVersion1(roots);
        var contextNodes = roots.Where(item => item.Keyword == "context").ToArray();
        if (contextNodes.Length == 0) throw new RmlException("rml.statement.required", "At least one 'context' declaration is required.", roots.First());

        var symbols = new List<Symbol>();
        var byName = new Dictionary<string, Node>(StringComparer.OrdinalIgnoreCase);
        void Register(Node node) => RegisterSymbol(node, byName, symbols);
        void AddSymbol(Node node) => AppendSymbol(node, symbols);
        foreach (var context in contextNodes) Register(context);

        var ownerOf = ComputeContextOwnership(roots);
        foreach (var node in roots.Where(item => item.Keyword is "entity" or "enumeration" or "fact" or "rule" or "behaviour"))
            RegisterDeclaration(node, Register, AddSymbol);
        string Id(string name, Node owner) => ResolveId(name, owner, byName);

        var dependencies = ResolveImports(contextNodes, byName, ownerOf);

        var contexts = ImmutableArray.CreateBuilder<ContextModel>();
        foreach (var context in contextNodes)
        {
            bool OwnedBy(Node node) => ownerOf.GetValueOrDefault(node) == context;
            var lines = new List<string> { "language 1.0", ContextHeader(context) };
            lines.AddRange(EmitDeclarations(
                roots.Where(item => item.Keyword == "entity" && OwnedBy(item)),
                roots.Where(item => item.Keyword == "enumeration" && OwnedBy(item)),
                roots.Where(item => item.Keyword == "fact" && OwnedBy(item)),
                roots.Where(item => item.Keyword == "rule" && OwnedBy(item)),
                roots.Where(item => item.Keyword == "behaviour" && OwnedBy(item)),
                Id, byName));
            contexts.Add(new(context.Value, Slug(context.Value), string.Join('\n', lines) + "\n"));
        }

        return new(contexts.ToImmutable(), dependencies, symbols.ToImmutableArray());
    }

    private static void RequireLanguageVersion1(ImmutableArray<Node> roots)
    {
        var versions = roots.Where(item => item.Keyword == "rml").ToArray();
        var version = versions.FirstOrDefault() ?? throw new RmlException("rml.statement.required", "At least one 'rml' declaration is required.", roots.First());
        if (versions.Any(item => item.Value != "1.0")) throw new RmlException("rml.language.unsupported", $"RML version '{version.Value}' is not supported.", version);
    }

    private static string ContextHeader(Node context) =>
        $"context id={context.Id} name=\"{context.Value}\" slug={Slug(context.Value)} version={Child(context, "version").Value}";

    private static void RegisterSymbol(Node node, Dictionary<string, Node> byName, List<Symbol> symbols)
    {
        RequiredId(node);
        if (byName.TryGetValue(node.Value, out var existing))
            throw new RmlException("rml.name.duplicate", $"'{node.Keyword} {node.Value}' has the same name as '{existing.Keyword} {existing.Value}' ({existing.Document}:{existing.Line}). Names must be unique across the document.", node);
        byName.Add(node.Value, node);
        symbols.Add(new(node.Id!, node.Document, node.Line, node.Column, node.TextLength, null));
    }

    private static void AppendSymbol(Node node, List<Symbol> symbols)
    {
        RequiredId(node);
        symbols.Add(new(node.Id!, node.Document, node.Line, node.Column, node.TextLength, null));
    }

    private static string ResolveId(string name, Node owner, Dictionary<string, Node> byName) =>
        byName.TryGetValue(name, out var node) ? node.Id! : throw new RmlException("rml.reference.unresolved", $"RML reference '{name}' could not be resolved.", owner);

    /// <summary>Registers a declaration and its identity-bearing children (lifecycle/stage,
    /// field/relationship, enumeration member, rule conclusion, behaviour outcome/transition) —
    /// shared by both the single-context and multi-context context builders.</summary>
    private static void RegisterDeclaration(Node node, Action<Node> register, Action<Node> addSymbol)
    {
        register(node);
        if (node.Keyword == "entity")
        {
            var lifecycle = node.Children.SingleOrDefault(item => item.Keyword == "lifecycle");
            if (lifecycle is not null)
            {
                register(lifecycle);
                foreach (var stage in lifecycle.Children.Where(item => item.Keyword == "stage")) register(stage);
            }
            foreach (var child in node.Children.Where(item => item.Keyword is "field" or "relationship")) addSymbol(child);
        }
        if (node.Keyword == "enumeration")
            foreach (var member in node.Children.Where(item => item.Keyword == "member")) addSymbol(member);
        if (node.Keyword == "rule") register(Child(node, "conclusion"));
        if (node.Keyword == "behaviour")
            foreach (var child in node.Children.Where(item => item.Keyword is "outcome" or "transition" or "event" or "effect")) register(child);
    }

    /// <summary>Assigns each entity/enumeration/fact/rule/behaviour to the nearest preceding
    /// 'context' declaration in source order.</summary>
    /// <summary>Assigns each entity/enumeration/fact/rule/behaviour to the nearest preceding
    /// 'context' declared in the SAME document — matching how a workspace already mixes multiple
    /// contexts in one file — or, for a document that declares no 'context' of its own (the
    /// conventional case: separate documents grouping entities/, facts/, behaviours/ ... under one
    /// shared context), to the first 'context' declared anywhere in the workspace. Ownership is
    /// never inferred from a *different* document's most recent context: a workspace's document
    /// names commonly don't sort with its context-declaring document first, and attributing an
    /// unrelated document's declarations to whichever context happens to sort last would silently
    /// misassign ownership rather than surface a clear diagnostic.</summary>
    private static Dictionary<Node, Node> ComputeContextOwnership(ImmutableArray<Node> roots)
    {
        var ownerOf = new Dictionary<Node, Node>(ReferenceEqualityComparer.Instance);
        var firstContext = roots.First(node => node.Keyword == "context");
        Node? currentDocumentContext = null;
        string? currentDocument = null;
        foreach (var node in roots)
        {
            if (node.Document != currentDocument) { currentDocument = node.Document; currentDocumentContext = null; }
            if (node.Keyword == "context") { currentDocumentContext = node; continue; }
            if (node.Keyword is not ("entity" or "enumeration" or "fact" or "rule" or "behaviour")) continue;
            ownerOf[node] = currentDocumentContext ?? firstContext;
        }
        return ownerOf;
    }

    private static ImmutableArray<ContextDependency> ResolveImports(
        IEnumerable<Node> contextNodes, Dictionary<string, Node> byName, Dictionary<Node, Node> ownerOf)
    {
        var dependencies = ImmutableArray.CreateBuilder<ContextDependency>();
        foreach (var context in contextNodes)
        {
            foreach (var import in context.Children.Where(item => item.Keyword == "import"))
            {
                var from = Child(import, "from");
                if (!byName.TryGetValue(from.Value, out var sourceContext) || sourceContext.Keyword != "context")
                    throw new RmlException("rml.import.context-unresolved", $"Bounded context '{from.Value}' could not be resolved.", from);
                if (!byName.TryGetValue(import.Value, out var factNode) || factNode.Keyword != "fact")
                    throw new RmlException("rml.import.fact-unresolved", $"Imported fact '{import.Value}' could not be resolved.", import);
                if (!ownerOf.TryGetValue(factNode, out var factOwner) || factOwner != sourceContext)
                    throw new RmlException("rml.import.context-unresolved", $"Fact '{import.Value}' is not declared by bounded context '{from.Value}'.", import);
                if (!factNode.Children.Any(item => item.Keyword == "export"))
                    throw new RmlException("rml.import.not-exported", $"Fact '{import.Value}' is not exported by bounded context '{from.Value}'.", import);
                dependencies.Add(new(
                    SemanticId.Parse(context.Id!), new(context.Value),
                    SemanticId.Parse(sourceContext.Id!), new(sourceContext.Value),
                    SemanticId.Parse(factNode.Id!), new(factNode.Value)));
            }
        }
        return dependencies.ToImmutable();
    }

    /// <summary>Emits SAF lines for one context's declarations — shared by the single-context and
    /// multi-context builders, which differ only in which declarations they pass in.</summary>
    private static List<string> EmitDeclarations(
        IEnumerable<Node> entities, IEnumerable<Node> enumerations, IEnumerable<Node> facts,
        IEnumerable<Node> rules, IEnumerable<Node> behaviours,
        Func<string, Node, string> id, Dictionary<string, Node> byName)
    {
        var lines = new List<string>();
        EmitEntities(lines, entities, id);
        EmitEnumerations(lines, enumerations);
        EmitFacts(lines, facts);
        EmitRules(lines, rules, id);
        EmitBehaviours(lines, behaviours, id, byName);
        return lines;
    }

    private static void EmitEntities(List<string> lines, IEnumerable<Node> entities, Func<string, Node, string> id)
    {
        foreach (var entity in entities)
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
                var named = namedKind is not null ? $" named-type={id(Unquote(type.Value[(namedKind.Length + 1)..]), type)}" : "";
                var primitive = namedKind switch { "enumeration" => "Enumeration", "entity" => "EntityReference", "value" => "ValueTypeReference", _ => CanonicalType(type.Value.Split('(')[0]) };
                var precision = DecimalPrecision(type.Value);
                lines.Add($"field owner={entity.Id} id={field.Id} name=\"{field.Value}\" slug={Slug(field.Value)} type={primitive}{named}{precision}{Flag(field, "optional")}");
            }
            foreach (var relationship in entity.Children.Where(item => item.Keyword == "relationship"))
                lines.Add($"relationship owner={entity.Id} id={relationship.Id} name=\"{relationship.Value}\" slug={Slug(relationship.Value)} target={id(Child(relationship, "target").Value, relationship)} cardinality={Title(Child(relationship, "cardinality").Value)}{Flag(relationship, "optional")}");
        }
    }

    private static void EmitEnumerations(List<string> lines, IEnumerable<Node> enumerations)
    {
        foreach (var enumeration in enumerations)
        {
            lines.Add($"enumeration id={enumeration.Id} name=\"{enumeration.Value}\" slug={Slug(enumeration.Value)}");
            foreach (var member in enumeration.Children.Where(item => item.Keyword == "member"))
                lines.Add($"enumeration-member owner={enumeration.Id} id={member.Id} name=\"{member.Value}\" slug={Slug(member.Value)} value={Child(member, "value").Value}");
        }
    }

    private static void EmitFacts(List<string> lines, IEnumerable<Node> facts)
    {
        foreach (var fact in facts)
            lines.Add($"fact id={fact.Id} name=\"{fact.Value}\" slug={Slug(fact.Value)} type={Title(Child(fact, "type").Value)}{Flag(fact, "export")}");
    }

    private static void EmitRules(List<string> lines, IEnumerable<Node> rules, Func<string, Node, string> id)
    {
        foreach (var rule in rules)
        {
            var inputs = rule.Children.Where(item => item.Keyword == "input").Select(item => id(item.Value, item)).ToArray();
            var when = Child(rule, "when");
            if (!when.Value.Equals("all", StringComparison.OrdinalIgnoreCase)) throw new RmlException("rml.expression.unsupported", "RML 1.0 supports only 'when all'.", when);
            var operands = when.Children.Where(item => item.Keyword == "fact").Select(item => id(item.Value, item)).ToArray();
            var conclusion = Child(rule, "conclusion");
            var findings = rule.Children.Where(item => item.Keyword == "finding").Select(Finding).ToArray();
            string Findings(string disposition) => string.Join(',', findings.Where(item => item.Disposition == disposition).Select(item => $"{id(item.Fact, item.Node)}:{item.Code}"));
            var optional = new[] { ("true-findings", Findings("true")), ("false-findings", Findings("false")), ("missing-findings", Findings("missing")) }
                .Where(item => item.Item2.Length > 0).Select(item => $" {item.Item1}={item.Item2}");
            lines.Add($"rule id={rule.Id} name=\"{rule.Value}\" slug={Slug(rule.Value)} inputs={string.Join(',', inputs)} expression=and({string.Join(',', operands)}){string.Concat(optional)} conclusion-id={conclusion.Id} conclusion-name=\"{conclusion.Value}\" conclusion-slug={Slug(conclusion.Value)}{Flag(rule, "export")}");
        }
    }

    private static void EmitBehaviours(List<string> lines, IEnumerable<Node> behaviours, Func<string, Node, string> id, Dictionary<string, Node> byName)
    {
        foreach (var behaviour in behaviours)
        {
            lines.Add($"behaviour id={behaviour.Id} name=\"{behaviour.Value}\" slug={Slug(behaviour.Value)} entity={id(Child(behaviour, "for").Value, behaviour)}");
            EmitOwnedChildren(lines, behaviour, "outcome");
            EmitOwnedChildren(lines, behaviour, "event");
            EmitOwnedChildren(lines, behaviour, "effect");
            foreach (var requires in behaviour.Children.Where(item => item.Keyword == "requires"))
            {
                var rule = byName[requires.Value];
                var facts = rule.Children.Where(item => item.Keyword == "input").Select(item => id(item.Value, item)).ToArray();
                lines.Add($"binding owner={behaviour.Id} rule={rule.Id} purpose=Requirement facts={string.Join(',', facts.Select(value => $"{value}:{value}"))}");
            }
            foreach (var transition in behaviour.Children.Where(item => item.Keyword == "transition"))
                lines.Add($"transition owner={behaviour.Id} id={transition.Id} name=\"{transition.Value}\" slug={Slug(transition.Value)} lifecycle={id(Child(transition, "lifecycle").Value, transition)} source={id(Child(transition, "from").Value, transition)} target={id(Child(transition, "to").Value, transition)} outcome={id(Child(transition, "outcome").Value, transition)}");
        }
    }

    /// <summary>Emits one SAF line per <paramref name="owner"/> child matching <paramref name="keyword"/>
    /// (outcome/event/effect all share this identity-only shape: id, name, slug, owner).</summary>
    private static void EmitOwnedChildren(List<string> lines, Node owner, string keyword)
    {
        foreach (var child in owner.Children.Where(item => item.Keyword == keyword))
            lines.Add($"{keyword} owner={owner.Id} id={child.Id} name=\"{child.Value}\" slug={Slug(child.Value)}");
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
        BlockOpeningKeywords.TryGetValue(parent ?? "", out var allowed) && allowed.Contains(keyword);

    private static readonly Dictionary<string, string[]> BlockOpeningKeywords = new(StringComparer.Ordinal)
    {
        [""] = ["context", "entity", "enumeration", "fact", "rule", "behaviour"],
        ["context"] = ["import"],
        ["entity"] = ["lifecycle", "field", "relationship"],
        ["enumeration"] = ["member"],
        ["rule"] = ["when", "conclusion"],
        ["behaviour"] = ["outcome", "transition", "event", "effect"],
    };
    private static readonly HashSet<string> IdentityDeclarations = ["context", "entity", "lifecycle", "stage", "field", "relationship", "enumeration", "member", "fact", "rule", "conclusion", "behaviour", "outcome", "transition", "event", "effect"];
    [GeneratedRegex("^#\\s*@id=(?<id>[0-9a-fA-F-]{36})\\s*$", RegexOptions.CultureInvariant)] private static partial Regex Identity();
    [GeneratedRegex("[^a-z0-9]+", RegexOptions.CultureInvariant)] private static partial Regex SlugCharacters();
    [GeneratedRegex("\"(?<quoted>[^\"]+)\"|(?<bare>\\S+)", RegexOptions.CultureInvariant)] private static partial Regex QuotedTokens();
    private sealed record Node(string Keyword, string Value, string? Id, string Document, int Line, int Column, int TextLength, List<Node> Children);
    private sealed record Symbol(string Id, string Document, int Line, int Column, int Length, string? SemanticPath);
    private sealed record Model(string Saf, ImmutableArray<Symbol> Symbols);
    private sealed record ContextModel(string Name, string Slug, string Saf);
    private sealed record WorkspaceModel(ImmutableArray<ContextModel> Contexts, ImmutableArray<ContextDependency> Dependencies, ImmutableArray<Symbol> Symbols);
    private sealed class RmlException(string code, string message, Node node) : Exception(message) { public string Code { get; } = code; public Node Node { get; } = node; }
}

public sealed record RmlSourceEdit(string Original, string Updated, bool Changed);

public sealed record WorkspaceParseResult(
    ImmutableArray<LoadedContextPackage> Contexts,
    ImmutableArray<ContextDependency> Dependencies,
    ImmutableArray<SourceProvenance> Provenance,
    ImmutableArray<ParseDiagnostic> Diagnostics,
    bool IsCancelled)
{
    public bool IsSuccess => !IsCancelled && Diagnostics.IsEmpty && !Contexts.IsDefaultOrEmpty;
}
