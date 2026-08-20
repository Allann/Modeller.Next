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
    /// <summary>Cancellation is observed between each definition walked while building nodes/edges
    /// — not just once on entry — so a caller can abort a projection already in progress against a
    /// large revision, not merely refuse to start one.</summary>
    public static ProjectionResult Project(AuthoredContextRevision revision, ViewDefinition view, LayoutState? layout = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(revision);
        ArgumentNullException.ThrowIfNull(view);
        cancellationToken.ThrowIfCancellationRequested();
        if (view.Version != 1)
            return new(null, [new("projection.view-version.unsupported", $"View version '{view.Version}' is not supported.")]);

        return view.Kind switch
        {
            ViewKind.Lifecycle => Lifecycle(revision, view, cancellationToken),
            ViewKind.RuleDecision => RuleDecision(revision, view, cancellationToken),
            ViewKind.Structural => Structural(revision, view, cancellationToken),
            ViewKind.BehaviourMap => BehaviourMap(revision, view, cancellationToken),
            ViewKind.CausalityAndEventFlow => CausalityAndEventFlow(revision, view, cancellationToken),
            ViewKind.ContextMap => ContextMap(revision, view, cancellationToken),
            _ => new(new(revision.Revision, view.Kind, [], []), [])
        };
    }

    private static ProjectionResult BehaviourMap(AuthoredContextRevision revision, ViewDefinition view, CancellationToken cancellationToken)
    {
        var entity = revision.Definitions.OfType<EntityDefinition>().FirstOrDefault(item => view.Roots.Contains(item.Id));
        if (entity is null)
            return new(null, [new("projection.root.invalid", "A behaviour map requires an entity root.")]);

        var nodes = ImmutableArray.CreateBuilder<ProjectionNode>();
        var edges = ImmutableArray.CreateBuilder<ProjectionEdge>();
        nodes.Add(new(ElementId("entity", entity.Id), "entity", entity.Name.Value, [entity.Id]));
        foreach (var behaviour in revision.Definitions.OfType<BehaviourDefinition>().Where(item => item.Entity.TargetId == entity.Id))
        {
            cancellationToken.ThrowIfCancellationRequested();
            nodes.Add(new(ElementId("behaviour", behaviour.Id), "behaviour", behaviour.Name.Value, [behaviour.Id]));
            edges.Add(new(ElementId("acts-on", behaviour.Id), "behaviour", "", ElementId("entity", entity.Id), ElementId("behaviour", behaviour.Id), [entity.Id, behaviour.Id]));
            foreach (var outcome in behaviour.Outcomes)
            {
                nodes.Add(new(ElementId("outcome", outcome.Id), "outcome", outcome.Name.Value, [outcome.Id]));
                edges.Add(new(ElementId("outcome", outcome.Id), "outcome", "", ElementId("behaviour", behaviour.Id), ElementId("outcome", outcome.Id), [behaviour.Id, outcome.Id]));
            }
            foreach (var effect in behaviour.Effects)
            {
                nodes.Add(new(ElementId("effect", effect.Id), "effect", effect.Name.Value, [effect.Id]));
                edges.Add(new(ElementId("effect", effect.Id), "effect", "", ElementId("behaviour", behaviour.Id), ElementId("effect", effect.Id), [behaviour.Id, effect.Id]));
            }
            foreach (var @event in behaviour.PublishedEvents)
            {
                nodes.Add(new(ElementId("event", @event.Id), "event", @event.Name.Value, [@event.Id]));
                edges.Add(new(ElementId("publishes", @event.Id), "publishes", "", ElementId("behaviour", behaviour.Id), ElementId("event", @event.Id), [behaviour.Id, @event.Id]));
            }
        }
        return new(new(revision.Revision, view.Kind, nodes.ToImmutable(), edges.ToImmutable()), []);
    }

    private static ProjectionResult CausalityAndEventFlow(AuthoredContextRevision revision, ViewDefinition view, CancellationToken cancellationToken)
    {
        if (!view.Roots.Contains(revision.Id))
            return new(null, [new("projection.root.invalid", "A causality and event-flow view requires a context root.")]);

        var nodes = ImmutableArray.CreateBuilder<ProjectionNode>();
        var edges = ImmutableArray.CreateBuilder<ProjectionEdge>();
        var seenBehaviours = new HashSet<SemanticId>();
        var seenEvents = new HashSet<SemanticId>();
        foreach (var behaviour in revision.Definitions.OfType<BehaviourDefinition>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var @event in behaviour.PublishedEvents)
            {
                if (seenBehaviours.Add(behaviour.Id))
                    nodes.Add(new(ElementId("behaviour", behaviour.Id), "behaviour", behaviour.Name.Value, [behaviour.Id]));
                if (seenEvents.Add(@event.Id))
                    nodes.Add(new(ElementId("event", @event.Id), "event", @event.Name.Value, [@event.Id]));
                edges.Add(new(ElementId("publishes", @event.Id), "publishes", "", ElementId("behaviour", behaviour.Id), ElementId("event", @event.Id), [behaviour.Id, @event.Id]));
            }
        }
        return new(new(revision.Revision, view.Kind, nodes.ToImmutable(), edges.ToImmutable()), []);
    }

    private static ProjectionResult ContextMap(AuthoredContextRevision revision, ViewDefinition view, CancellationToken cancellationToken)
    {
        if (!view.Roots.Contains(revision.Id))
            return new(null, [new("projection.root.invalid", "A context map requires a context root.")]);

        cancellationToken.ThrowIfCancellationRequested();
        ProjectionNode[] nodes = [new(ElementId("context", revision.Id), "context", revision.Name.Value, [revision.Id])];
        return new(new(revision.Revision, view.Kind, [.. nodes], []), []);
    }

    private static ProjectionResult Structural(AuthoredContextRevision revision, ViewDefinition view, CancellationToken cancellationToken)
    {
        if (!view.Roots.Contains(revision.Id))
            return new(null, [new("projection.root.invalid", "A structural view requires a context root.")]);

        var nodes = ImmutableArray.CreateBuilder<ProjectionNode>();
        var edges = ImmutableArray.CreateBuilder<ProjectionEdge>();
        foreach (var definition in revision.Definitions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (definition)
            {
                case EntityDefinition entity:
                    nodes.Add(new(ElementId("entity", entity.Id), "entity", entity.Name.Value, [entity.Id]));
                    foreach (var field in entity.Fields)
                    {
                        nodes.Add(new(ElementId("field", field.Id), "field", field.Name.Value, [field.Id]));
                        edges.Add(new(ElementId("owns", field.Id), "owns", "", ElementId("entity", entity.Id), ElementId("field", field.Id), [entity.Id, field.Id]));
                    }
                    foreach (var relationship in entity.Relationships)
                        edges.Add(new(ElementId("relationship", relationship.Id), "relationship", $"{relationship.Name.Value} ({relationship.Cardinality.ToString().ToLowerInvariant()})",
                            ElementId("entity", entity.Id), ElementId("entity", relationship.TargetId), [relationship.Id]));
                    break;
                case EnumerationDefinition enumeration:
                    nodes.Add(new(ElementId("enumeration", enumeration.Id), "enumeration", enumeration.Name.Value, [enumeration.Id]));
                    foreach (var member in enumeration.Members)
                    {
                        nodes.Add(new(ElementId("enumeration-member", member.Id), "enumeration-member", member.Name.Value, [member.Id]));
                        edges.Add(new(ElementId("owns", member.Id), "owns", "", ElementId("enumeration", enumeration.Id), ElementId("enumeration-member", member.Id), [enumeration.Id, member.Id]));
                    }
                    break;
            }
        }
        return new(new(revision.Revision, view.Kind, nodes.ToImmutable(), edges.ToImmutable()), []);
    }

    private static ProjectionResult RuleDecision(AuthoredContextRevision revision, ViewDefinition view, CancellationToken cancellationToken)
    {
        var rule = revision.Definitions.OfType<RuleDefinition>().FirstOrDefault(item => view.Roots.Contains(item.Id));
        if (rule is null)
            return new(new(revision.Revision, view.Kind, [], []), []);

        var facts = revision.Definitions.OfType<FactDefinition>().ToDictionary(item => item.Id);
        cancellationToken.ThrowIfCancellationRequested();
        var nodes = rule.InputFacts.Select(reference => facts.GetValueOrDefault(reference.TargetId))
            .Where(fact => fact is not null).Select(fact => new ProjectionNode(ElementId("fact", fact!.Id), "fact", fact.Name.Value, [fact.Id]))
            .Concat(rule.Conclusions.Select(conclusion => new ProjectionNode(ElementId("conclusion", conclusion.Id), "conclusion", conclusion.Name.Value, [conclusion.Id])))
            .ToImmutableArray();
        cancellationToken.ThrowIfCancellationRequested();
        var conclusion = rule.Conclusions.FirstOrDefault();
        var edges = conclusion is null ? [] : rule.InputFacts.Select(reference => new ProjectionEdge(
            $"input:{reference.TargetId}:{conclusion.Id}", "input", "", ElementId("fact", reference.TargetId), ElementId("conclusion", conclusion.Id), [reference.TargetId, conclusion.Id]))
            .ToImmutableArray();
        return new(new(revision.Revision, view.Kind, nodes, edges), []);
    }

    private static ProjectionResult Lifecycle(AuthoredContextRevision revision, ViewDefinition view, CancellationToken cancellationToken)
    {
        var entity = revision.Definitions.OfType<EntityDefinition>()
            .FirstOrDefault(item => view.Roots.Contains(item.Id));
        if (entity?.Lifecycle is null)
            return new(null, [new("projection.root.invalid", "A lifecycle view requires an entity root with a lifecycle.")]);

        var nodes = entity.Lifecycle.Stages
            .Select(stage => new ProjectionNode(ElementId("stage", stage.Id), "lifecycle-stage", stage.Name.Value, [stage.Id]))
            .ToImmutableArray();
        cancellationToken.ThrowIfCancellationRequested();
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
