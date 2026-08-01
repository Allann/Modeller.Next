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
}

public sealed record SemanticConceptAddress(
    SemanticId Id,
    SemanticName Name,
    SemanticSlug Slug,
    SemanticKind Kind,
    SemanticId OwnerId,
    string QualifiedName,
    IReadOnlyList<string> FormerQualifiedNames);

internal static class SemanticNavigation
{
    internal static IEnumerable<SemanticConceptAddress> Concepts(
        AuthoredContextRevision revision) =>
        Concepts(revision.Id, revision.Slug, revision.Definitions);

    internal static IEnumerable<SemanticConceptAddress> Concepts(
        SemanticId contextId,
        SemanticSlug contextSlug,
        IEnumerable<SemanticDefinition> definitions)
    {
        foreach (var definition in definitions)
        {
            var definitionName = $"{contextSlug}.{definition.Slug}";
            yield return Address(definition, contextId, definitionName);

            switch (definition)
            {
                case EntityDefinition entity:
                    var lifecycleName = $"{definitionName}.{entity.Lifecycle.Slug}";
                    yield return new SemanticConceptAddress(
                        entity.Lifecycle.Id,
                        entity.Lifecycle.Name,
                        entity.Lifecycle.Slug,
                        SemanticKind.Lifecycle,
                        entity.Id,
                        lifecycleName,
                        entity.Lifecycle.FormerQualifiedNames);
                    foreach (var stage in entity.Lifecycle.Stages)
                    {
                        yield return new SemanticConceptAddress(
                            stage.Id,
                            stage.Name,
                            stage.Slug,
                            SemanticKind.LifecycleStage,
                            entity.Lifecycle.Id,
                            $"{lifecycleName}.{stage.Slug}",
                            stage.FormerQualifiedNames);
                    }

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
                _ => throw new InvalidOperationException(
                    $"Unsupported semantic definition '{definition.GetType().Name}'.")
            },
            ownerId,
            qualifiedName,
            definition.FormerQualifiedNames);
}
