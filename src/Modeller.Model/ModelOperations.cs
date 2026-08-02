using System.Collections.Immutable;

namespace Modeller.Model;

public abstract record ModelOperation;

public sealed record AddDefinition(SemanticDefinition Definition) : ModelOperation;

public sealed record RenameConcept(
    SemanticId ConceptId,
    SemanticName Name,
    SemanticSlug Slug) : ModelOperation;

public sealed record DeleteConcept(SemanticId ConceptId) : ModelOperation;

public sealed record ModelDiagnostic(
    string Code,
    string Message,
    SemanticId? SubjectId = null);

public sealed record ModelOperationResult(
    AuthoredContextRevision Revision,
    ImmutableArray<ModelDiagnostic> Diagnostics)
{
    public bool Succeeded => Diagnostics.IsEmpty;
}

public static class CanonicalModel
{
    public static ModelOperationResult Apply(
        AuthoredContextRevision revision,
        params ReadOnlySpan<ModelOperation> operations)
    {
        ArgumentNullException.ThrowIfNull(revision);

        var definitions = revision.Definitions;
        foreach (var operation in operations)
        {
            var result = Apply(revision.Id, revision.Slug, definitions, operation);
            if (result.Diagnostic is not null)
            {
                return new ModelOperationResult(revision, [result.Diagnostic]);
            }

            definitions = result.Definitions;
        }

        return new ModelOperationResult(
            operations.IsEmpty ? revision : revision.Next(definitions),
            []);
    }

    private static ApplyResult Apply(
        SemanticId contextId,
        SemanticSlug contextSlug,
        ImmutableArray<SemanticDefinition> definitions,
        ModelOperation operation) => operation switch
        {
            AddDefinition add => Add(contextId, definitions, add.Definition),
            RenameConcept rename => Rename(contextId, contextSlug, definitions, rename),
            DeleteConcept delete => Delete(definitions, delete),
            null => Failure(definitions, "model.operation.required", "A model operation is required."),
            _ => Failure(definitions, "model.operation.unsupported", $"Unsupported model operation '{operation.GetType().Name}'.")
        };

    private static ApplyResult Delete(ImmutableArray<SemanticDefinition> definitions, DeleteConcept operation)
    {
        var definition = definitions.FirstOrDefault(item => item.Id == operation.ConceptId);
        if (definition is null)
            return Failure(definitions, "model.concept.not-found", $"Top-level semantic concept '{operation.ConceptId}' does not exist.", operation.ConceptId);

        if (definitions.Where(item => item.Id != operation.ConceptId).Any(item => References(item, operation.ConceptId)))
            return Failure(definitions, "model.delete.referenced", $"Semantic concept '{operation.ConceptId}' is still referenced.", operation.ConceptId);

        return new ApplyResult(definitions.Remove(definition), null);
    }

    private static bool References(SemanticDefinition definition, SemanticId id) => definition switch
    {
        EntityDefinition entity => entity.Fields.Any(field => field.Type switch
        {
            EnumerationDataType named => named.EnumerationId == id,
            EntityReferenceDataType named => named.EntityId == id,
            ValueTypeReferenceDataType named => named.ValueTypeId == id,
            _ => false
        }) || entity.Relationships.Any(relationship => relationship.TargetId == id),
        RuleDefinition rule => rule.InputFacts.Any(reference => reference.TargetId == id),
        DecisionDefinition decision => decision.InputFacts.Any(reference => reference.TargetId == id) ||
            decision.Table.Rows.Any(row => row.Conclusion.TargetId == id || row.Conditions.Any(condition => condition.Fact.TargetId == id)),
        BehaviourDefinition behaviour => behaviour.Entity.TargetId == id ||
            behaviour.Transitions.Any(transition => transition.Lifecycle.TargetId == id || transition.SourceStage.TargetId == id ||
                transition.TargetStage.TargetId == id || transition.Outcome.TargetId == id || transition.GuardRule?.TargetId == id) ||
            behaviour.RuleBindings.Any(binding => binding.Rule.TargetId == id || binding.FactBindings.Any(pair => pair.Key.TargetId == id || pair.Value.TargetId == id)),
        _ => false
    };

