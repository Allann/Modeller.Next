using System.Collections.Immutable;
using Modeller.Contexts;
using Modeller.Model;

namespace Modeller.Rendering;

/// <summary>Language-neutral canonical traversal shared by every renderer's globals provider.</summary>
public sealed record ProjectedField(SemanticId FieldId, string RawName, DataType Type, bool IsOptional);
public sealed record ProjectedRelationship(SemanticId RelationshipId, string RawName, SemanticId TargetId, RelationshipCardinality Cardinality, bool IsOptional);
public sealed record ProjectedEntity(EntityDefinition Source, ImmutableArray<ProjectedField> Fields, ImmutableArray<ProjectedRelationship> Relationships);

public sealed record ProjectedEnumerationMember(string RawName, int Value);
public sealed record ProjectedEnumeration(EnumerationDefinition Source, ImmutableArray<ProjectedEnumerationMember> Members);

public sealed record ProjectedFact(FactDefinition Source);
public sealed record ProjectedConclusion(string RawName);

public abstract record ProjectedRuleExpression;
public sealed record ProjectedFactTerm(ProjectedFact Fact) : ProjectedRuleExpression;
public sealed record ProjectedAndExpression(ImmutableArray<ProjectedRuleExpression> Operands) : ProjectedRuleExpression;

public sealed record ProjectedRule(RuleDefinition Source, ImmutableArray<ProjectedFact> Facts, ProjectedRuleExpression Expression,
    ImmutableArray<ProjectedConclusion> Conclusions);

public sealed record ProjectedRuleBinding(RuleBinding Source, RuleDefinition Rule);

/// <summary>
/// A transition's own resolved guard: its <see cref="TransitionDefinition.GuardRule"/> (if any) followed by every
/// behaviour-level <see cref="RuleBinding"/> purposefully marked <see cref="RuleBindingPurpose.TransitionGuard"/> —
/// falling back to every binding on the behaviour when none are marked, preserving the historical "the one
/// binding is the guard" behaviour for packs that don't yet distinguish binding purposes. May be empty for a
/// transition that is unconditional or whose behaviour declares no rule at all.
/// </summary>
public sealed record ProjectedTransition(TransitionDefinition Source, LifecycleStage SourceStage, LifecycleStage TargetStage,
    ImmutableArray<RuleDefinition> GuardRules);

public sealed record ProjectedBehaviour(
    BehaviourDefinition Source,
    EntityDefinition Entity,
    LifecycleDefinition Lifecycle,
    ImmutableArray<ProjectedTransition> Transitions,
    ImmutableArray<ProjectedRuleBinding> RuleBindings)
{
    /// <summary>
    /// The rule a renderer should use to name a behaviour-wide Facts type: the first rule guarding any
    /// transition, else the first rule bound to the behaviour at all, else <c>null</c> if the behaviour — validly,
    /// per the canonical model — references no rule whatsoever (e.g. a zero-transition, unguarded behaviour).
    /// Renderers decide how to handle a <c>null</c> result; the projection never guesses on their behalf.
    /// </summary>
    public RuleDefinition? PrimaryRule => Transitions.SelectMany(transition => transition.GuardRules)
        .Concat(RuleBindings.Select(binding => binding.Rule))
        .FirstOrDefault();
}

/// <summary>
/// Owns canonical traversal over an <see cref="AuthoredContextRevision"/>: definition lookup, nullability rules,
/// multi-transition/multi-binding behaviour handling (including transitions with zero resolvable guard rules) and
/// <see cref="RuleExpression"/> tree walking. Naming, import formatting and data-type spelling remain the
/// responsibility of each language-specific globals provider.
/// </summary>
public sealed class TemplateSemanticProjection(AuthoredContextRevision revision)
{
    private readonly IReadOnlyDictionary<SemanticId, SemanticDefinition> definitions =
        revision.Definitions.ToDictionary(definition => definition.Id);

    public ProjectedEntity Entity(EntityDefinition entity) => new(
        entity,
        entity.Fields.Select(field => new ProjectedField(field.Id, field.Name.Value, field.Type,
            field.IsOptional && field.Type is not StringDataType)).ToImmutableArray(),
        entity.Relationships.Select(relationship => new ProjectedRelationship(relationship.Id, relationship.Name.Value, relationship.TargetId,
            relationship.Cardinality, relationship.Cardinality == RelationshipCardinality.One && relationship.IsOptional)).ToImmutableArray());

    public ProjectedEnumeration Enumeration(EnumerationDefinition enumeration) => new(
        enumeration,
        enumeration.Members.OrderBy(member => member.Value)
            .Select(member => new ProjectedEnumerationMember(member.Name.Value, member.Value)).ToImmutableArray());

    public ProjectedRule Rule(RuleDefinition rule)
    {
        var facts = rule.InputFacts.Select(reference => new ProjectedFact((FactDefinition)definitions[reference.TargetId])).ToImmutableArray();
        var expression = rule.Expression is null
            ? new ProjectedAndExpression(facts.Select(fact => (ProjectedRuleExpression)new ProjectedFactTerm(fact)).ToImmutableArray())
            : WalkExpression(rule.Expression);
        var conclusions = rule.Conclusions.Select(conclusion => new ProjectedConclusion(conclusion.Name.Value)).ToImmutableArray();
        return new(rule, facts, expression, conclusions);
    }

    public ProjectedBehaviour Behaviour(BehaviourDefinition behaviour)
    {
        var entity = (EntityDefinition)definitions[behaviour.Entity.TargetId];
        var lifecycle = entity.Lifecycle ?? throw new InvalidOperationException("A generated behaviour requires an entity lifecycle.");
        var bindings = behaviour.RuleBindings.Select(binding =>
            new ProjectedRuleBinding(binding, (RuleDefinition)definitions[binding.Rule.TargetId])).ToImmutableArray();

        var fallbackGuardRules = bindings.Where(binding => binding.Source.Purpose == RuleBindingPurpose.TransitionGuard)
            .Select(binding => binding.Rule).ToImmutableArray();
        if (fallbackGuardRules.Length == 0) fallbackGuardRules = [.. bindings.Select(binding => binding.Rule)];

        var transitions = behaviour.Transitions.Select(transition =>
        {
            var guardRule = transition.GuardRule is null ? null : (RuleDefinition)definitions[transition.GuardRule.Value.TargetId];
            var guardRules = guardRule is null ? fallbackGuardRules : fallbackGuardRules.Insert(0, guardRule);
            return new ProjectedTransition(
                transition,
                lifecycle.Stages.Single(stage => stage.Id == transition.SourceStage.TargetId),
                lifecycle.Stages.Single(stage => stage.Id == transition.TargetStage.TargetId),
                guardRules);
        }).ToImmutableArray();

        return new(behaviour, entity, lifecycle, transitions, bindings);
    }

    private ProjectedRuleExpression WalkExpression(RuleExpression expression) => expression switch
    {
        FactExpression fact => new ProjectedFactTerm(new ProjectedFact((FactDefinition)definitions[fact.Fact.TargetId])),
        AndExpression and => new ProjectedAndExpression(and.Operands.Select(WalkExpression).ToImmutableArray()),
        _ => throw new NotSupportedException($"'{expression.GetType().Name}' is not a supported canonical rule expression.")
    };
}
