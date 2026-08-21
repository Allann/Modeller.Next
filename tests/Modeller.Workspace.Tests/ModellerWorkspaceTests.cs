using System.Collections.Immutable;
using Modeller.Configuration;
using Modeller.Model;
using Modeller.Projections;
using Modeller.Workspace;
using Xunit;

namespace Modeller.Workspace.Tests;

public sealed class ModellerWorkspaceTests
{
    // A genuine multi-document RML workspace: one document declares the context and an entity with
    // a lifecycle, the other declares the facts, rule, and behaviour that reference it. Analyzing
    // and projecting this — using only WorkspaceInput/ModellerWorkspace, no ICliHost, no System.IO —
    // is the acceptance-criterion proof that a multi-document workspace can be analyzed and
    // projected without reading or writing the host filesystem.
    private const string ContextAndEntityDocument = """
        rml 1.0
        context Child Care
          version 1.0.0
        end
        entity ACCS determination application
          lifecycle ACCS determination application lifecycle
            stage Draft
            stage Submitted
          end
        end
        """;

    private const string FactsAndRuleDocument = """
        fact Active enrolment exists
          type truth
          export
        end
        fact Supporting evidence is held
          type truth
          export
        end
        rule Determine ACCS eligibility
          input "Active enrolment exists"
          input "Supporting evidence is held"
          when all
            fact "Active enrolment exists"
            fact "Supporting evidence is held"
          end
          conclusion Eligible
          end
          finding "Active enrolment exists" true accs.active-enrolment-confirmed
          finding "Supporting evidence is held" true accs.supporting-evidence-confirmed
          export
        end
        behaviour Submit ACCS determination application
          for "ACCS determination application"
          requires "Determine ACCS eligibility"
          outcome Application submitted
          end
          outcome Application rejected
          end
          transition Submit application
            lifecycle "ACCS determination application lifecycle"
            from "Draft"
            to "Submitted"
            outcome "Application submitted"
          end
        end
        """;

    private static WorkspaceInput EphemeralWorkspace() => new(
        [
            new(LogicalPath.Create("model/context.rml"), ContextAndEntityDocument),
            new(LogicalPath.Create("model/rules.rml"), FactsAndRuleDocument),
        ],
        IdentityStrategy.Ephemeral.Instance,
        new WorkspaceConfigurationInput("1.0", "generated/"));

    [Fact]
    public void Analyze_parses_a_multi_document_ephemeral_workspace_entirely_in_memory()
    {
        var outcome = ModellerWorkspace.Analyze(EphemeralWorkspace(), TestContext.Current.CancellationToken);

        var analyzed = Assert.IsType<WorkspaceOutcome<AnalyzedWorkspace>.Success>(outcome).Value;
        Assert.NotNull(analyzed.Package);
        Assert.Contains(analyzed.Package.AuthoredRevision.Definitions, definition => definition is EntityDefinition);
        Assert.Contains(analyzed.Package.AuthoredRevision.Definitions, definition => definition is RuleDefinition);
        Assert.Contains(analyzed.Package.AuthoredRevision.Definitions, definition => definition is BehaviourDefinition);
        Assert.Equal(2, analyzed.IdentifiedDocuments.Length);
    }

    [Fact]
    public void Analyze_and_Project_a_multi_document_workspace_without_any_filesystem_access()
    {
        var analyzed = Assert.IsType<WorkspaceOutcome<AnalyzedWorkspace>.Success>(ModellerWorkspace.Analyze(EphemeralWorkspace(), TestContext.Current.CancellationToken)).Value;
        var entity = Assert.Single(analyzed.Package.AuthoredRevision.Definitions.OfType<EntityDefinition>());

        var outcome = ModellerWorkspace.Project(analyzed, new ViewDefinition("lifecycle:root", 1, ViewKind.Lifecycle, [entity.Id]), cancellationToken: TestContext.Current.CancellationToken);

        var projection = Assert.IsType<WorkspaceOutcome<ProjectionResult>.Success>(outcome).Value;
        Assert.True(projection.Succeeded, string.Join(",", projection.Diagnostics));
        Assert.NotEmpty(projection.Graph!.Nodes);
    }

