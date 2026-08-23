namespace Modeller.Model;

public enum SemanticKind
{
    Fact,
    Entity,
    Lifecycle,
    LifecycleStage,
    Rule,
    Decision,
    DecisionRow,
    Conclusion,
    Behaviour,
    Outcome,
    Effect,
    Event,
    Transition
    ,Field
    ,Relationship
    ,Enumeration
    ,EnumerationMember
}

public sealed record SemanticConceptAddress(
    SemanticId Id,
    SemanticName Name,
    SemanticSlug Slug,
    SemanticKind Kind,
    SemanticId OwnerId,
    string QualifiedName,
    IReadOnlyList<string> FormerQualifiedNames);

public static class SemanticNavigation
{
    public static IEnumerable<SemanticConceptAddress> Concepts(
        AuthoredContextRevision revision) =>
        Concepts(revision.Id, revision.Slug, revision.Definitions);

    public static IEnumerable<SemanticConceptAddress> Concepts(
        SemanticId contextId,
        SemanticSlug contextSlug,
        IEnumerable<SemanticDefinition> definitions)
    {
        foreach (var definition in definitions)
        {
            var definitionName = $"{contextSlug}.{definition.Slug}";
            // An entity's declared aggregate-root owner (another entity) takes precedence over the
            // default "owned by its context" address every other top-level definition gets — without
            // this, EntityDefinition.OwnerId is parsed and stored but never reaches the navigation
            // layer that Studio's outline/details panel and the API's /v1/workspace/analyze endpoint
            // both read from, so the aggregate-root fact would compile successfully yet be silently
            // invisible everywhere a person or client actually looks for it.
            var ownerId = definition is EntityDefinition { OwnerId: { } aggregateOwnerId } ? aggregateOwnerId : contextId;
            yield return Address(definition, ownerId, definitionName);

            switch (definition)
            {
                case EntityDefinition entity:
                    if (entity.Lifecycle is not null)
                    {
                        yield return new SemanticConceptAddress(
                            entity.Lifecycle.Id, entity.Lifecycle.Name, entity.Lifecycle.Slug, SemanticKind.Lifecycle,
                            entity.Id, $"{definitionName}.{entity.Lifecycle.Slug}", entity.Lifecycle.FormerQualifiedNames);
                        foreach (var stage in entity.Lifecycle.Stages)
                            yield return new(stage.Id, stage.Name, stage.Slug, SemanticKind.LifecycleStage, entity.Lifecycle.Id,
                                $"{definitionName}.{entity.Lifecycle.Slug}.{stage.Slug}", stage.FormerQualifiedNames);
                    }

                    foreach (var field in entity.Fields)
                        yield return new(field.Id, field.Name, field.Slug, SemanticKind.Field, entity.Id, $"{definitionName}.{field.Slug}", field.FormerQualifiedNames);
                    foreach (var relationship in entity.Relationships)
                        yield return new(relationship.Id, relationship.Name, relationship.Slug, SemanticKind.Relationship, entity.Id, $"{definitionName}.{relationship.Slug}", relationship.FormerQualifiedNames);

                    break;

                case EnumerationDefinition enumeration:
                    foreach (var member in enumeration.Members)
                        yield return new(member.Id, member.Name, member.Slug, SemanticKind.EnumerationMember, enumeration.Id, $"{definitionName}.{member.Slug}", member.FormerQualifiedNames);
                    break;

                case RuleDefinition rule:
                    foreach (var conclusion in rule.Conclusions)
                    {
                        yield return new SemanticConceptAddress(
                            conclusion.Id,
                            conclusion.Name,
                            conclusion.Slug,
                            SemanticKind.Conclusion,
                            rule.Id,
                            $"{definitionName}.{conclusion.Slug}",
                            conclusion.FormerQualifiedNames);
                    }

                    break;

                case DecisionDefinition decision:
                    foreach (var conclusion in decision.Conclusions)
                    {
                        yield return new SemanticConceptAddress(
                            conclusion.Id, conclusion.Name, conclusion.Slug, SemanticKind.Conclusion,
                            decision.Id, $"{definitionName}.{conclusion.Slug}", conclusion.FormerQualifiedNames);
                    }
                    foreach (var row in decision.Table.Rows)
                    {
                        yield return new SemanticConceptAddress(
                            row.Id, row.Name, row.Slug, SemanticKind.DecisionRow,
                            decision.Id, $"{definitionName}.{row.Slug}", row.FormerQualifiedNames);
                    }
                    break;

                case BehaviourDefinition behaviour:
                    foreach (var outcome in behaviour.Outcomes)
                    {
                        yield return new SemanticConceptAddress(
                            outcome.Id,
                            outcome.Name,
                            outcome.Slug,
                            SemanticKind.Outcome,
                            behaviour.Id,
                            $"{definitionName}.{outcome.Slug}",
                            outcome.FormerQualifiedNames);
                    }

                    foreach (var effect in behaviour.Effects)
                    {
                        yield return new SemanticConceptAddress(
                            effect.Id,
                            effect.Name,
                            effect.Slug,
                            SemanticKind.Effect,
                            behaviour.Id,
                            $"{definitionName}.{effect.Slug}",
                            effect.FormerQualifiedNames);
                    }

                    foreach (var publishedEvent in behaviour.PublishedEvents)
                    {
                        yield return new SemanticConceptAddress(
                            publishedEvent.Id,
                            publishedEvent.Name,
                            publishedEvent.Slug,
                            SemanticKind.Event,
                            behaviour.Id,
                            $"{definitionName}.{publishedEvent.Slug}",
                            publishedEvent.FormerQualifiedNames);
                    }

                    foreach (var transition in behaviour.Transitions)
                    {
                        yield return new SemanticConceptAddress(
                            transition.Id,
                            transition.Name,
                            transition.Slug,
                            SemanticKind.Transition,
                            behaviour.Id,
                            $"{definitionName}.{transition.Slug}",
                            transition.FormerQualifiedNames);
                    }

                    break;
            }
        }
    }

    private static SemanticConceptAddress Address(
        SemanticDefinition definition,
        SemanticId ownerId,
        string qualifiedName) => new(
            definition.Id,
            definition.Name,
            definition.Slug,
            definition switch
            {
                FactDefinition => SemanticKind.Fact,
                EntityDefinition => SemanticKind.Entity,
                RuleDefinition => SemanticKind.Rule,
                DecisionDefinition => SemanticKind.Decision,
                BehaviourDefinition => SemanticKind.Behaviour,
                EnumerationDefinition => SemanticKind.Enumeration,
                _ => throw new InvalidOperationException(
                    $"Unsupported semantic definition '{definition.GetType().Name}'.")
            },
            ownerId,
            qualifiedName,
            definition.FormerQualifiedNames);
}
