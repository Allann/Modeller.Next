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
    private readonly TemplateSemanticProjection projection = new(revision);

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
            EntityDefinition entity => Entity(projection.Entity(entity)),
            EnumerationDefinition enumeration => Enumeration(projection.Enumeration(enumeration)),
            RuleDefinition rule => Rule(projection.Rule(rule)),
            BehaviourDefinition behaviour => Behaviour(projection.Behaviour(behaviour)),
            _ => throw new InvalidOperationException($"'{input.Id}' cannot be projected into this C# template pack.")
        };
        return globals;
    }

    private object Entity(ProjectedEntity entity)
    {
        var properties = entity.Fields.Select(field => new
            {
                name = Identifier(field.RawName),
                type = CSharpDataTypeRenderer.Render(field.Type, id => Identifier(FindName(id))),
                nullable = field.IsOptional
            }).Concat(entity.Relationships.Select(relationship => new
            {
                name = Identifier(relationship.RawName),
                type = relationship.Cardinality == RelationshipCardinality.Many ? "IReadOnlyList<Guid>" : "Guid",
                nullable = relationship.IsOptional
            })).ToArray();
        var lifecycle = entity.Source.Lifecycle;
        return new
        {
            kind = "entity",
            name = Identifier(entity.Source.Name.Value),
            properties,
            stage_type = lifecycle is null ? null : $"{Identifier(entity.Source.Name.Value)}Stage",
            stages = lifecycle?.Stages.Select(stage => new { name = Identifier(stage.Name.Value) }).ToArray() ?? []
        };
    }

    private static object Enumeration(ProjectedEnumeration enumeration) => new
    {
        kind = "enumeration",
        name = Identifier(enumeration.Source.Name.Value),
        members = enumeration.Members.Select(member => new { name = Identifier(member.RawName), value = member.Value }).ToArray()
    };

    private object Rule(ProjectedRule rule)
    {
        var facts = rule.Facts.Select(fact => new { name = Identifier(fact.Source.Name.Value), type = FactTypeName(fact.Source.Type) }).ToArray();
        var subjectName = SubjectName(rule.Source.Name.Value);
        return new
        {
            kind = "rule",
            name = Identifier(rule.Source.Name.Value),
            subject_name = subjectName,
            facts,
            expression_terms = ExpressionTerms(rule.Expression),
            conclusions = rule.Conclusions.Select(conclusion => new { name = Identifier(conclusion.RawName) }).ToArray()
        };
    }

    private object Behaviour(ProjectedBehaviour behaviour)
    {
        var transitions = behaviour.Transitions.Select(transition => new
        {
            source_stage = Identifier(transition.SourceStage.Name.Value),
            target_stage = Identifier(transition.TargetStage.Name.Value),
            guard = string.Join(" && ", transition.GuardRules.Select(rule => $"{SubjectName(rule.Name.Value)}.Determine(facts)"))
        }).ToArray();
        // A behaviour may legally have no rule at all (e.g. a zero-transition behaviour that only publishes an
        // event) — "object" needs no using directive and is a valid, if unused, type for the Facts parameter.
        var factsType = behaviour.PrimaryRule is null ? "object" : $"{SubjectName(behaviour.PrimaryRule.Name.Value)}Facts";
        return new
        {
            kind = "behaviour",
            name = Identifier(behaviour.Source.Name.Value),
            stage_type = $"{Identifier(behaviour.Entity.Name.Value)}Stage",
            stages = behaviour.Lifecycle.Stages.Select(stage => new { name = Identifier(stage.Name.Value) }).ToArray(),
            transitions,
            facts_type = factsType
        };
    }

    private static string[] ExpressionTerms(ProjectedRuleExpression expression) => expression switch
    {
        ProjectedAndExpression and => and.Operands.Select(RenderExpression).ToArray(),
        _ => [RenderExpression(expression)]
    };

    private static string RenderExpression(ProjectedRuleExpression expression) => expression switch
    {
        ProjectedFactTerm term => $"facts.{Identifier(term.Fact.Source.Name.Value)}",
        ProjectedAndExpression and => $"({string.Join(" && ", and.Operands.Select(RenderExpression))})",
        _ => throw new NotSupportedException($"'{expression.GetType().Name}' cannot be rendered as a C# expression.")
    };

    private string FindName(SemanticId id) => revision.Definitions.Single(definition => definition.Id == id).Name.Value;

    private static string SubjectName(string ruleName) => Identifier(ruleName).Replace("Determine", "", StringComparison.Ordinal);

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
