using System.Collections.Immutable;
using Modeller.Model;

namespace Modeller.Projections;

public enum ViewKind { BehaviourMap, Lifecycle, CausalityAndEventFlow, ContextMap, Structural, RuleDecision }

public sealed record ViewDefinition(
    string Id,
    int Version,
    ViewKind Kind,
    ImmutableArray<SemanticId> Roots,
    ImmutableHashSet<SemanticId>? Inclusions = null,
    ImmutableHashSet<SemanticId>? Exclusions = null);

public sealed record ElementLayout(double X, double Y);
public sealed record LayoutState(ImmutableDictionary<string, ElementLayout> Elements);

public sealed record ProjectionNode(string Id, string Role, string Label, ImmutableArray<SemanticId> SemanticIds);
public sealed record ProjectionEdge(string Id, string Role, string Label, string SourceId, string TargetId, ImmutableArray<SemanticId> SemanticIds);
public sealed record ProjectionGraph(long SourceRevision, ViewKind Kind, ImmutableArray<ProjectionNode> Nodes, ImmutableArray<ProjectionEdge> Edges);
public sealed record ProjectionDiagnostic(string Code, string Message, SemanticId? SubjectId = null);
public sealed record ProjectionResult(ProjectionGraph? Graph, ImmutableArray<ProjectionDiagnostic> Diagnostics)
{
    public bool Succeeded => Graph is not null && Diagnostics.IsEmpty;
}

public static class DiagramProjector
{
    public static ProjectionResult Project(AuthoredContextRevision revision, ViewDefinition view, LayoutState? layout = null)
    {
        ArgumentNullException.ThrowIfNull(revision);
        ArgumentNullException.ThrowIfNull(view);
        if (view.Version != 1)
            return new(null, [new("projection.view-version.unsupported", $"View version '{view.Version}' is not supported.")]);

        return view.Kind switch
        {
            ViewKind.Lifecycle => Lifecycle(revision, view),
            ViewKind.RuleDecision => RuleDecision(revision, view),
            _ => new(new(revision.Revision, view.Kind, [], []), [])
        };
    }

    private static ProjectionResult RuleDecision(AuthoredContextRevision revision, ViewDefinition view)
    {
        var rule = revision.Definitions.OfType<RuleDefinition>().FirstOrDefault(item => view.Roots.Contains(item.Id));
        if (rule is null)
            return new(new(revision.Revision, view.Kind, [], []), []);

        var facts = revision.Definitions.OfType<FactDefinition>().ToDictionary(item => item.Id);
        var nodes = rule.InputFacts.Select(reference => facts.GetValueOrDefault(reference.TargetId))
            .Where(fact => fact is not null).Select(fact => new ProjectionNode(ElementId("fact", fact!.Id), "fact", fact.Name.Value, [fact.Id]))
            .Concat(rule.Conclusions.Select(conclusion => new ProjectionNode(ElementId("conclusion", conclusion.Id), "conclusion", conclusion.Name.Value, [conclusion.Id])))
            .ToImmutableArray();
        var conclusion = rule.Conclusions.FirstOrDefault();
        var edges = conclusion is null ? [] : rule.InputFacts.Select(reference => new ProjectionEdge(
            $"input:{reference.TargetId}:{conclusion.Id}", "input", "", ElementId("fact", reference.TargetId), ElementId("conclusion", conclusion.Id), [reference.TargetId, conclusion.Id]))
            .ToImmutableArray();
        return new(new(revision.Revision, view.Kind, nodes, edges), []);
    }

    private static ProjectionResult Lifecycle(AuthoredContextRevision revision, ViewDefinition view)
    {
        var entity = revision.Definitions.OfType<EntityDefinition>()
            .FirstOrDefault(item => view.Roots.Contains(item.Id));
        if (entity is null)
            return new(null, [new("projection.root.invalid", "A lifecycle view requires an entity root.")]);

        var nodes = entity.Lifecycle.Stages
            .Select(stage => new ProjectionNode(ElementId("stage", stage.Id), "lifecycle-stage", stage.Name.Value, [stage.Id]))
            .ToImmutableArray();
        var edges = revision.Definitions.OfType<BehaviourDefinition>()
            .SelectMany(behaviour => behaviour.Transitions)
            .Where(transition => transition.Lifecycle.TargetId == entity.Lifecycle.Id)
            .Select(transition => new ProjectionEdge(ElementId("transition", transition.Id), "transition", transition.Name.Value,
                ElementId("stage", transition.SourceStage.TargetId), ElementId("stage", transition.TargetStage.TargetId), [transition.Id]))
            .OrderBy(edge => edge.Id, StringComparer.Ordinal)
            .ToImmutableArray();
        return new(new(revision.Revision, view.Kind, nodes, edges), []);
    }

    private static string ElementId(string role, SemanticId id) => $"{role}:{id}";
}

public abstract record EditRequest;
public sealed record MoveElement(string ElementId, double X, double Y) : EditRequest;
public sealed record RemoveFromView(SemanticId SemanticId) : EditRequest;
public sealed record HighlightEvaluation(ImmutableArray<SemanticId> SemanticIds) : EditRequest;
public sealed record DeleteFromModel(SemanticId SemanticId, long ExpectedRevision) : EditRequest;
public sealed record SpatialRelationshipGesture(string SourceElementId, string TargetElementId) : EditRequest;

public abstract record EditTranslation;
public sealed record SemanticEdit(ModelOperation Operation, long ExpectedRevision) : EditTranslation;
public sealed record ViewEdit(SemanticId ExcludedSemanticId) : EditTranslation;
public sealed record LayoutEdit(string ElementId, ElementLayout Layout) : EditTranslation;
public sealed record SessionEdit(ImmutableArray<SemanticId> HighlightedSemanticIds) : EditTranslation;
public sealed record InvalidEdit(ImmutableArray<ProjectionDiagnostic> Diagnostics, bool ReprojectRequired = false) : EditTranslation;

public static class ProjectionEditor
{
    public static EditTranslation Translate(AuthoredContextRevision revision, EditRequest request) => request switch
    {
        MoveElement move => new LayoutEdit(move.ElementId, new(move.X, move.Y)),
        RemoveFromView remove => new ViewEdit(remove.SemanticId),
        HighlightEvaluation highlight => new SessionEdit(highlight.SemanticIds),
        DeleteFromModel delete when delete.ExpectedRevision != revision.Revision => new InvalidEdit(
            [new("projection.edit.stale-revision", "The semantic revision changed; reproject before editing.", delete.SemanticId)], true),
        DeleteFromModel delete => new SemanticEdit(new DeleteConcept(delete.SemanticId), delete.ExpectedRevision),
        SpatialRelationshipGesture => new InvalidEdit([new("projection.edit.spatial-gesture-nonsemantic", "Spatial proximity cannot create a semantic relationship.")]),
        _ => new InvalidEdit([new("projection.edit.unsupported", "The edit request is not supported.")])
    };
}