    private static ApplyResult Add(
        SemanticId contextId,
        ImmutableArray<SemanticDefinition> definitions,
        SemanticDefinition definition)
    {
        definition = Normalize(definition);

        if (definition is not FactDefinition and
            not EntityDefinition and
            not EnumerationDefinition and
            not RuleDefinition and
            not DecisionDefinition and
            not BehaviourDefinition)
        {
            return Failure(
                definitions,
                "model.definition.unsupported",
                $"Unsupported semantic definition '{definition.GetType().Name}'.",
                definition.Id);
        }

        var existingNodes = definitions.SelectMany(existing => Nodes(existing, contextId));
        var candidateNodes = Nodes(definition, contextId).ToArray();
        var contextIdentityCollision = candidateNodes.FirstOrDefault(node => node.Id == contextId);
        if (contextIdentityCollision is not null)
        {
            return Failure(
                definitions,
                "model.identity.duplicate",
                $"Semantic identity '{contextId}' is already owned by this bounded context.",
                contextId);
        }

        var duplicateIdentity = existingNodes
            .Concat(candidateNodes)
            .GroupBy(node => node.Id)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateIdentity is not null)
        {
            return Failure(
                definitions,
                "model.identity.duplicate",
                $"Semantic identity '{duplicateIdentity.Key}' is already owned by this bounded context.",
                duplicateIdentity.Key);
        }

