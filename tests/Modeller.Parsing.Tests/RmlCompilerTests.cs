using Modeller.Parsing;
using Xunit;

namespace Modeller.Parsing.Tests;

public sealed class RmlCompilerTests
{
    private static readonly SourceDocument[] RmlDocument =
        [new("child-care.rml", "rml 1.0\ncontext Child Care\n  version 1.0.0\nend\n")];
    private static readonly SourceDocument[] NonRmlDocument =
        [new("child-care.modeller", "language 1.0\ncontext id=00000000-0000-7000-8000-000000000001 name=\"Child Care\" slug=child-care version=1.0.0\n")];

    [Fact]
    public void IsRml_returns_true_when_a_line_starts_with_the_rml_directive()
    {
        Assert.True(RmlCompiler.IsRml(RmlDocument));
    }

    [Fact]
    public void IsRml_returns_false_when_no_document_has_an_rml_directive_line()
    {
        Assert.False(RmlCompiler.IsRml(NonRmlDocument));
    }

    [Fact]
    public void Rename_updates_a_matching_declaration_line_and_preserves_indentation_and_kind()
    {
        var edit = RmlCompiler.Rename("  context Child Care\nend\n", "Child Care", "Family Care");

        Assert.True(edit.Changed);
        Assert.Equal("  context Family Care\nend\n", edit.Updated);
    }

    [Fact]
    public void Rename_consumes_the_final_trailing_newline_when_the_declaration_is_the_last_line_of_the_source()
    {
        var edit = RmlCompiler.Rename("context Child Care\n", "Child Care", "Family Care");

        Assert.True(edit.Changed);
        Assert.Equal("context Family Care", edit.Updated);
    }

    [Fact]
    public void Rename_also_updates_quoted_references_to_the_old_name()
    {
        var source = "rule Determine ACCS eligibility\nend\nbehaviour Submit\n  requires \"Determine ACCS eligibility\"\nend\n";

        var edit = RmlCompiler.Rename(source, "Determine ACCS eligibility", "Confirm ACCS eligibility");

        Assert.True(edit.Changed);
        Assert.Contains("rule Confirm ACCS eligibility", edit.Updated);
        Assert.Contains("requires \"Confirm ACCS eligibility\"", edit.Updated);
    }

    [Fact]
    public void Rename_leaves_the_source_untouched_when_the_name_does_not_appear()
    {
        var source = "context Child Care\n  version 1.0.0\nend\n";

        var edit = RmlCompiler.Rename(source, "Nonexistent Concept", "Whatever");

        Assert.False(edit.Changed);
        Assert.Equal(source, edit.Updated);
        Assert.Equal(source, edit.Original);
    }