    [Fact]
    public void Analyze_returns_a_document_missing_diagnostic_when_the_durable_registry_does_not_cover_a_document()
    {
        var input = new WorkspaceInput(
            [new(LogicalPath.Create("model/context.rml"), ContextAndEntityDocument)],
            new IdentityStrategy.Durable(WorkspaceIdentityRegistry.Empty),
            new WorkspaceConfigurationInput("1.0", "generated/"));

        var outcome = ModellerWorkspace.Analyze(input, TestContext.Current.CancellationToken);

        var failed = Assert.IsType<WorkspaceOutcome<AnalyzedWorkspace>.Failed>(outcome);
        Assert.Equal("workspace.identity-registry.document-missing", Assert.Single(failed.Diagnostics).Code);
    }

    [Fact]
    public void Analyze_returns_an_out_of_sync_diagnostic_when_the_durable_registry_does_not_match_the_document()
    {
        var path = LogicalPath.Create("model/context.rml");
        var registry = new WorkspaceIdentityRegistry("1.0", ImmutableDictionary<LogicalPath, ImmutableArray<string>>.Empty.Add(path, ["not-a-guid"]));
        var input = new WorkspaceInput([new(path, ContextAndEntityDocument)], new IdentityStrategy.Durable(registry), new WorkspaceConfigurationInput("1.0", "generated/"));

        var outcome = ModellerWorkspace.Analyze(input, TestContext.Current.CancellationToken);

        var failed = Assert.IsType<WorkspaceOutcome<AnalyzedWorkspace>.Failed>(outcome);
        Assert.Equal("workspace.identity-registry.out-of-sync", Assert.Single(failed.Diagnostics).Code);
    }

    [Fact]
    public void Analyze_returns_failed_when_configuration_resolution_fails()
    {
        var input = new WorkspaceInput(
            [new(LogicalPath.Create("model/context.rml"), ContextAndEntityDocument)],
            IdentityStrategy.Ephemeral.Instance,
            new WorkspaceConfigurationInput("${undefined-variable}", "generated/"));

        var outcome = ModellerWorkspace.Analyze(input, TestContext.Current.CancellationToken);

        var failed = Assert.IsType<WorkspaceOutcome<AnalyzedWorkspace>.Failed>(outcome);
        Assert.Equal("configuration.variable.unresolved", Assert.Single(failed.Diagnostics).Code);
    }

    [Fact]
    public void Analyze_surfaces_a_diagnostic_for_malformed_rml()
    {
        var input = new WorkspaceInput(
            [new(LogicalPath.Create("model/context.rml"), "rml 1.0\ncontext Child Care\n  version 1.0.0\n")], // missing 'end'
            IdentityStrategy.Ephemeral.Instance,
            new WorkspaceConfigurationInput("1.0", "generated/"));

        var outcome = ModellerWorkspace.Analyze(input, TestContext.Current.CancellationToken);

        var failed = Assert.IsType<WorkspaceOutcome<AnalyzedWorkspace>.Failed>(outcome);
        Assert.Equal("rml.block.unclosed", Assert.Single(failed.Diagnostics).Code);
    }

    private const string ChildCareExportingDocument = """
        rml 1.0
        context Child Care
          version 1.0.0
        end
        fact Active enrolment exists
          type truth
          export
        end
        """;

    private const string CentreOperationsImportingDocument = """
        context Centre Operations
          version 1.0.0
          import "Active enrolment exists"
            from "Child Care"
          end
        end
        """;

    private static WorkspaceInput TwoContextEphemeralWorkspace() => new(
        [
            new(LogicalPath.Create("model/child-care.rml"), ChildCareExportingDocument),
            new(LogicalPath.Create("model/centre-operations.rml"), CentreOperationsImportingDocument),
        ],
        IdentityStrategy.Ephemeral.Instance,
        new WorkspaceConfigurationInput("1.0", "generated/"));

