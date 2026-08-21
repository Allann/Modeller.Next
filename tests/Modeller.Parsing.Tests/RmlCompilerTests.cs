using Modeller.Model;
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
    public void Compile_rejects_an_unknown_statement_inside_a_context()
    {
        const string source = "rml 1.0\ncontext Ordering\n  version 1.0.0\n  asdf\nend\n";

        var result = DefinitionParser.Parse(
            [new SourceDocument("model/context.modeller", source)],
            ParseOptions.EditorLanguage1,
            TestContext.Current.CancellationToken);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("rml.statement.unexpected", diagnostic.Code);
        Assert.Equal(4, diagnostic.Location!.Line);
    }

    [Fact]
    public void Compile_reports_a_precise_diagnostic_when_two_declarations_share_a_name()
    {
        const string source = """
            rml 1.0
            context Child Care
              version 1.0.0
            end
            entity Booking
            end
            behaviour Record absence
              for "Booking"
              outcome Absence recorded as non chargeable
              end
              outcome Absence recorded as chargeable
              end
              transition Record absence
                lifecycle "Booking lifecycle"
                from "Planned"
                to "Absent"
                outcome "Absence recorded as non chargeable"
              end
            end
            """;

        var result = DefinitionParser.Parse(
            [new SourceDocument("model/behaviours/record-absence.modeller", source)],
            ParseOptions.EditorLanguage1,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Package);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("rml.name.duplicate", diagnostic.Code);
        Assert.Contains("Record absence", diagnostic.Message, StringComparison.Ordinal);
        Assert.Equal(13, diagnostic.Location!.Line);
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

        var edit = RmlCompiler.EnsureIdentities(source, TestContext.Current.CancellationToken);

        Assert.True(edit.Changed);
        var lines = edit.Updated.Split('\n');
        var contextLine = Array.FindIndex(lines, line => line == "context Child Care");
        Assert.StartsWith("# @id=", lines[contextLine - 1]);
    }

    [Fact]
    public void EnsureIdentities_does_not_duplicate_an_identity_that_already_precedes_the_declaration()
    {
        var source = "rml 1.0\n# @id=0191f6d4-4ea0-7000-8000-000000000001\ncontext Child Care\n  version 1.0.0\nend\n";

        var edit = RmlCompiler.EnsureIdentities(source, TestContext.Current.CancellationToken);

        Assert.False(edit.Changed);
        Assert.Equal(source, edit.Updated);
    }

    [Fact]
    public void EnsureIdentities_skips_a_declaration_keyword_whose_value_is_itself_a_quoted_reference()
    {
        var source = "rule \"Quoted Value\"\nend\n";

        var edit = RmlCompiler.EnsureIdentities(source, TestContext.Current.CancellationToken);

        Assert.False(edit.Changed);
        Assert.Equal(source, edit.Updated);
    }

    [Fact]
    public void ApplyIdentities_inserts_only_the_registered_identities_for_declarations_missing_one()
    {
        var source = "rml 1.0\ncontext Child Care\n  version 1.0.0\nend\n";
        var identity = "0191f6d4-4ea0-7000-8000-000000000001";

        var edit = RmlCompiler.ApplyIdentities(source, [identity], TestContext.Current.CancellationToken);

        Assert.True(edit.Changed);
        Assert.Contains($"# @id={identity}", edit.Updated);
    }

    [Fact]
    public void ApplyIdentities_does_not_insert_a_comment_for_a_declaration_that_already_has_one_but_still_consumes_the_identity()
    {
        var source = "rml 1.0\n# @id=0191f6d4-4ea0-7000-8000-000000000001\ncontext Child Care\n  version 1.0.0\nend\n";

        var edit = RmlCompiler.ApplyIdentities(source, ["0191f6d4-4ea0-7000-8000-000000000002"], TestContext.Current.CancellationToken);

        Assert.False(edit.Changed);
        Assert.Equal(source, edit.Updated);
    }

    [Fact]
    public void ApplyIdentities_succeeds_as_a_no_op_when_there_are_no_declarations_and_no_identities()
    {
        var source = "# just a comment\n";

        var edit = RmlCompiler.ApplyIdentities(source, [], TestContext.Current.CancellationToken);

        Assert.False(edit.Changed);
        Assert.Equal(source, edit.Updated);
    }

    [Fact]
    public void ApplyIdentities_throws_when_the_identity_registry_does_not_cover_every_declaration()
    {
        var source = "rml 1.0\ncontext Child Care\n  version 1.0.0\nend\n";

        var exception = Assert.Throws<ArgumentException>(() => RmlCompiler.ApplyIdentities(source, [], TestContext.Current.CancellationToken));
        Assert.Equal("identities", exception.ParamName);
    }

    [Fact]
    public void ApplyIdentities_throws_when_an_identity_is_not_a_valid_uuid_v7()
    {
        var source = "rml 1.0\ncontext Child Care\n  version 1.0.0\nend\n";

        var exception = Assert.Throws<ArgumentException>(() => RmlCompiler.ApplyIdentities(source, ["not-a-guid"], TestContext.Current.CancellationToken));
        Assert.Equal("identities", exception.ParamName);
    }

    [Fact]
    public void ApplyIdentities_throws_when_an_identity_is_a_well_formed_guid_but_not_version_7()
    {
        var source = "rml 1.0\ncontext Child Care\n  version 1.0.0\nend\n";
        var guidV4 = Guid.NewGuid().ToString();

        var exception = Assert.Throws<ArgumentException>(() => RmlCompiler.ApplyIdentities(source, [guidV4], TestContext.Current.CancellationToken));
        Assert.Equal("identities", exception.ParamName);
    }

    [Fact]
    public void ApplyIdentities_throws_when_the_identity_registry_has_unused_identities_left_over()
    {
        var source = "rml 1.0\ncontext Child Care\n  version 1.0.0\nend\n";

        var exception = Assert.Throws<ArgumentException>(() => RmlCompiler.ApplyIdentities(
            source,
            ["0191f6d4-4ea0-7000-8000-000000000001", "0191f6d4-4ea0-7000-8000-000000000002"], TestContext.Current.CancellationToken));
        Assert.Equal("identities", exception.ParamName);
    }

    [Fact]
    public void ApplyIdentities_throws_for_null_source_or_identities()
    {
        Assert.Throws<ArgumentNullException>(() => RmlCompiler.ApplyIdentities(null!, [], TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentNullException>(() => RmlCompiler.ApplyIdentities("rml 1.0\n", null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void HarvestIdentities_reads_back_the_ordered_identities_ensure_identities_minted()
    {
        var source = "rml 1.0\ncontext Child Care\n  version 1.0.0\nend\n";
        var minted = RmlCompiler.EnsureIdentities(source, TestContext.Current.CancellationToken).Updated;

        var harvested = RmlCompiler.HarvestIdentities(minted);

        Assert.Single(harvested);
        Assert.Matches("^[0-9a-fA-F-]{36}$", harvested[0]);
    }

    [Fact]
    public void HarvestIdentities_round_trips_through_apply_identities()
    {
        var source = "rml 1.0\ncontext Child Care\n  version 1.0.0\nend\n";
        var minted = RmlCompiler.EnsureIdentities(source, TestContext.Current.CancellationToken).Updated;
        var harvested = RmlCompiler.HarvestIdentities(minted);

        var reapplied = RmlCompiler.ApplyIdentities(source, harvested, TestContext.Current.CancellationToken).Updated;

        Assert.Equal(minted, reapplied);
    }

    [Fact]
    public void HarvestIdentities_returns_identities_in_declaration_order_for_multiple_declarations()
    {
        var source = "rml 1.0\ncontext Child Care\n  version 1.0.0\n  fact Age\n    type integer\n  end\nend\n";
        var minted = RmlCompiler.EnsureIdentities(source, TestContext.Current.CancellationToken).Updated;

        var harvested = RmlCompiler.HarvestIdentities(minted);

        Assert.Equal(2, harvested.Length);
        Assert.NotEqual(harvested[0], harvested[1]);
    }

    [Fact]
    public void HarvestIdentities_throws_when_a_declaration_lacks_a_preceding_identity_comment()
    {
        var source = "rml 1.0\ncontext Child Care\n  version 1.0.0\nend\n";

        var exception = Assert.Throws<ArgumentException>(() => RmlCompiler.HarvestIdentities(source));
        Assert.Equal("source", exception.ParamName);
    }

    [Fact]
    public void HarvestIdentities_throws_when_a_preceding_comment_is_not_a_valid_uuid_v7()
    {
        var source = "rml 1.0\n# @id=00000000-0000-4000-8000-000000000000\ncontext Child Care\n  version 1.0.0\nend\n";

        Assert.Throws<ArgumentException>(() => RmlCompiler.HarvestIdentities(source));
    }

    [Fact]
    public void HarvestIdentities_skips_a_declaration_keyword_whose_value_is_itself_a_quoted_reference()
    {
        var source = "rule \"Quoted Value\"\nend\n";

        var harvested = RmlCompiler.HarvestIdentities(source);

        Assert.Empty(harvested);
    }

    [Fact]
    public void HarvestIdentities_returns_empty_for_a_source_with_no_declarations()
    {
        var harvested = RmlCompiler.HarvestIdentities("# just a comment\n");

        Assert.Empty(harvested);
    }

    [Fact]
    public void HarvestIdentities_throws_for_null_source()
    {
        Assert.Throws<ArgumentNullException>(() => RmlCompiler.HarvestIdentities(null!));
    }

    [Fact]
    public void EnsureIdentities_throws_when_cancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() => RmlCompiler.EnsureIdentities("rml 1.0\ncontext Child Care\nend\n", cts.Token));
    }

    [Fact]
    public void ApplyIdentities_throws_when_cancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() => RmlCompiler.ApplyIdentities("rml 1.0\ncontext Child Care\nend\n", [], cts.Token));
    }

    [Fact]
    public void CompileWorkspace_compiles_every_declared_bounded_context()
    {
        const string source = """
            rml 1.0
            context Child Care
              version 1.0.0
            end
            context Centre Operations
              version 1.0.0
            end
            """;

        var result = RmlCompiler.CompileWorkspace(
            [new SourceDocument("workspace.rml", source)], ParseOptions.EditorLanguage1, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(["Child Care", "Centre Operations"], result.Contexts.Select(context => context.AuthoredRevision.Name.Value));
        Assert.Empty(result.Dependencies);
    }

    [Fact]
    public void CompileWorkspace_records_a_dependency_for_an_import_of_an_exported_fact()
    {
        const string source = """
            rml 1.0
            context Child Care
              version 1.0.0
            end
            fact Active enrolment exists
              type truth
              export
            end
            context Centre Operations
              version 1.0.0
              import "Active enrolment exists"
                from "Child Care"
              end
            end
            """;

        var result = RmlCompiler.CompileWorkspace(
            [new SourceDocument("workspace.rml", source)], ParseOptions.EditorLanguage1, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var dependency = Assert.Single(result.Dependencies);
        Assert.Equal("Centre Operations", dependency.ImportingContextName.Value);
        Assert.Equal("Child Care", dependency.ExportingContextName.Value);
        Assert.Equal("Active enrolment exists", dependency.FactName.Value);
    }

    [Fact]
    public void CompileWorkspace_rejects_an_import_of_a_fact_that_is_not_exported()
    {
        const string source = """
            rml 1.0
            context Child Care
              version 1.0.0
            end
            fact Active enrolment exists
              type truth
            end
            context Centre Operations
              version 1.0.0
              import "Active enrolment exists"
                from "Child Care"
              end
            end
            """;

        var result = RmlCompiler.CompileWorkspace(
            [new SourceDocument("workspace.rml", source)], ParseOptions.EditorLanguage1, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("rml.import.not-exported", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void CompileWorkspace_rejects_an_import_naming_an_unresolved_bounded_context()
    {
        const string source = """
            rml 1.0
            context Centre Operations
              version 1.0.0
              import "Active enrolment exists"
                from "Child Care"
              end
            end
            """;

        var result = RmlCompiler.CompileWorkspace(
            [new SourceDocument("workspace.rml", source)], ParseOptions.EditorLanguage1, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("rml.import.context-unresolved", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void CompileWorkspace_supports_entities_enumerations_rules_and_behaviours_across_contexts()
    {
        const string source = """
            rml 1.0
            context Child Care
              version 1.0.0
            end
            enumeration Room status type
              member Open
                value 1
              end
            end
            entity Room
              field Room name
                type text
              end
              lifecycle Room lifecycle
                stage Draft
                stage Active
              end
            end
            entity Child
              relationship Assigned room
                target "Room"
                cardinality one
              end
            end
            fact Active enrolment exists
              type truth
            end
            rule Determine eligibility
              input "Active enrolment exists"
              when all
                fact "Active enrolment exists"
              end
              conclusion Eligible
              end
            end
            behaviour Activate room
              for "Room"
              requires "Determine eligibility"
              outcome Activated
              end
              transition Activate
                lifecycle "Room lifecycle"
                from "Draft"
                to "Active"
                outcome "Activated"
              end
            end
            context Centre Operations
              version 1.0.0
            end
            """;

        var result = RmlCompiler.CompileWorkspace(
            [new SourceDocument("workspace.rml", source)], ParseOptions.EditorLanguage1, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, string.Join("; ", result.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message} @ {diagnostic.Location?.Line}")));
        Assert.Equal(["Child Care", "Centre Operations"], result.Contexts.Select(context => context.AuthoredRevision.Name.Value));
        var childCare = result.Contexts.Single(context => context.AuthoredRevision.Name.Value == "Child Care").AuthoredRevision;
        Assert.Contains(childCare.Definitions, definition => definition is EntityDefinition entity && entity.Name.Value == "Room");
        Assert.Contains(childCare.Definitions, definition => definition is EntityDefinition entity && entity.Name.Value == "Child");
        Assert.Contains(childCare.Definitions, definition => definition is EnumerationDefinition enumeration && enumeration.Name.Value == "Room status type");
        Assert.Contains(childCare.Definitions, definition => definition is RuleDefinition rule && rule.Name.Value == "Determine eligibility");
        Assert.Contains(childCare.Definitions, definition => definition is BehaviourDefinition behaviour && behaviour.Name.Value == "Activate room");
        var centreOperations = result.Contexts.Single(context => context.AuthoredRevision.Name.Value == "Centre Operations").AuthoredRevision;
        Assert.Empty(centreOperations.Definitions);
    }

    [Fact]
    public void CompileWorkspace_rejects_two_bounded_contexts_sharing_a_name()
    {
        const string source = """
            rml 1.0
            context Child Care
              version 1.0.0
            end
            context Child Care
              version 1.0.0
            end
            """;

        var result = RmlCompiler.CompileWorkspace(
            [new SourceDocument("workspace.rml", source)], ParseOptions.EditorLanguage1, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("rml.name.duplicate", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void CompileWorkspace_returns_a_cancelled_result_when_the_token_is_already_cancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = RmlCompiler.CompileWorkspace(
            [new SourceDocument("workspace.rml", "rml 1.0\ncontext Child Care\n  version 1.0.0\nend\n")], ParseOptions.EditorLanguage1, cts.Token);

        Assert.True(result.IsCancelled);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void RequiresWorkspaceCompilation_is_false_for_a_single_context_with_no_import()
    {
        const string source = "rml 1.0\ncontext Child Care\n  version 1.0.0\nend\n";

        Assert.False(RmlCompiler.RequiresWorkspaceCompilation([new SourceDocument("workspace.rml", source)]));
    }

    [Fact]
    public void RequiresWorkspaceCompilation_is_true_for_more_than_one_declared_context()
    {
        const string source = "rml 1.0\ncontext Child Care\n  version 1.0.0\nend\ncontext Centre Operations\n  version 1.0.0\nend\n";

        Assert.True(RmlCompiler.RequiresWorkspaceCompilation([new SourceDocument("workspace.rml", source)]));
    }

    [Fact]
    public void RequiresWorkspaceCompilation_is_true_for_a_single_context_that_declares_an_import()
    {
        const string source = """
            rml 1.0
            context Centre Operations
              version 1.0.0
              import "Active enrolment exists"
                from "Child Care"
              end
            end
            """;

        Assert.True(RmlCompiler.RequiresWorkspaceCompilation([new SourceDocument("workspace.rml", source)]));
    }

    [Fact]
    public void RequiresWorkspaceCompilation_counts_context_declarations_across_multiple_documents()
    {
        var documents = new[]
        {
            new SourceDocument("a.rml", "rml 1.0\ncontext Child Care\n  version 1.0.0\nend\n"),
            new SourceDocument("b.rml", "context Centre Operations\n  version 1.0.0\nend\n"),
        };

        Assert.True(RmlCompiler.RequiresWorkspaceCompilation(documents));
    }
}
