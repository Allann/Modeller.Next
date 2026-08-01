using Modeller.Parsing;
using Modeller.Conformance;
using System.Text.Json;
using Xunit;

namespace Modeller.Parsing.Tests;

public sealed class DefinitionParserTests
{
    [Fact]
    public void Readable_child_care_source_has_the_canonical_json_semantic_digest()
    {
        var source = new SourceDocument(
            "child-care-accs.modeller",
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "child-care-accs.modeller")));

        var result = DefinitionParser.Parse([source], ParseOptions.Language1, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(
            "sha256:26b35b94c741cae8ffb8aafac1ad7cefb7bb5bf106cddc5db27544f5ccdfcd16",
            result.Package!.SemanticDigest);
        Assert.Equal(5, result.Package.AuthoredRevision.Definitions.Length);
    }

    [Fact]
    public void Unknown_fact_reference_reports_the_exact_source_token()
    {
        var content = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "child-care-accs.modeller"));
        const string unknown = "0191f6d4-4ea0-7000-8000-00000000ffff";
        content = content.Replace(
            "inputs=0191f6d4-4ea0-7000-8000-000000000006,",
            $"inputs={unknown},",
            StringComparison.Ordinal);
        var source = new SourceDocument("child-care-accs.modeller", content);

        var result = DefinitionParser.Parse([source], ParseOptions.Language1, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Package);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("validation.reference.fact-unresolved", diagnostic.Code);
        Assert.Equal(9, diagnostic.Location!.Line);
        var ruleLine = content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n')[8];
        Assert.Equal(ruleLine.IndexOf(unknown, StringComparison.Ordinal) + 1, diagnostic.Location.Column);
        Assert.Equal(unknown.Length, diagnostic.Location.Length);
    }

    [Fact]
    public void Comments_formatting_and_source_order_do_not_change_meaning()
    {
        var content = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "child-care-accs.modeller"));
        var statements = content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n')
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Reverse();
        var reordered = $"# moved and reformatted\n\n{string.Join("\n\n", statements)}\n";

        var result = DefinitionParser.Parse(
            [new SourceDocument("renamed/source.modeller", reordered)],
            ParseOptions.Language1,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            "sha256:26b35b94c741cae8ffb8aafac1ad7cefb7bb5bf106cddc5db27544f5ccdfcd16",
            result.Package!.SemanticDigest);
    }

    [Fact]
    public void Incomplete_transition_reports_its_source_line()
    {
        var content = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "child-care-accs.modeller"))
            .Replace(" outcome=0191f6d4-4ea0-7000-8000-00000000000b", string.Empty, StringComparison.Ordinal);

        var result = DefinitionParser.Parse(
            [new SourceDocument("child-care-accs.modeller", content)],
            ParseOptions.Language1,
            TestContext.Current.CancellationToken);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("parse.attribute.required", diagnostic.Code);
        Assert.Equal(14, diagnostic.Location!.Line);
    }

    [Fact]
    public async Task Readable_source_passes_executable_conformance_evidence()
    {
        var fixture = ConformanceFixture.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "readable-source.v1.json")));
        var report = await ConformanceRunner.RunAsync(
            fixture,
            new ParsingConformanceAdapter(Path.Combine(AppContext.BaseDirectory, "Fixtures")),
            TestContext.Current.CancellationToken);

        Assert.Equal(ConformanceStatus.Passed, report.Status);
    }

    [Fact]
    public void Legacy_domain_root_requires_explicit_successor_identity_mapping()
    {
        var legacy = new SourceDocument(
            "child-care.def",
            """
            domain ChildCare
              version "1.0.0"
            end
            """);
        var mapping = new LegacyDomainMapping(
            "0191f6d4-4ea0-7000-8000-000000000001",
            "Child Care",
            "child-care");

        var result = LegacyDefinitionAdapter.ImportDomainRoot(
            legacy,
            mapping,
            TestContext.Current.CancellationToken);

        Assert.True(result.ParseResult.IsSuccess);
        Assert.Equal("legacy-domain-root/1.0", result.AdapterVersion);
        Assert.Equal(mapping.ContextId, result.ParseResult.Package!.AuthoredRevision.Id.ToString());
        Assert.Equal("1.0.0", result.ParseResult.Package.AuthoredRevision.ContextVersion);
        Assert.Equal("ChildCare", Assert.Single(result.IdentifierMappings).LegacyIdentifier);
    }

    [Fact]
    public void Attribute_token_limit_is_enforced_independently_of_statement_limit()
    {
        var result = DefinitionParser.Parse(
            [new SourceDocument("source.modeller", "language 1.0\ncontext id=one name=two slug=three version=four")],
            new ParseOptions("1.0", 1_000, 10, 3),
            TestContext.Current.CancellationToken);

        Assert.Equal("parse.limit.tokens", Assert.Single(result.Diagnostics).Code);
    }

    [Theory]
    [InlineData("../private.modeller")]
    [InlineData("C:\\private.modeller")]
    public void Source_document_names_must_remain_package_relative(string name)
    {
        var result = DefinitionParser.Parse(
            [new SourceDocument(name, "language 1.0")],
            ParseOptions.Language1,
            TestContext.Current.CancellationToken);

        Assert.Equal("parse.path.invalid", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Pre_cancelled_parse_returns_a_cancelled_result()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var result = DefinitionParser.Parse(
            [new SourceDocument("source.modeller", "language 1.0")],
            ParseOptions.Language1,
            cancellation.Token);

        Assert.True(result.IsCancelled);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Legacy_service_lists_are_rejected_instead_of_becoming_bounded_contexts()
    {
        var legacy = new SourceDocument(
            "child-care.def",
            """
            domain ChildCare
              version "1.0.0"
              services [Eligibility]
            end
            """);

        var result = LegacyDefinitionAdapter.ImportDomainRoot(
            legacy,
            new LegacyDomainMapping("0191f6d4-4ea0-7000-8000-000000000001", "Child Care", "child-care"),
            TestContext.Current.CancellationToken);

        Assert.Equal("parse.legacy.construct-unsupported", Assert.Single(result.ParseResult.Diagnostics).Code);
        Assert.Null(result.ParseResult.Package);
    }

    private sealed class ParsingConformanceAdapter(string fixtureDirectory) : IConformanceAdapter
    {
        public string Capability => "readable-source-parsing";
        public string ContractVersion => "1.0";

        public ValueTask<JsonElement> ExecuteAsync(JsonElement input, ConformanceExecutionContext context, CancellationToken cancellationToken)
        {
            var name = input.GetProperty("artifact").GetString()!;
            var result = DefinitionParser.Parse(
                [new SourceDocument(name, File.ReadAllText(Path.Combine(fixtureDirectory, name)))],
                ParseOptions.Language1,
                cancellationToken);
            return ValueTask.FromResult(JsonSerializer.SerializeToElement(new
            {
                semanticDigest = result.Package!.SemanticDigest,
                context = result.Package.AuthoredRevision.Slug.Value,
                definitionCount = result.Package.AuthoredRevision.Definitions.Length,
                diagnosticCount = result.Diagnostics.Length
            }));
        }
    }
}