    [Fact]
    public void Rename_does_not_match_a_name_that_is_only_a_prefix_of_the_declared_name()
    {
        var source = "rule Determine ACCS eligibility\nend\n";

        var edit = RmlCompiler.Rename(source, "Determine ACCS", "Something else");

        Assert.False(edit.Changed);
        Assert.Equal(source, edit.Updated);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rename_throws_when_source_is_blank(string source)
    {
        Assert.Throws<ArgumentException>(() => RmlCompiler.Rename(source, "Old", "New"));
    }

    [Fact]
    public void Rename_throws_when_source_is_null()
    {
        Assert.Throws<ArgumentNullException>(() => RmlCompiler.Rename(null!, "Old", "New"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rename_throws_when_old_name_is_blank(string oldName)
    {
        Assert.Throws<ArgumentException>(() => RmlCompiler.Rename("context Old\nend\n", oldName, "New"));
    }

    [Fact]
    public void Rename_throws_when_old_name_is_null()
    {
        Assert.Throws<ArgumentNullException>(() => RmlCompiler.Rename("context Old\nend\n", null!, "New"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rename_throws_when_new_name_is_blank(string newName)
    {
        Assert.Throws<ArgumentException>(() => RmlCompiler.Rename("context Old\nend\n", "Old", newName));
    }

    [Fact]
    public void Rename_throws_when_new_name_is_null()
    {
        Assert.Throws<ArgumentNullException>(() => RmlCompiler.Rename("context Old\nend\n", "Old", null!));
    }

    [Fact]
    public void EnsureIdentities_inserts_an_identity_comment_before_a_bare_declaration_that_lacks_one()
    {
        var source = "rml 1.0\ncontext Child Care\n  version 1.0.0\nend\n";

        var edit = RmlCompiler.EnsureIdentities(source);

        Assert.True(edit.Changed);
        var lines = edit.Updated.Split('\n');
        var contextLine = Array.FindIndex(lines, line => line == "context Child Care");
        Assert.StartsWith("# @id=", lines[contextLine - 1]);
    }

    [Fact]
    public void EnsureIdentities_does_not_duplicate_an_identity_that_already_precedes_the_declaration()
    {
        var source = "rml 1.0\n# @id=0191f6d4-4ea0-7000-8000-000000000001\ncontext Child Care\n  version 1.0.0\nend\n";

        var edit = RmlCompiler.EnsureIdentities(source);

        Assert.False(edit.Changed);
        Assert.Equal(source, edit.Updated);
    }

    [Fact]
    public void EnsureIdentities_skips_a_declaration_keyword_whose_value_is_itself_a_quoted_reference()
    {
        var source = "rule \"Quoted Value\"\nend\n";

        var edit = RmlCompiler.EnsureIdentities(source);

        Assert.False(edit.Changed);
        Assert.Equal(source, edit.Updated);
    }

    [Fact]
    public void ApplyIdentities_inserts_only_the_registered_identities_for_declarations_missing_one()
    {
        var source = "rml 1.0\ncontext Child Care\n  version 1.0.0\nend\n";
        var identity = "0191f6d4-4ea0-7000-8000-000000000001";

        var edit = RmlCompiler.ApplyIdentities(source, [identity]);

        Assert.True(edit.Changed);
        Assert.Contains($"# @id={identity}", edit.Updated);
    }

    [Fact]
    public void ApplyIdentities_does_not_insert_a_comment_for_a_declaration_that_already_has_one_but_still_consumes_the_identity()
    {
        var source = "rml 1.0\n# @id=0191f6d4-4ea0-7000-8000-000000000001\ncontext Child Care\n  version 1.0.0\nend\n";

        var edit = RmlCompiler.ApplyIdentities(source, ["0191f6d4-4ea0-7000-8000-000000000002"]);

        Assert.False(edit.Changed);
        Assert.Equal(source, edit.Updated);
    }

    [Fact]
    public void ApplyIdentities_succeeds_as_a_no_op_when_there_are_no_declarations_and_no_identities()
    {
        var source = "# just a comment\n";

        var edit = RmlCompiler.ApplyIdentities(source, []);

        Assert.False(edit.Changed);
        Assert.Equal(source, edit.Updated);
    }

    [Fact]
    public void ApplyIdentities_throws_when_the_identity_registry_does_not_cover_every_declaration()
    {
        var source = "rml 1.0\ncontext Child Care\n  version 1.0.0\nend\n";

        var exception = Assert.Throws<ArgumentException>(() => RmlCompiler.ApplyIdentities(source, []));
        Assert.Equal("identities", exception.ParamName);
    }

    [Fact]
    public void ApplyIdentities_throws_when_an_identity_is_not_a_valid_uuid_v7()
    {
        var source = "rml 1.0\ncontext Child Care\n  version 1.0.0\nend\n";

        var exception = Assert.Throws<ArgumentException>(() => RmlCompiler.ApplyIdentities(source, ["not-a-guid"]));
        Assert.Equal("identities", exception.ParamName);
    }

    [Fact]
    public void ApplyIdentities_throws_when_an_identity_is_a_well_formed_guid_but_not_version_7()
    {
        var source = "rml 1.0\ncontext Child Care\n  version 1.0.0\nend\n";
        var guidV4 = Guid.NewGuid().ToString();

        var exception = Assert.Throws<ArgumentException>(() => RmlCompiler.ApplyIdentities(source, [guidV4]));
        Assert.Equal("identities", exception.ParamName);
    }

    [Fact]
    public void ApplyIdentities_throws_when_the_identity_registry_has_unused_identities_left_over()
    {
        var source = "rml 1.0\ncontext Child Care\n  version 1.0.0\nend\n";

        var exception = Assert.Throws<ArgumentException>(() => RmlCompiler.ApplyIdentities(
            source,
            ["0191f6d4-4ea0-7000-8000-000000000001", "0191f6d4-4ea0-7000-8000-000000000002"]));
        Assert.Equal("identities", exception.ParamName);
    }

    [Fact]
    public void ApplyIdentities_throws_for_null_source_or_identities()
    {
        Assert.Throws<ArgumentNullException>(() => RmlCompiler.ApplyIdentities(null!, []));
        Assert.Throws<ArgumentNullException>(() => RmlCompiler.ApplyIdentities("rml 1.0\n", null!));
    }
}