    [Fact]
    public void Analyze_a_single_context_workspace_populates_Contexts_with_just_the_primary_package()
    {
        var analyzed = Assert.IsType<WorkspaceOutcome<AnalyzedWorkspace>.Success>(
            ModellerWorkspace.Analyze(EphemeralWorkspace(), TestContext.Current.CancellationToken)).Value;

        var context = Assert.Single(analyzed.Contexts);
        Assert.Same(analyzed.Package, context);
        Assert.Empty(analyzed.Dependencies);
    }

    [Fact]
    public void Analyze_a_workspace_declaring_two_bounded_contexts_routes_through_CompileWorkspace()
    {
        var outcome = ModellerWorkspace.Analyze(TwoContextEphemeralWorkspace(), TestContext.Current.CancellationToken);

        var analyzed = Assert.IsType<WorkspaceOutcome<AnalyzedWorkspace>.Success>(outcome).Value;
        Assert.Equal(2, analyzed.Contexts.Length);
        Assert.Contains(analyzed.Contexts, context => context.AuthoredRevision.Name.Value == "Child Care");
        Assert.Contains(analyzed.Contexts, context => context.AuthoredRevision.Name.Value == "Centre Operations");
        var dependency = Assert.Single(analyzed.Dependencies);
        Assert.Equal("Active enrolment exists", dependency.FactName.Value);
    }

    [Fact]
    public void Analyze_a_workspace_declaring_a_single_context_with_no_import_does_not_route_through_CompileWorkspace()
    {
        var analyzed = Assert.IsType<WorkspaceOutcome<AnalyzedWorkspace>.Success>(
            ModellerWorkspace.Analyze(EphemeralWorkspace(), TestContext.Current.CancellationToken)).Value;

        // Single-context workspaces keep going through the original Compile() path: Package still
        // carries the full LoadedContextPackage (imports/exports/digests), not just a revision.
        Assert.NotNull(analyzed.Package.PackageDigest);
        Assert.NotNull(analyzed.Package.SemanticDigest);
    }

    [Fact]
    public void Analyze_does_not_route_a_non_rml_workspace_through_CompileWorkspace_even_if_it_looks_multi_context()
    {
        // Neither line starts with "rml " (IsRml is false), but both start with "context " (so
        // RequiresWorkspaceCompilation alone would be true). Routing must require BOTH — an
        // accidental && -> || here would send non-RML source into the RML compiler instead of the
        // legacy parser, producing an RML-flavoured diagnostic instead of the legacy one.
        const string nonRmlSource =
            "context id=00000000-0000-7000-8000-000000000001 name=\"A\" slug=a version=1.0.0\n" +
            "context id=00000000-0000-7000-8000-000000000002 name=\"B\" slug=b version=1.0.0\n";
        var input = new WorkspaceInput(
            [new(LogicalPath.Create("model/legacy.modeller"), nonRmlSource)],
            IdentityStrategy.Ephemeral.Instance,
            new WorkspaceConfigurationInput("1.0", "generated/"));

        var outcome = ModellerWorkspace.Analyze(input, TestContext.Current.CancellationToken);

        var failed = Assert.IsType<WorkspaceOutcome<AnalyzedWorkspace>.Failed>(outcome);
        var diagnostic = Assert.Single(failed.Diagnostics);
        Assert.Equal("parse.statement.required", diagnostic.Code);
    }

