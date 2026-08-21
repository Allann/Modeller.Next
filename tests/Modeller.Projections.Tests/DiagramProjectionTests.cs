using System.Collections.Immutable;
using Modeller.Model;
using Modeller.Projections;
using Xunit;

namespace Modeller.Projections.Tests;

public sealed class DiagramProjectionTests
{
    [Fact]
    public void Child_care_lifecycle_is_projected_deterministically_without_layout_authority()
    {
        var revision = ChildCare.Revision();
        var view = new ViewDefinition("accs-lifecycle", 1, ViewKind.Lifecycle, [ChildCare.ApplicationId]);

        var first = DiagramProjector.Project(revision, view, cancellationToken: TestContext.Current.CancellationToken);
        var second = DiagramProjector.Project(revision, view, new LayoutState(ImmutableDictionary<string, ElementLayout>.Empty
            .Add("stage:draft", new ElementLayout(400, 200))), TestContext.Current.CancellationToken);

        Assert.True(first.Succeeded);
        Assert.Equal(first.Graph!.Nodes.Select(NodeShape), second.Graph!.Nodes.Select(NodeShape));
        Assert.Equal(first.Graph.Edges.Select(EdgeShape), second.Graph.Edges.Select(EdgeShape));
        Assert.Collection(first.Graph.Nodes,
            node => Assert.Equal("Draft", node.Label),
            node => Assert.Equal("Submitted", node.Label));
        var edge = Assert.Single(first.Graph.Edges);
        Assert.Equal("Submit application", edge.Label);
        Assert.Equal("Draft", first.Graph.Nodes.Single(n => n.Id == edge.SourceId).Label);
        Assert.Equal("Submitted", first.Graph.Nodes.Single(n => n.Id == edge.TargetId).Label);
    }

