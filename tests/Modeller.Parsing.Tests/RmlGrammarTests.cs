using Modeller.Parsing;
using Xunit;

namespace Modeller.Parsing.Tests;

public sealed class RmlGrammarTests
{
    [Theory]
    [InlineData("rml 1.0\n", 2, "entity", true)]
    [InlineData("rml 1.0\nentity Order\n  \nend\n", 3, "relationship", true)]
    [InlineData("rml 1.0\nentity Order\n  rel\nend\n", 3, "relationship", true)]
    [InlineData("rml 1.0\nentity Order\n  rel\nend\n", 3, "context", false)]
    [InlineData("rml 1.0\nrule Decide\n  \nend\n", 3, "finding", true)]
    [InlineData("rml 1.0\nbehaviour Place\n  transition Place\n    \n  end\nend\n", 4, "from", true)]
    public void Complete_returns_only_statements_allowed_in_the_cursor_block(string source, int line, string label, bool expected)
    {
        var column = source.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n')[line - 1].Length + 1;
        var items = RmlGrammar.Complete([new("model.modeller", source)], "model.modeller", line, column, TestContext.Current.CancellationToken);

        Assert.Equal(expected, items.Any(item => item.Label == label));
    }

    [Fact]
    public void Complete_filters_compatible_references_and_supplies_valid_quoted_insertion()
    {
        var documents = new SourceDocument[]
        {
            new("entities.modeller", "entity Order\nentity Orderline\n"),
            new("rules.modeller", "rule Determine readiness\n"),
            new("behaviour.modeller", "behaviour Place order\n  requires \n"),
        };

        var items = RmlGrammar.Complete(documents, "behaviour.modeller", 2, 12, TestContext.Current.CancellationToken);

        var rule = Assert.Single(items);
        Assert.Equal("Determine readiness", rule.Label);
        Assert.Equal("Rule", rule.Kind);
        Assert.Equal("\"Determine readiness\"", rule.InsertText);
        Assert.DoesNotContain(items, item => item.Kind == "Entity");
    }

    [Fact]
    public void Complete_suggests_declared_bounded_contexts_after_import_from()
    {
        var documents = new SourceDocument[]
        {
            new("contexts.modeller", "context Child Care\nend\ncontext Centre Operations\n  import \"Active enrolment exists\"\n    from \n  end\nend\n"),
        };

        var items = RmlGrammar.Complete(documents, "contexts.modeller", 5, 10, TestContext.Current.CancellationToken);

        Assert.Contains(items, item => item.Kind == "Context" && item.Label == "Child Care");
        Assert.All(items, item => Assert.Equal("Context", item.Kind));
    }

    [Fact]
    public void Complete_uses_the_prefix_inside_an_open_quote_across_an_invalid_workspace()
    {
        var documents = new SourceDocument[]
        {
            new("rules.modeller", "rule Determine readiness\nrule Reject order\n"),
            new("behaviour.modeller", "behaviour Place order\n  requires \"Det"),
        };

        var items = RmlGrammar.Complete(documents, "behaviour.modeller", 2, 16, TestContext.Current.CancellationToken);

        var rule = Assert.Single(items);
        Assert.Equal("Determine readiness", rule.Label);
        Assert.Equal("Determine readiness", rule.InsertText);
        Assert.Equal(13, rule.ReplacementStartColumn);
    }

    [Theory]
    [InlineData("behaviour Work\n  for ", 2, "Entity")]
    [InlineData("behaviour Work\n  requires ", 2, "Rule")]
    [InlineData("behaviour Work\n  transition Move\n    lifecycle ", 3, "Lifecycle")]
    [InlineData("behaviour Work\n  transition Move\n    from ", 3, "LifecycleStage")]
    [InlineData("behaviour Work\n  transition Move\n    to ", 3, "LifecycleStage")]
    [InlineData("behaviour Work\n  transition Move\n    outcome ", 3, "Outcome")]
    [InlineData("entity Order\n  relationship Lines\n    target ", 3, "Entity")]
    [InlineData("rule Decide\n  input ", 2, "Fact")]
    [InlineData("rule Decide\n  finding ", 2, "Fact")]
    [InlineData("rule Decide\n  when All\n    fact ", 3, "Fact")]
    [InlineData("entity Widget\n  field Kind\n    type enumeration ", 3, "Enumeration")]
    [InlineData("entity Widget\n  field Kind\n    type entity ", 3, "Entity")]
    public void Complete_returns_only_the_compatible_reference_kind(string activeSource, int line, string expectedKind)
    {
        const string declarations = """
            entity Order
              lifecycle Order lifecycle
                stage Draft
              end
            end
            enumeration Status
              member Open
            end
            fact Payment confirmed
            rule Determine readiness
            behaviour Place
              outcome Placed
            end
            """;
        var column = activeSource.Split('\n')[line - 1].Length + 1;

        var items = RmlGrammar.Complete(
            [new("declarations.modeller", declarations), new("active.modeller", activeSource)],
            "active.modeller", line, column, TestContext.Current.CancellationToken);

        Assert.NotEmpty(items);
        Assert.All(items, item => Assert.Equal(expectedKind, item.Kind));
    }

    [Fact]
    public void Complete_observes_a_cancelled_request()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() => RmlGrammar.Complete(
            [new("model.modeller", "rule Decide\nbehaviour Work\n  requires ")],
            "model.modeller", 3, 12, cancellation.Token));
    }

    [Fact]
    public void ParentAt_skips_blank_and_comment_lines_when_finding_the_enclosing_block()
    {
        const string source = "entity Order\n\n  # a comment about the next field\n  field Total\n";

        Assert.Equal("entity", RmlGrammar.ParentAt(source, 4));
    }

    [Fact]
    public void ParentAt_returns_null_outside_any_block()
    {
        Assert.Null(RmlGrammar.ParentAt("entity Order\nend\n", 3));
    }
}
