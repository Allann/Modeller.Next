using Modeller.Contexts;
using Modeller.Model;

namespace Modeller.Rendering;

/// <summary>Projects canonical meaning into data that a C# template pack can consume.</summary>
public sealed class CSharpTemplateGlobalsProvider(
    AuthoredContextRevision revision,
    string namespaceName,
    string projectName,
    string targetFramework) : ITemplateGlobalsProvider
{
    private readonly IReadOnlyDictionary<SemanticId, SemanticDefinition> definitions =
        revision.Definitions.ToDictionary(definition => definition.Id);

    public IReadOnlyDictionary<string, object?> GetGlobals(ArtifactRenderingContext context)
    {
        var globals = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["csharp_namespace"] = namespaceName,
            ["project_name"] = projectName,
            ["target_framework"] = targetFramework
        };

        if (context.Artifact.SemanticInputs.Length == 0) return globals;
        var input = context.Artifact.SemanticInputs.Single();
        var definition = revision.Definitions.Single(item => item.Slug.Value == input.Id);
        globals["definition"] = definition switch
        {
            EntityDefinition entity => Entity(entity),
            EnumerationDefinition enumeration => Enumeration(enumeration),
            RuleDefinition rule => Rule(rule),
            BehaviourDefinition behaviour => Behaviour(behaviour),
            _ => throw new InvalidOperationException($"'{input.Id}' cannot be projected into this C# template pack.")
        };
        return globals;
    }

    private object Entity(EntityDefinition entity)
    {
        var properties = entity.Fields.Select(field => new
            {
                name = Identifier(field.Name.Value),
                type = CSharpDataTypeRenderer.Render(field.Type, id => Identifier(definitions[id].Name.Value)),
                nullable = field.IsOptional && field.Type is not StringDataType
            }).Concat(entity.Relationships.Select(relationship => new
            {
                name = Identifier(relationship.Name.Value),
                type = relationship.Cardinality == RelationshipCardinality.Many ? "IReadOnlyList<Guid>" : "Guid",
                nullable = relationship.Cardinality == RelationshipCardinality.One && relationship.IsOptional
            })).ToArray();
        return new { kind = "entity", name = CSharpTemplateNaming.Identifier(entity.Name.Value), properties };
    }

    private static object Enumeration(EnumerationDefinition enumeration) => new
    {
        kind = "enumeration",
        name = CSharpTemplateNaming.Identifier(enumeration.Name.Value),
        members = enumeration.Members.OrderBy(member => member.Value)
            .Select(member => new { name = CSharpTemplateNaming.Identifier(member.Name.Value), value = member.Value }).ToArray()
    };

    private object Rule(RuleDefinition rule)
    {
        var facts = rule.InputFacts.Select(reference => (FactDefinition)definitions[reference.TargetId])
            .Select(fact => new { name = Identifier(fact.Name.Value), type = FactTypeName(fact.Type) }).ToArray();
        return new
        {
            kind = "rule",
            name = Identifier(rule.Name.Value),
            subject_name = Identifier(rule.Name.Value).Replace("Determine", "", StringComparison.Ordinal),
            facts
        };
    }

    private object Behaviour(BehaviourDefinition behaviour)
    {
        var entity = (EntityDefinition)definitions[behaviour.Entity.TargetId];
        var transition = behaviour.Transitions.Single();
        var lifecycle = entity.Lifecycle ?? throw new InvalidOperationException("A generated transition requires a lifecycle.");
        var source = lifecycle.Stages.Single(stage => stage.Id == transition.SourceStage.TargetId);
        var target = lifecycle.Stages.Single(stage => stage.Id == transition.TargetStage.TargetId);
        var rule = (RuleDefinition)definitions[behaviour.RuleBindings.Single().Rule.TargetId];
        var ruleName = Identifier(rule.Name.Value).Replace("Determine", "", StringComparison.Ordinal);
        return new
        {
            kind = "behaviour",
            name = Identifier(behaviour.Name.Value),
            stage_type = $"{Identifier(entity.Name.Value)}Stage",
            stages = lifecycle.Stages.Select(stage => new { name = Identifier(stage.Name.Value) }).ToArray(),
            source_stage = Identifier(source.Name.Value),
            target_stage = Identifier(target.Name.Value),
            rule_name = ruleName,
            facts_type = $"{ruleName}Facts"
        };
    }

    private static string FactTypeName(FactType type) => type switch
    {
        FactType.Truth => "bool", FactType.Text => "string", FactType.Number => "decimal", FactType.Date => "DateOnly",
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    private static string Identifier(string value) => CSharpTemplateNaming.Identifier(value);
}

public static class CSharpTemplateNaming
{
    public static string Identifier(string value) => string.Concat(value
        .Split([' ', '-', '_'], StringSplitOptions.RemoveEmptyEntries)
        .Select(word => char.ToUpperInvariant(word[0]) + word[1..]));
}
