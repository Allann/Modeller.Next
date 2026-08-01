using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace Modeller.Parsing;

public static partial class LegacyDefinitionAdapter
{
    public static LegacyImportResult ImportDomainRoot(
        SourceDocument source,
        LegacyDomainMapping mapping,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(mapping);
        var lines = source.Content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var domain = lines.Select((text, index) => (text, index))
            .FirstOrDefault(item => Domain().IsMatch(item.text));
        var version = lines.Select((text, index) => (text, index))
            .FirstOrDefault(item => Version().IsMatch(item.text));
        var unsupported = lines.Select((text, index) => (text, index))
            .FirstOrDefault(item => item.text.TrimStart().StartsWith("services", StringComparison.Ordinal));

        if (unsupported.text is not null)
        {
            return Failure(
                "parse.legacy.construct-unsupported",
                "Legacy service lists are ambiguous and require an explicit bounded-context migration.",
                source.Name,
                unsupported.index + 1,
                unsupported.text.Length);
        }

        if (domain.text is null || version.text is null)
        {
            return Failure(
                "parse.legacy.root-invalid",
                "A supported legacy root requires one domain and one version declaration.",
                source.Name,
                1,
                1);
        }

        var legacyIdentifier = Domain().Match(domain.text).Groups["name"].Value;
        var contextVersion = Version().Match(version.text).Groups["version"].Value;
        var readable = new SourceDocument(
            $"imported/{source.Name}.modeller",
            $"""
            language 1.0
            context id={mapping.ContextId} name="{mapping.Name}" slug={mapping.Slug} version={contextVersion}
            """);
        var parsed = DefinitionParser.Parse([readable], ParseOptions.Language1, cancellationToken);
        if (parsed.Package is not null)
        {
            parsed = parsed with
            {
                Provenance =
                [
                    new SourceProvenance(
                        mapping.ContextId,
                        new SourceSpan(source.Name, domain.index + 1, domain.text.IndexOf(legacyIdentifier, StringComparison.Ordinal) + 1, legacyIdentifier.Length))
                ]
            };
        }

        return new LegacyImportResult(
            "legacy-domain-root/1.0",
            parsed,
            [new LegacyIdentifierMapping(legacyIdentifier, mapping.ContextId)]);
    }

    private static LegacyImportResult Failure(string code, string message, string document, int line, int length) =>
        new(
            "legacy-domain-root/1.0",
            new ParseResult(null, [], [new ParseDiagnostic(code, message, new SourceSpan(document, line, 1, length))], false),
            []);

    [GeneratedRegex("^\\s*domain\\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex Domain();

    [GeneratedRegex("^\\s*version\\s+\"(?<version>[^\"]+)\"\\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex Version();
}

public sealed record LegacyDomainMapping(string ContextId, string Name, string Slug);
public sealed record LegacyIdentifierMapping(string LegacyIdentifier, string SemanticId);
public sealed record LegacyImportResult(
    string AdapterVersion,
    ParseResult ParseResult,
    ImmutableArray<LegacyIdentifierMapping> IdentifierMappings);