    [Fact]
    public void Analyze_remaps_a_multi_context_diagnostic_location_back_to_the_submitted_source()
    {
        const string centreOperationsWithUnresolvedImport = """
            context Centre Operations
              version 1.0.0
              import "Active enrolment exists"
                from "Unknown Context"
              end
            end
            """;
        var input = new WorkspaceInput(
            [
                new(LogicalPath.Create("model/child-care.rml"), ChildCareExportingDocument),
                new(LogicalPath.Create("model/centre-operations.rml"), centreOperationsWithUnresolvedImport),
            ],
            IdentityStrategy.Ephemeral.Instance,
            new WorkspaceConfigurationInput("1.0", "generated/"));

        var outcome = ModellerWorkspace.Analyze(input, TestContext.Current.CancellationToken);

        var failed = Assert.IsType<WorkspaceOutcome<AnalyzedWorkspace>.Failed>(outcome);
        var diagnostic = Assert.Single(failed.Diagnostics);
        Assert.Equal("rml.import.context-unresolved", diagnostic.Code);
        Assert.NotNull(diagnostic.Location);
        Assert.Equal("model/centre-operations.rml", diagnostic.Location.Document);
        Assert.Equal(4, diagnostic.Location.Line);
    }

    [Fact]
    public void Analyze_remaps_multi_context_provenance_spans_back_to_the_submitted_source()
    {
        // Ephemeral identity mints a "# @id=" comment before "context Child Care" (submitted line
        // 2), shifting it to identified line 3. Provenance must be remapped back to line 2 — the
        // line a client that only ever sees the submitted source can actually navigate to.
        var analyzed = Assert.IsType<WorkspaceOutcome<AnalyzedWorkspace>.Success>(
            ModellerWorkspace.Analyze(TwoContextEphemeralWorkspace(), TestContext.Current.CancellationToken)).Value;
        var childCare = analyzed.Contexts.Single(context => context.AuthoredRevision.Name.Value == "Child Care");

        var provenance = Assert.Single(analyzed.Provenance, item => item.SemanticId == childCare.AuthoredRevision.Id.ToString());

        Assert.Equal("model/child-care.rml", provenance.Span.Document);
        Assert.Equal(2, provenance.Span.Line);
    }

    [Fact]
    public void Project_a_context_map_via_ModellerWorkspace_shows_the_cross_context_dependency_edge()
    {
        var analyzed = Assert.IsType<WorkspaceOutcome<AnalyzedWorkspace>.Success>(
            ModellerWorkspace.Analyze(TwoContextEphemeralWorkspace(), TestContext.Current.CancellationToken)).Value;
        var childCare = analyzed.Contexts.Single(context => context.AuthoredRevision.Name.Value == "Child Care");

        var outcome = ModellerWorkspace.Project(
            analyzed, new ViewDefinition("context-map", 1, ViewKind.ContextMap, [childCare.AuthoredRevision.Id]),
            cancellationToken: TestContext.Current.CancellationToken);

        var projection = Assert.IsType<WorkspaceOutcome<ProjectionResult>.Success>(outcome).Value;
        Assert.True(projection.Succeeded, string.Join(",", projection.Diagnostics));
        Assert.Equal(2, projection.Graph!.Nodes.Length);
        Assert.Single(projection.Graph.Edges);
    }

    [Fact]
    public void Analyze_returns_cancelled_for_a_pre_cancelled_token()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var outcome = ModellerWorkspace.Analyze(EphemeralWorkspace(), cts.Token);

