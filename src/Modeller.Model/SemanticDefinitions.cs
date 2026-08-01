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
    RuleExpression? Expression = null,
    SemanticDocumentation? Documentation = null)
    : SemanticDefinition(Id, Name, Slug, Documentation);

public abstract record RuleExpression;

public sealed record FactExpression(
    FactReference Fact,
    string? TrueFindingCode = null,
    string? FalseFindingCode = null,
    string? MissingFindingCode = null) : RuleExpression;

public sealed record AndExpression(ImmutableArray<RuleExpression> Operands) : RuleExpression;

public sealed record DecisionDefinition(
    SemanticId Id,
    SemanticName Name,
    SemanticSlug Slug,
    ImmutableArray<FactReference> InputFacts,
    ImmutableArray<ConclusionDefinition> Conclusions,
    DecisionTable Table,
    SemanticDocumentation? Documentation = null)
    : SemanticDefinition(Id, Name, Slug, Documentation);

public sealed record DecisionTable(
    DecisionHitPolicy HitPolicy,
    ImmutableArray<DecisionRow> Rows);

public enum DecisionHitPolicy
{
    Unique
}

public sealed record DecisionRow(
    SemanticId Id,
    SemanticName Name,
    SemanticSlug Slug,
    ImmutableArray<TruthDecisionCondition> Conditions,
    ConclusionReference Conclusion,
    string FindingCode,
    SemanticDocumentation? Documentation = null) : ISemanticConcept
{
    public ImmutableArray<string> FormerQualifiedNames { get; init; } = [];
}

public sealed record TruthDecisionCondition(
    FactReference Fact,
    bool? Expected,
    string? MissingFindingCode = null);

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
