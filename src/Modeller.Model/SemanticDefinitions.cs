using System.Collections.Immutable;

namespace Modeller.Model;

public interface ISemanticConcept
{
    SemanticId Id { get; }
    SemanticName Name { get; }
    SemanticSlug Slug { get; }
    ImmutableArray<string> FormerQualifiedNames { get; }
    SemanticDocumentation? Documentation { get; }
}

public abstract record SemanticDefinition(
    SemanticId Id,
    SemanticName Name,
    SemanticSlug Slug,
    SemanticDocumentation? Documentation = null) : ISemanticConcept
{
    public ImmutableArray<string> FormerQualifiedNames { get; init; } = [];
}

public sealed record FactDefinition(
    SemanticId Id,
    SemanticName Name,
    SemanticSlug Slug,
    FactType Type,
    SemanticDocumentation? Documentation = null)
    : SemanticDefinition(Id, Name, Slug, Documentation);

public enum FactType
{
    Truth,
    Text,
    Number,
    Date
}

public sealed record EntityDefinition(
    SemanticId Id,
    SemanticName Name,
    SemanticSlug Slug,
    LifecycleDefinition Lifecycle,
    SemanticDocumentation? Documentation = null)
    : SemanticDefinition(Id, Name, Slug, Documentation);

public sealed record LifecycleDefinition(
    SemanticId Id,
    SemanticName Name,
    SemanticSlug Slug,
    ImmutableArray<LifecycleStage> Stages,
    SemanticDocumentation? Documentation = null) : ISemanticConcept
{
    public ImmutableArray<string> FormerQualifiedNames { get; init; } = [];
}

public sealed record LifecycleStage(
    SemanticId Id,
    SemanticName Name,
    SemanticSlug Slug,
    SemanticDocumentation? Documentation = null) : ISemanticConcept
{
    public ImmutableArray<string> FormerQualifiedNames { get; init; } = [];
}

public sealed record RuleDefinition(
    SemanticId Id,
    SemanticName Name,
    SemanticSlug Slug,
    ImmutableArray<FactReference> InputFacts,
    ImmutableArray<ConclusionDefinition> Conclusions,
    SemanticDocumentation? Documentation = null)
    : SemanticDefinition(Id, Name, Slug, Documentation);

public sealed record ConclusionDefinition(
    SemanticId Id,
    SemanticName Name,
    SemanticSlug Slug,
    SemanticDocumentation? Documentation = null) : ISemanticConcept
{
    public ImmutableArray<string> FormerQualifiedNames { get; init; } = [];
}

public sealed record BehaviourDefinition(
    SemanticId Id,
    SemanticName Name,
    SemanticSlug Slug,
    EntityReference Entity,
    ImmutableArray<OutcomeDefinition> Outcomes,
    ImmutableArray<EffectDefinition> Effects,
    ImmutableArray<EventDefinition> PublishedEvents,
    ImmutableArray<TransitionDefinition> Transitions,
    ImmutableArray<RuleBinding> RuleBindings,
    SemanticDocumentation? Documentation = null)
    : SemanticDefinition(Id, Name, Slug, Documentation);

public sealed record OutcomeDefinition(
    SemanticId Id,
    SemanticName Name,
    SemanticSlug Slug,
    SemanticDocumentation? Documentation = null) : ISemanticConcept
{
    public ImmutableArray<string> FormerQualifiedNames { get; init; } = [];
}

public sealed record EffectDefinition(
    SemanticId Id,
    SemanticName Name,
    SemanticSlug Slug,
    SemanticDocumentation? Documentation = null) : ISemanticConcept
{
    public ImmutableArray<string> FormerQualifiedNames { get; init; } = [];
}

public sealed record EventDefinition(
    SemanticId Id,
    SemanticName Name,
    SemanticSlug Slug,
    SemanticDocumentation? Documentation = null) : ISemanticConcept
{
    public ImmutableArray<string> FormerQualifiedNames { get; init; } = [];
}

public sealed record TransitionDefinition(
    SemanticId Id,
    SemanticName Name,
    SemanticSlug Slug,
    LifecycleReference Lifecycle,
    LifecycleStageReference SourceStage,
    LifecycleStageReference TargetStage,
    OutcomeReference Outcome,
    RuleReference? GuardRule = null,
    SemanticDocumentation? Documentation = null) : ISemanticConcept
{
    public ImmutableArray<string> FormerQualifiedNames { get; init; } = [];
}

public sealed record RuleBinding(
    RuleReference Rule,
    RuleBindingPurpose Purpose,
    ImmutableDictionary<FactReference, FactReference> FactBindings);

public enum RuleBindingPurpose
{
    Requirement,
    Authorization,
    Invariant,
    TransitionGuard,
    Classification,
    Outcome
}