        Assert.IsType<WorkspaceOutcome<AnalyzedWorkspace>.Cancelled>(outcome);
    }

    [Fact]
    public void Project_returns_cancelled_for_a_pre_cancelled_token()
    {
        var analyzed = Assert.IsType<WorkspaceOutcome<AnalyzedWorkspace>.Success>(ModellerWorkspace.Analyze(EphemeralWorkspace(), TestContext.Current.CancellationToken)).Value;
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var outcome = ModellerWorkspace.Project(analyzed, new ViewDefinition("v", 1, ViewKind.Lifecycle, []), cancellationToken: cts.Token);

        Assert.IsType<WorkspaceOutcome<ProjectionResult>.Cancelled>(outcome);
    }

    [Fact]
    public void SupportedViewKinds_lists_exactly_the_view_kinds_DiagramProjector_implements()
    {
        Assert.Equal(Enum.GetValues<ViewKind>().ToHashSet(), ModellerWorkspace.SupportedViewKinds.ToHashSet());
    }

    [Fact]
    public void Export_harvests_ephemeral_identities_into_a_durable_registry_that_reproduces_the_same_package_on_reanalysis()
    {
        var ephemeralOutcome = ModellerWorkspace.Analyze(EphemeralWorkspace(), TestContext.Current.CancellationToken);
        var ephemeral = Assert.IsType<WorkspaceOutcome<AnalyzedWorkspace>.Success>(ephemeralOutcome).Value;

        var exportOutcome = ModellerWorkspace.Export(ephemeral);
        var registry = Assert.IsType<WorkspaceOutcome<WorkspaceIdentityRegistry>.Success>(exportOutcome).Value;

        var durableInput = new WorkspaceInput(
            [
                new(LogicalPath.Create("model/context.rml"), ContextAndEntityDocument),
                new(LogicalPath.Create("model/rules.rml"), FactsAndRuleDocument),
            ],
            new IdentityStrategy.Durable(registry),
            new WorkspaceConfigurationInput("1.0", "generated/"));
        var durableOutcome = ModellerWorkspace.Analyze(durableInput, TestContext.Current.CancellationToken);
        var durable = Assert.IsType<WorkspaceOutcome<AnalyzedWorkspace>.Success>(durableOutcome).Value;

        Assert.Equal(ephemeral.Package.PackageDigest, durable.Package.PackageDigest);
        Assert.Equal(ephemeral.Package.SemanticDigest, durable.Package.SemanticDigest);
    }

    [Fact]
    public void Export_is_idempotent_when_re_exporting_an_already_durable_workspace()
    {
        var ephemeral = Assert.IsType<WorkspaceOutcome<AnalyzedWorkspace>.Success>(ModellerWorkspace.Analyze(EphemeralWorkspace(), TestContext.Current.CancellationToken)).Value;
        var registry = Assert.IsType<WorkspaceOutcome<WorkspaceIdentityRegistry>.Success>(ModellerWorkspace.Export(ephemeral)).Value;
        var durableInput = new WorkspaceInput(
            [
                new(LogicalPath.Create("model/context.rml"), ContextAndEntityDocument),
                new(LogicalPath.Create("model/rules.rml"), FactsAndRuleDocument),
            ],
            new IdentityStrategy.Durable(registry),
            new WorkspaceConfigurationInput("1.0", "generated/"));
        var durable = Assert.IsType<WorkspaceOutcome<AnalyzedWorkspace>.Success>(ModellerWorkspace.Analyze(durableInput, TestContext.Current.CancellationToken)).Value;

        var reExported = Assert.IsType<WorkspaceOutcome<WorkspaceIdentityRegistry>.Success>(ModellerWorkspace.Export(durable)).Value;

        Assert.Equal(registry.Documents.Count, reExported.Documents.Count);
        foreach (var (path, identities) in registry.Documents)
            Assert.Equal(identities, reExported.Documents[path]);
    }

    [Fact]
    public void Export_throws_a_diagnostic_when_a_document_has_not_yet_been_identified()
    {
        var reference = Assert.IsType<WorkspaceOutcome<AnalyzedWorkspace>.Success>(ModellerWorkspace.Analyze(EphemeralWorkspace(), TestContext.Current.CancellationToken)).Value;
        var analyzed = reference with
        {
            IdentifiedDocuments = [new(LogicalPath.Create("model/context.rml"), "rule Some Rule\nend\n")],
        };

        var outcome = ModellerWorkspace.Export(analyzed);

        Assert.Equal("workspace.identity-registry.harvest-failed", Assert.Single(Assert.IsType<WorkspaceOutcome<WorkspaceIdentityRegistry>.Failed>(outcome).Diagnostics).Code);
    }
}