    [Fact]
    public void Rule_decision_view_explains_child_care_rule_expression()
    {
        var revision = CanonicalModel.Apply(ChildCare.Revision(), new AddDefinition(ChildCare.Rule())).Revision;
        var result = DiagramProjector.Project(revision, new("accs-rule", 1, ViewKind.RuleDecision, [ChildCare.RuleId]), cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(["Active enrolment exists", "Supporting evidence is held", "Eligible"], result.Graph!.Nodes.Select(n => n.Label));
        Assert.Equal(2, result.Graph.Edges.Length);
        Assert.All(result.Graph.Edges, edge => Assert.Equal("input", edge.Role));
    }

    [Fact]
    public void Structural_view_contains_entities_for_a_context_root()
    {
        var revision = ChildCare.Revision();
        var result = DiagramProjector.Project(revision, new("structure", 1, ViewKind.Structural, [ChildCare.ContextId]), cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Contains(result.Graph!.Nodes, node => node.Role == "entity" && node.Label == "ACCS determination application");
    }

    [Fact]
    public void Behaviour_map_reveals_a_behaviour_and_its_published_event_for_an_entity_root()
    {
        var revision = ChildCare.Revision();
        var result = DiagramProjector.Project(revision, new("behaviour-map", 1, ViewKind.BehaviourMap, [ChildCare.ApplicationId]), cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Contains(result.Graph!.Nodes, node => node.Role == "behaviour" && node.Label == "Submit ACCS determination application");
        Assert.Contains(result.Graph.Nodes, node => node.Role == "outcome" && node.Label == "Application submitted");
        Assert.Contains(result.Graph.Nodes, node => node.Role == "event" && node.Label == "Application submitted event");
        Assert.Contains(result.Graph.Edges, edge => edge.Role == "publishes");
    }

    [Fact]
    public void Behaviour_map_requires_an_entity_root()
    {
        var result = DiagramProjector.Project(ChildCare.Revision(), new("behaviour-map", 1, ViewKind.BehaviourMap, [ChildCare.ContextId]), cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("projection.root.invalid", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Causality_and_event_flow_view_shows_the_behaviour_that_publishes_an_event()
    {
        var revision = ChildCare.Revision();
        var result = DiagramProjector.Project(revision, new("event-flow", 1, ViewKind.CausalityAndEventFlow, [ChildCare.ContextId]), cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        var edge = Assert.Single(result.Graph!.Edges);
        Assert.Equal("publishes", edge.Role);
        Assert.Equal("Submit ACCS determination application", result.Graph.Nodes.Single(n => n.Id == edge.SourceId).Label);
        Assert.Equal("Application submitted event", result.Graph.Nodes.Single(n => n.Id == edge.TargetId).Label);
    }

    [Fact]
    public void Context_map_shows_the_context_root_itself()
    {
        var revision = ChildCare.Revision();
        var result = DiagramProjector.Project(revision, new("context-map", 1, ViewKind.ContextMap, [ChildCare.ContextId]), cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        var node = Assert.Single(result.Graph!.Nodes);
        Assert.Equal("context", node.Role);
        Assert.Equal("Child Care", node.Label);
    }

    [Fact]
    public void ProjectContextMap_shows_a_dependency_edge_for_a_declared_import()
    {
        var childCare = ChildCare.Revision();
        var centreOperations = AuthoredContextRevision.Create(CentreOperationsId, new("Centre Operations"), new("centre-operations"), "1.0.0");
        var dependency = new ContextDependency(
            CentreOperationsId, new("Centre Operations"),
            ChildCare.ContextId, new("Child Care"),
            SemanticId.Parse("0191f6d4-4ea0-7000-8000-0000000000e1"), new("Active enrolment exists"));

        var result = DiagramProjector.ProjectContextMap(
            [childCare, centreOperations], [dependency],
            new("context-map", 1, ViewKind.ContextMap, [ChildCare.ContextId]),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Graph!.Nodes.Length);
        Assert.Contains(result.Graph.Nodes, node => node.Role == "context" && node.Label == "Child Care");
        Assert.Contains(result.Graph.Nodes, node => node.Role == "context" && node.Label == "Centre Operations");
        var edge = Assert.Single(result.Graph.Edges);
        Assert.Equal("import", edge.Role);
        Assert.Equal($"import:{CentreOperationsId}:{ChildCare.ContextId}:0191f6d4-4ea0-7000-8000-0000000000e1", edge.Id);
        Assert.Equal("Active enrolment exists", edge.Label);
        Assert.Equal("Centre Operations", result.Graph.Nodes.Single(n => n.Id == edge.SourceId).Label);
        Assert.Equal("Child Care", result.Graph.Nodes.Single(n => n.Id == edge.TargetId).Label);
    }

    [Fact]
    public void ProjectContextMap_shows_the_same_dependency_edge_when_rooted_at_the_importing_context()
    {
        var childCare = ChildCare.Revision();
        var centreOperations = AuthoredContextRevision.Create(CentreOperationsId, new("Centre Operations"), new("centre-operations"), "1.0.0");
        var dependency = new ContextDependency(
            CentreOperationsId, new("Centre Operations"),
            ChildCare.ContextId, new("Child Care"),
            SemanticId.Parse("0191f6d4-4ea0-7000-8000-0000000000e1"), new("Active enrolment exists"));

        var result = DiagramProjector.ProjectContextMap(
            [childCare, centreOperations], [dependency],
            new("context-map", 1, ViewKind.ContextMap, [CentreOperationsId]),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Graph!.Nodes.Length);
        Assert.Single(result.Graph.Edges);
    }

    [Fact]
    public void ProjectContextMap_returns_a_diagnostic_when_the_root_is_not_among_the_contexts()
    {
        var result = DiagramProjector.ProjectContextMap(
            [ChildCare.Revision()], [],
            new("context-map", 1, ViewKind.ContextMap, [CentreOperationsId]),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("projection.root.invalid", diagnostic.Code);
        Assert.Equal("A context map requires a context root.", diagnostic.Message);
    }

    [Fact]
    public void ProjectContextMap_returns_a_diagnostic_when_the_view_version_is_unsupported()
    {
        var result = DiagramProjector.ProjectContextMap(
            [ChildCare.Revision()], [], new("context-map", 2, ViewKind.ContextMap, [ChildCare.ContextId]),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("projection.view-version.unsupported", diagnostic.Code);
        Assert.Equal("View version '2' is not supported.", diagnostic.Message);
    }

    [Fact]
    public void ProjectContextMap_returns_a_diagnostic_when_the_view_kind_is_not_context_map()
    {
        var result = DiagramProjector.ProjectContextMap(
            [ChildCare.Revision()], [], new("view", 1, ViewKind.Structural, [ChildCare.ContextId]),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("projection.view-kind.unsupported", diagnostic.Code);
        Assert.Equal("'Structural' is not a context map.", diagnostic.Message);
    }

    [Fact]
    public void ProjectContextMap_throws_when_view_is_null()
    {
        Assert.Throws<ArgumentNullException>(() => DiagramProjector.ProjectContextMap(
            [ChildCare.Revision()], [], null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ProjectContextMap_throws_when_cancellation_already_requested()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // No contexts supplied: only the first ThrowIfCancellationRequested (before the root
        // lookup) can observably fire here — a graceful root-invalid diagnostic, not a thrown
        // exception, is what the second check further down would otherwise mask this into.
        Assert.Throws<OperationCanceledException>(() => DiagramProjector.ProjectContextMap(
            [], [], new("context-map", 1, ViewKind.ContextMap, [ChildCare.ContextId]), cts.Token));
    }

    [Fact]
    public void ProjectContextMap_for_a_single_context_workspace_has_no_dependency_edges()
    {
        var childCare = ChildCare.Revision();

        var result = DiagramProjector.ProjectContextMap(
            [childCare], [],
            new("context-map", 1, ViewKind.ContextMap, [ChildCare.ContextId]),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        var node = Assert.Single(result.Graph!.Nodes);
        Assert.Equal("Child Care", node.Label);
        Assert.Empty(result.Graph.Edges);
    }

    private static readonly SemanticId CentreOperationsId = SemanticId.Parse("0191f6d4-4ea0-7000-8000-0000000000e0");

    [Fact]
    public void Every_initial_view_kind_uses_the_same_projection_interface()
    {
        foreach (var kind in Enum.GetValues<ViewKind>())
        {
            var roots = kind switch
            {
                ViewKind.Lifecycle => ImmutableArray.Create(ChildCare.ApplicationId),
                ViewKind.Structural => ImmutableArray.Create(ChildCare.ContextId),
                ViewKind.BehaviourMap => ImmutableArray.Create(ChildCare.ApplicationId),
                ViewKind.CausalityAndEventFlow => ImmutableArray.Create(ChildCare.ContextId),
                ViewKind.ContextMap => ImmutableArray.Create(ChildCare.ContextId),
                _ => [],
            };
            var result = DiagramProjector.Project(ChildCare.Revision(), new($"view-{kind}", 1, kind, roots), cancellationToken: TestContext.Current.CancellationToken);
            Assert.True(result.Succeeded);
            Assert.Equal(kind, result.Graph!.Kind);
        }
    }

    [Fact]
    public void Gestures_translate_to_exactly_one_operation_category()
    {
        var revision = ChildCare.Revision();
        Assert.IsType<LayoutEdit>(ProjectionEditor.Translate(revision, new MoveElement("stage:draft", 10, 20)));
        Assert.IsType<ViewEdit>(ProjectionEditor.Translate(revision, new RemoveFromView(ChildCare.ApplicationId)));
        Assert.IsType<SessionEdit>(ProjectionEditor.Translate(revision, new HighlightEvaluation([ChildCare.ApplicationId])));
        Assert.IsType<SemanticEdit>(ProjectionEditor.Translate(revision, new DeleteFromModel(ChildCare.ApplicationId, revision.Revision)));
        Assert.IsType<InvalidEdit>(ProjectionEditor.Translate(revision, new SpatialRelationshipGesture("a", "b")));
    }

    [Fact]
    public void Stale_semantic_edit_conflicts_and_requests_reprojection()
    {
        var result = Assert.IsType<InvalidEdit>(ProjectionEditor.Translate(
            ChildCare.Revision(), new DeleteFromModel(ChildCare.ApplicationId, 0)));
        Assert.Equal("projection.edit.stale-revision", Assert.Single(result.Diagnostics).Code);
        Assert.True(result.ReprojectRequired);
    }

    private static string NodeShape(ProjectionNode node) => $"{node.Id}|{node.Role}|{node.Label}|{string.Join(',', node.SemanticIds)}";
    private static string EdgeShape(ProjectionEdge edge) => $"{edge.Id}|{edge.Role}|{edge.Label}|{edge.SourceId}|{edge.TargetId}|{string.Join(',', edge.SemanticIds)}";

    private static class ChildCare
    {
        internal static readonly SemanticId ContextId = Id("0191f6d4-4ea0-7000-8000-000000000001");
        internal static readonly SemanticId ApplicationId = Id("0191f6d4-4ea0-7000-8000-000000000002");
        internal static readonly SemanticId RuleId = Id("0191f6d4-4ea0-7000-8000-000000000008");
        private static readonly SemanticId LifecycleId = Id("0191f6d4-4ea0-7000-8000-000000000003");
        private static readonly SemanticId DraftId = Id("0191f6d4-4ea0-7000-8000-000000000004");
        private static readonly SemanticId SubmittedId = Id("0191f6d4-4ea0-7000-8000-000000000005");
        private static readonly SemanticId BehaviourId = Id("0191f6d4-4ea0-7000-8000-00000000000a");
        private static readonly SemanticId OutcomeId = Id("0191f6d4-4ea0-7000-8000-00000000000b");
        private static readonly SemanticId EventId = Id("0191f6d4-4ea0-7000-8000-00000000000c");
        private static readonly SemanticId TransitionId = Id("0191f6d4-4ea0-7000-8000-00000000000d");
        private static readonly SemanticId ActiveId = Id("0191f6d4-4ea0-7000-8000-000000000006");
        private static readonly SemanticId EvidenceId = Id("0191f6d4-4ea0-7000-8000-000000000007");
        private static readonly SemanticId EligibleId = Id("0191f6d4-4ea0-7000-8000-000000000009");

        internal static AuthoredContextRevision Revision()
        {
            var empty = AuthoredContextRevision.Create(ContextId, new("Child Care"), new("child-care"), "1.1.0");
            var entity = new EntityDefinition(ApplicationId, new("ACCS determination application"), new("accs-determination-application"),
                new(LifecycleId, new("ACCS determination application lifecycle"), new("accs-determination-application-lifecycle"),
                    [new(DraftId, new("Draft"), new("draft")), new(SubmittedId, new("Submitted"), new("submitted"))]));
            var behaviour = new BehaviourDefinition(BehaviourId, new("Submit ACCS determination application"), new("submit-accs-determination-application"), new(ApplicationId),
                [new(OutcomeId, new("Application submitted"), new("application-submitted"))], [],
                [new(EventId, new("Application submitted event"), new("application-submitted-event"))],
                [new(TransitionId, new("Submit application"), new("submit-application"), new(LifecycleId), new(DraftId), new(SubmittedId), new(OutcomeId))], []);
            return CanonicalModel.Apply(empty, new AddDefinition(entity),
                new AddDefinition(new FactDefinition(ActiveId, new("Active enrolment exists"), new("active-enrolment"), FactType.Truth)),
                new AddDefinition(new FactDefinition(EvidenceId, new("Supporting evidence is held"), new("supporting-evidence"), FactType.Truth)),
                new AddDefinition(behaviour)).Revision;
        }

        internal static RuleDefinition Rule() => new(RuleId, new("Determine ACCS eligibility"), new("determine-accs-eligibility"),
            [new(ActiveId), new(EvidenceId)], [new(EligibleId, new("Eligible"), new("eligible"))],
            new AndExpression([new FactExpression(new(ActiveId)), new FactExpression(new(EvidenceId))]));

        private static SemanticId Id(string value) => SemanticId.Parse(value);
    }
}