        var duplicateSlug = definitions
            .SelectMany(existing => Nodes(existing, contextId))
            .Concat(candidateNodes)
            .GroupBy(node => (node.OwnerId, node.Slug))
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateSlug is not null)
        {
            return Failure(
                definitions,
                "model.slug.duplicate",
                $"Semantic slug '{duplicateSlug.Key.Slug}' is already used by a sibling definition.",
                duplicateSlug.Last().Id);
        }

        return new ApplyResult(definitions.Add(definition), null);
    }

    private static SemanticDefinition Normalize(SemanticDefinition definition) => definition switch
    {
        FactDefinition fact => fact with
        {
            FormerQualifiedNames = OrEmpty(fact.FormerQualifiedNames)
        },
        EntityDefinition entity => entity with
        {
            FormerQualifiedNames = OrEmpty(entity.FormerQualifiedNames),
            Fields = OrEmpty(entity.Fields).Select(field => field with { FormerQualifiedNames = OrEmpty(field.FormerQualifiedNames) }).ToImmutableArray(),
            Relationships = OrEmpty(entity.Relationships).Select(relationship => relationship with { FormerQualifiedNames = OrEmpty(relationship.FormerQualifiedNames) }).ToImmutableArray(),
            Lifecycle = entity.Lifecycle is null ? null : entity.Lifecycle with
            {
                FormerQualifiedNames = OrEmpty(entity.Lifecycle.FormerQualifiedNames),
                Stages = OrEmpty(entity.Lifecycle.Stages)
                    .Select(stage => stage with
                    {
                        FormerQualifiedNames = OrEmpty(stage.FormerQualifiedNames)
                    })
                    .ToImmutableArray()
            }
        },
        EnumerationDefinition enumeration => enumeration with
        {
            FormerQualifiedNames = OrEmpty(enumeration.FormerQualifiedNames),
            Members = OrEmpty(enumeration.Members).Select(member => member with { FormerQualifiedNames = OrEmpty(member.FormerQualifiedNames) }).ToImmutableArray()
        },
        RuleDefinition rule => rule with
        {
            FormerQualifiedNames = OrEmpty(rule.FormerQualifiedNames),
            InputFacts = OrEmpty(rule.InputFacts),
            Conclusions = OrEmpty(rule.Conclusions)
                .Select(conclusion => conclusion with
                {
                    FormerQualifiedNames = OrEmpty(conclusion.FormerQualifiedNames)
                })
                .ToImmutableArray()
        },
        DecisionDefinition decision => decision with
        {
            FormerQualifiedNames = OrEmpty(decision.FormerQualifiedNames),
            InputFacts = OrEmpty(decision.InputFacts),
            Conclusions = OrEmpty(decision.Conclusions).Select(conclusion => conclusion with
            {
                FormerQualifiedNames = OrEmpty(conclusion.FormerQualifiedNames)
            }).ToImmutableArray(),
            Table = decision.Table with
            {
                Rows = OrEmpty(decision.Table.Rows).Select(row => row with
                {
                    FormerQualifiedNames = OrEmpty(row.FormerQualifiedNames),
                    Conditions = OrEmpty(row.Conditions)
                }).ToImmutableArray()
            }
        },
        BehaviourDefinition behaviour => behaviour with
        {
            FormerQualifiedNames = OrEmpty(behaviour.FormerQualifiedNames),
            Outcomes = OrEmpty(behaviour.Outcomes)
                .Select(outcome => outcome with { FormerQualifiedNames = OrEmpty(outcome.FormerQualifiedNames) })
                .ToImmutableArray(),
            Effects = OrEmpty(behaviour.Effects)
                .Select(effect => effect with { FormerQualifiedNames = OrEmpty(effect.FormerQualifiedNames) })
                .ToImmutableArray(),
            PublishedEvents = OrEmpty(behaviour.PublishedEvents)
                .Select(@event => @event with { FormerQualifiedNames = OrEmpty(@event.FormerQualifiedNames) })
                .ToImmutableArray(),
            Transitions = OrEmpty(behaviour.Transitions)
                .Select(transition => transition with { FormerQualifiedNames = OrEmpty(transition.FormerQualifiedNames) })
                .ToImmutableArray(),
            RuleBindings = OrEmpty(behaviour.RuleBindings)
        },
        _ => definition
    };

    private static ImmutableArray<T> OrEmpty<T>(ImmutableArray<T> items) =>
        items.IsDefault ? [] : items;

    private static ApplyResult Rename(
        SemanticId contextId,
        SemanticSlug contextSlug,
        ImmutableArray<SemanticDefinition> definitions,
        RenameConcept operation)
    {
        var concepts = SemanticNavigation
            .Concepts(contextId, contextSlug, definitions)
            .ToArray();
        var current = concepts.FirstOrDefault(concept => concept.Id == operation.ConceptId);
        if (current is null)
        {
            return Failure(
                definitions,
                "model.concept.not-found",
                $"Semantic concept '{operation.ConceptId}' does not exist.",
                operation.ConceptId);
        }

        if (concepts.Any(concept =>
                concept.Id != operation.ConceptId &&
                concept.OwnerId == current.OwnerId &&
                concept.Slug == operation.Slug))
        {
            return Failure(
                definitions,
                "model.slug.duplicate",
                $"Semantic slug '{operation.Slug}' is already used by a sibling definition.",
                operation.ConceptId);
        }

        var renamed = definitions.Select(definition =>
            Rename(definition, operation, current.QualifiedName)).ToImmutableArray();
        return new ApplyResult(renamed, null);
    }

    private static SemanticDefinition Rename(
        SemanticDefinition definition,
        RenameConcept operation,
        string formerQualifiedName)
    {
        if (definition.Id == operation.ConceptId)
        {
            return WithName(definition, operation, formerQualifiedName);
        }

        return definition switch
        {
            EntityDefinition entity => entity with
            {
                Lifecycle = entity.Lifecycle is null ? null : Rename(entity.Lifecycle, operation, formerQualifiedName),
                Fields = entity.Fields.Select(field => field.Id == operation.ConceptId ? field with { Name = operation.Name, Slug = operation.Slug, FormerQualifiedNames = Aliases(field, operation.Slug, formerQualifiedName) } : field).ToImmutableArray(),
                Relationships = entity.Relationships.Select(relationship => relationship.Id == operation.ConceptId ? relationship with { Name = operation.Name, Slug = operation.Slug, FormerQualifiedNames = Aliases(relationship, operation.Slug, formerQualifiedName) } : relationship).ToImmutableArray()
            },
            EnumerationDefinition enumeration => enumeration with { Members = enumeration.Members.Select(member => member.Id == operation.ConceptId ? member with { Name = operation.Name, Slug = operation.Slug, FormerQualifiedNames = Aliases(member, operation.Slug, formerQualifiedName) } : member).ToImmutableArray() },
            RuleDefinition rule => rule with
            {
                Conclusions = rule.Conclusions
                    .Select(conclusion => Rename(conclusion, operation, formerQualifiedName))
                    .ToImmutableArray()
            },
            DecisionDefinition decision => decision with
            {
                Conclusions = decision.Conclusions.Select(conclusion => Rename(conclusion, operation, formerQualifiedName)).ToImmutableArray(),
                Table = decision.Table with
                {
                    Rows = decision.Table.Rows.Select(row => Rename(row, operation, formerQualifiedName)).ToImmutableArray()
                }
            },
            BehaviourDefinition behaviour => behaviour with
            {
                Outcomes = behaviour.Outcomes.Select(outcome => Rename(outcome, operation, formerQualifiedName)).ToImmutableArray(),
                Effects = behaviour.Effects.Select(effect => Rename(effect, operation, formerQualifiedName)).ToImmutableArray(),
                PublishedEvents = behaviour.PublishedEvents.Select(@event => Rename(@event, operation, formerQualifiedName)).ToImmutableArray(),
                Transitions = behaviour.Transitions.Select(transition => Rename(transition, operation, formerQualifiedName)).ToImmutableArray()
            },
            _ => definition
        };
    }

    private static SemanticDefinition WithName(
        SemanticDefinition definition,
        RenameConcept operation,
        string formerQualifiedName)
    {
        var formerNames = Aliases(definition, operation.Slug, formerQualifiedName);
        return definition with
        {
            Name = operation.Name,
            Slug = operation.Slug,
            FormerQualifiedNames = formerNames
        };
    }

    private static LifecycleDefinition Rename(LifecycleDefinition lifecycle, RenameConcept operation, string formerName)
    {
        if (lifecycle.Id == operation.ConceptId)
        {
            return lifecycle with
            {
                Name = operation.Name,
                Slug = operation.Slug,
                FormerQualifiedNames = Aliases(lifecycle, operation.Slug, formerName)
            };
        }

        return lifecycle with
        {
            Stages = lifecycle.Stages.Select(stage => Rename(stage, operation, formerName)).ToImmutableArray()
        };
    }

    private static LifecycleStage Rename(LifecycleStage stage, RenameConcept operation, string formerName) =>
        stage.Id != operation.ConceptId ? stage : stage with
        {
            Name = operation.Name,
            Slug = operation.Slug,
            FormerQualifiedNames = Aliases(stage, operation.Slug, formerName)
        };

    private static ConclusionDefinition Rename(ConclusionDefinition conclusion, RenameConcept operation, string formerName) =>
        conclusion.Id != operation.ConceptId ? conclusion : conclusion with
        {
            Name = operation.Name,
            Slug = operation.Slug,
            FormerQualifiedNames = Aliases(conclusion, operation.Slug, formerName)
        };

    private static DecisionRow Rename(DecisionRow row, RenameConcept operation, string formerName) =>
        row.Id != operation.ConceptId ? row : row with
        {
            Name = operation.Name,
            Slug = operation.Slug,
            FormerQualifiedNames = Aliases(row, operation.Slug, formerName)
        };

    private static OutcomeDefinition Rename(OutcomeDefinition outcome, RenameConcept operation, string formerName) =>
        outcome.Id != operation.ConceptId ? outcome : outcome with
        {
            Name = operation.Name,
            Slug = operation.Slug,
            FormerQualifiedNames = Aliases(outcome, operation.Slug, formerName)
        };

    private static EffectDefinition Rename(EffectDefinition effect, RenameConcept operation, string formerName) =>
        effect.Id != operation.ConceptId ? effect : effect with
        {
            Name = operation.Name,
            Slug = operation.Slug,
            FormerQualifiedNames = Aliases(effect, operation.Slug, formerName)
        };

    private static EventDefinition Rename(EventDefinition @event, RenameConcept operation, string formerName) =>
        @event.Id != operation.ConceptId ? @event : @event with
        {
            Name = operation.Name,
            Slug = operation.Slug,
            FormerQualifiedNames = Aliases(@event, operation.Slug, formerName)
        };

    private static TransitionDefinition Rename(TransitionDefinition transition, RenameConcept operation, string formerName) =>
        transition.Id != operation.ConceptId ? transition : transition with
        {
            Name = operation.Name,
            Slug = operation.Slug,
            FormerQualifiedNames = Aliases(transition, operation.Slug, formerName)
        };

    private static ImmutableArray<string> Aliases(
        ISemanticConcept concept,
        SemanticSlug nextSlug,
        string formerQualifiedName) =>
        concept.Slug == nextSlug || concept.FormerQualifiedNames.Contains(formerQualifiedName)
            ? concept.FormerQualifiedNames
            : concept.FormerQualifiedNames.Add(formerQualifiedName);

    private static ApplyResult Failure(
        ImmutableArray<SemanticDefinition> definitions,
        string code,
        string message,
        SemanticId? subjectId = null) =>
        new(definitions, new ModelDiagnostic(code, message, subjectId));

    private static IEnumerable<SemanticNode> Nodes(
        SemanticDefinition definition,
        SemanticId contextId)
    {
        yield return new SemanticNode(definition.Id, definition.Slug, contextId);

        switch (definition)
        {
            case EntityDefinition entity:
                if (entity.Lifecycle is not null)
                {
                    yield return new SemanticNode(entity.Lifecycle.Id, entity.Lifecycle.Slug, entity.Id);
                    foreach (var stage in entity.Lifecycle.Stages) yield return new SemanticNode(stage.Id, stage.Slug, entity.Lifecycle.Id);
                }
                foreach (var field in entity.Fields) yield return new SemanticNode(field.Id, field.Slug, entity.Id);
                foreach (var relationship in entity.Relationships) yield return new SemanticNode(relationship.Id, relationship.Slug, entity.Id);

                break;

            case EnumerationDefinition enumeration:
                foreach (var member in enumeration.Members) yield return new SemanticNode(member.Id, member.Slug, enumeration.Id);
                break;

            case RuleDefinition rule:
                foreach (var conclusion in rule.Conclusions)
                {
                    yield return new SemanticNode(conclusion.Id, conclusion.Slug, rule.Id);
                }

                break;

            case DecisionDefinition decision:
                foreach (var conclusion in decision.Conclusions)
                {
                    yield return new SemanticNode(conclusion.Id, conclusion.Slug, decision.Id);
                }
                foreach (var row in decision.Table.Rows)
                {
                    yield return new SemanticNode(row.Id, row.Slug, decision.Id);
                }
                break;

            case BehaviourDefinition behaviour:
                foreach (var outcome in behaviour.Outcomes)
                {
                    yield return new SemanticNode(outcome.Id, outcome.Slug, behaviour.Id);
                }

                foreach (var effect in behaviour.Effects)
                {
                    yield return new SemanticNode(effect.Id, effect.Slug, behaviour.Id);
                }

                foreach (var publishedEvent in behaviour.PublishedEvents)
                {
                    yield return new SemanticNode(publishedEvent.Id, publishedEvent.Slug, behaviour.Id);
                }

                foreach (var transition in behaviour.Transitions)
                {
                    yield return new SemanticNode(transition.Id, transition.Slug, behaviour.Id);
                }

                break;
        }
    }

    private sealed record ApplyResult(
        ImmutableArray<SemanticDefinition> Definitions,
        ModelDiagnostic? Diagnostic);

    private sealed record SemanticNode(
        SemanticId Id,
        SemanticSlug Slug,
        SemanticId OwnerId);
}
