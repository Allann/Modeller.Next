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

        var first = DiagramProjector.Project(revision, view);
        var second = DiagramProjector.Project(revision, view, new LayoutState(ImmutableDictionary<string, ElementLayout>.Empty
            .Add("stage:draft", new ElementLayout(400, 200))));

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
        var result = DiagramProjector.Project(revision, new("accs-rule", 1, ViewKind.RuleDecision, [ChildCare.RuleId]));

        Assert.True(result.Succeeded);
        Assert.Equal(["Active enrolment exists", "Supporting evidence is held", "Eligible"], result.Graph!.Nodes.Select(n => n.Label));
        Assert.Equal(2, result.Graph.Edges.Length);
        Assert.All(result.Graph.Edges, edge => Assert.Equal("input", edge.Role));
    }

    [Fact]
    public void Every_initial_view_kind_uses_the_same_projection_interface()
    {
        foreach (var kind in Enum.GetValues<ViewKind>())
        {
            var roots = kind == ViewKind.Lifecycle ? ImmutableArray.Create(ChildCare.ApplicationId) : [];
            var result = DiagramProjector.Project(ChildCare.Revision(), new($"view-{kind}", 1, kind, roots));
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
                [new(OutcomeId, new("Application submitted"), new("application-submitted"))], [], [],
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
