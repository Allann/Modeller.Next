using Modeller.Parsing;
using Modeller.Conformance;
using System.Text.Json;
using Xunit;

namespace Modeller.Parsing.Tests;

public sealed class DefinitionParserTests
{
    [Fact]
    public void Rml_child_care_source_has_the_canonical_json_semantic_digest()
    {
        var source = new SourceDocument(
            "child-care-accs.modeller",
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "child-care-accs.modeller")));

        var result = DefinitionParser.Parse([source], ParseOptions.Language1, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(
            "sha256:6d1448c32127c49571d2a7ea5982045b7aa74ed28da2d7323ac5360b94ed6728",
            result.Package!.SemanticDigest);
        Assert.Equal(5, result.Package.AuthoredRevision.Definitions.Length);
    }

    [Fact]
    public void Unknown_fact_reference_reports_the_exact_source_token()
    {
        var content = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "child-care-accs.modeller"));
        const string unknown = "Unknown eligibility fact";
        content = content.Replace(
            "input \"Active enrolment exists\"",
            $"input \"{unknown}\"",
            StringComparison.Ordinal);
        var source = new SourceDocument("child-care-accs.modeller", content);

        var result = DefinitionParser.Parse([source], ParseOptions.Language1, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Package);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("rml.reference.unresolved", diagnostic.Code);
        Assert.Equal(35, diagnostic.Location!.Line);
    }

    [Fact]
    public void Comments_and_indentation_do_not_change_rml_meaning()
    {
        var content = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "child-care-accs.modeller"));
        var reordered = $"# added review comment\n\n{content.Replace("  version 1.0.0", "      version 1.0.0", StringComparison.Ordinal)}\n";

        var result = DefinitionParser.Parse(
            [new SourceDocument("renamed/source.modeller", reordered)],
            ParseOptions.Language1,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            "sha256:6d1448c32127c49571d2a7ea5982045b7aa74ed28da2d7323ac5360b94ed6728",
            result.Package!.SemanticDigest);
    }

    [Fact]
    public void Incomplete_transition_reports_its_source_line()
    {
        var content = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "child-care-accs.modeller"))
            .Replace("    outcome \"Application submitted\"\n", string.Empty, StringComparison.Ordinal);

        var result = DefinitionParser.Parse(
            [new SourceDocument("child-care-accs.modeller", content)],
            ParseOptions.Language1,
            TestContext.Current.CancellationToken);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("rml.statement.required", diagnostic.Code);
        Assert.Equal(65, diagnostic.Location!.Line);
    }

    [Fact]
    public async Task Rml_source_passes_executable_conformance_evidence()
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
    public void Readable_child_care_decision_table_matches_canonical_json_meaning()
    {
        var source = new SourceDocument(
            "child-care-accs-decision-table.modeller",
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "child-care-accs-decision-table.modeller")));

        var result = DefinitionParser.Parse([source], ParseOptions.Language1, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("sha256:63e39411df24b9ee59614cc8d3fae4397e79272f85ac99b8fc2d97eda4e7a37a", result.Package!.SemanticDigest);
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

    [Fact]
    public void Identity_source_edit_adds_uuidv7_metadata_once()
    {
        const string source = "rml 1.0\ncontext Child Care\n  version 1.0.0\nend\n";

        var first = RmlCompiler.EnsureIdentities(source);
        var second = RmlCompiler.EnsureIdentities(first.Updated);

        Assert.True(first.Changed);
        Assert.Contains("# @id=", first.Updated);
        Assert.False(second.Changed);
        var identity = first.Updated.Split('\n').Single(line => line.StartsWith("# @id=", StringComparison.Ordinal))[6..];
        Assert.Equal(7, Guid.Parse(identity).Version);
    }

    [Fact]
    public void Rml_rejects_non_v7_identity_metadata()
    {
        const string source = "rml 1.0\n# @id=00000000-0000-4000-8000-000000000001\ncontext Child Care\n  version 1.0.0\nend\n";

        var result = DefinitionParser.Parse([new("child-care.modeller", source)], ParseOptions.Language1, TestContext.Current.CancellationToken);

        Assert.Equal("rml.identity.invalid", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Rml_path_and_token_limits_are_enforced_before_compilation()
    {
        var unsafePath = DefinitionParser.Parse([new("../child-care.modeller", "rml 1.0")], ParseOptions.Language1, TestContext.Current.CancellationToken);
        var tooManyTokens = DefinitionParser.Parse([new("child-care.modeller", "rml 1.0\ncontext Child Care")], new("1.0", 100, 10, 2), TestContext.Current.CancellationToken);

        Assert.Equal("parse.path.invalid", Assert.Single(unsafePath.Diagnostics).Code);
        Assert.Equal("parse.limit.tokens", Assert.Single(tooManyTokens.Diagnostics).Code);
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
