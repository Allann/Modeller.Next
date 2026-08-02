using System.Text.RegularExpressions;
using Modeller.Contexts;
using Modeller.Model;

namespace Modeller.Rendering;

/// <summary>Projects canonical meaning into data that a Python template pack can consume.</summary>
public sealed class PythonTemplateGlobalsProvider(
    AuthoredContextRevision revision,
    string packageName,
    string projectName,
    string pythonVersion) : ITemplateGlobalsProvider
{
    private readonly TemplateSemanticProjection projection = new(revision);

    public IReadOnlyDictionary<string, object?> GetGlobals(ArtifactRenderingContext context)
    {
        var globals = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["python_package"] = packageName,
            ["project_name"] = projectName,
            ["python_version"] = pythonVersion
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
            _ => throw new InvalidOperationException($"'{input.Id}' cannot be projected into this Python template pack.")
        };
        return globals;
    }

    private object Entity(ProjectedEntity entity)
    {
        var referenced = new List<(string Package, string ModuleName, string ClassName)>();
        string RenderType(DataType type) => PythonDataTypeRenderer.Render(type, id =>
        {
            var target = revision.Definitions.Single(definition => definition.Id == id);
            var package = target is EnumerationDefinition ? "enumerations" : "entities";
            var className = ClassName(target.Name.Value);
            referenced.Add((package, Identifier(target.Name.Value), className));
            return className;
        });

        var properties = entity.Fields.Select(field => new
            {
                name = Identifier(field.RawName),
                type = RenderType(field.Type),
                nullable = field.IsOptional
            }).Concat(entity.Relationships.Select(relationship => new
            {
                name = Identifier(relationship.RawName),
                type = relationship.Cardinality == RelationshipCardinality.Many ? "list[UUID]" : "UUID",
                nullable = relationship.IsOptional
            })).ToArray();

        var imports = referenced.Distinct()
            .OrderBy(reference => reference.Package, StringComparer.Ordinal).ThenBy(reference => reference.ModuleName, StringComparer.Ordinal)
            .Select(reference => $"from ..{reference.Package}.{reference.ModuleName} import {reference.ClassName}").ToArray();

        return new
        {
            kind = "entity",
            name = ClassName(entity.Source.Name.Value),
            module_name = Identifier(entity.Source.Name.Value),
            properties,
            imports
        };
    }

    private static object Enumeration(ProjectedEnumeration enumeration) => new
    {
        kind = "enumeration",
        name = ClassName(enumeration.Source.Name.Value),
        module_name = Identifier(enumeration.Source.Name.Value),
        members = enumeration.Members.Select(member => new { name = ScreamingSnakeCase(member.RawName), value = member.Value }).ToArray()
    };

    private object Rule(ProjectedRule rule)
    {
        var facts = rule.Facts.Select(fact => new { name = Identifier(fact.Source.Name.Value), type = FactTypeName(fact.Source.Type) }).ToArray();
        var name = ClassName(rule.Source.Name.Value);
        var functionName = SnakeCaseFromPascal(name);
        return new
        {
            kind = "rule",
            name,
            subject_name = SubjectName(rule.Source.Name.Value),
            function_name = functionName,
            module_name = functionName,
            facts,
            expression_terms = ExpressionTerms(rule.Expression),
            conclusions = rule.Conclusions.Select(conclusion => new { name = ScreamingSnakeCase(conclusion.RawName) }).ToArray()
        };
    }

    private object Behaviour(ProjectedBehaviour behaviour)
    {
        var name = ClassName(behaviour.Source.Name.Value);
        var functionName = SnakeCaseFromPascal(name);
        var transitions = behaviour.Transitions.Select(transition => new
        {
            source_stage = ScreamingSnakeCase(transition.SourceStage.Name.Value),
            target_stage = ScreamingSnakeCase(transition.TargetStage.Name.Value),
            has_guard = transition.GuardRules.Length > 0,
            guard = string.Join(" and ", transition.GuardRules.Select(rule => $"{SnakeCaseFromPascal(ClassName(rule.Name.Value))}(facts)"))
        }).ToArray();

        // A behaviour may legally have no rule at all (e.g. a zero-transition behaviour that only publishes an
        // event) — "object" needs no import and is a valid, if unused, type hint for the Facts parameter.
        var primaryRule = behaviour.PrimaryRule;
        var factsType = primaryRule is null ? "object" : $"{SubjectName(primaryRule.Name.Value)}Facts";
        var factsModuleName = primaryRule is null ? null : SnakeCaseFromPascal(ClassName(primaryRule.Name.Value));

        // Every distinct rule referenced as a transition guard must be imported by its own module — not just the
        // "primary" rule used for the Facts type — otherwise a multi-binding behaviour calls unresolved functions.
        var guardRules = behaviour.Transitions.SelectMany(transition => transition.GuardRules).DistinctBy(rule => rule.Id).ToArray();
        var imports = guardRules.Select(rule =>
        {
            var moduleName = SnakeCaseFromPascal(ClassName(rule.Name.Value));
            var isPrimary = primaryRule is not null && rule.Id == primaryRule.Id;
            var symbols = isPrimary ? $"{SubjectName(rule.Name.Value)}Facts, {moduleName}" : moduleName;
            return new { module_name = moduleName, symbols };
        }).ToList();
        if (primaryRule is not null && guardRules.All(rule => rule.Id != primaryRule.Id))
            imports.Add(new { module_name = factsModuleName!, symbols = $"{SubjectName(primaryRule.Name.Value)}Facts" });

        return new
        {
            kind = "behaviour",
            name,
            function_name = functionName,
            module_name = functionName,
            stage_type = $"{ClassName(behaviour.Entity.Name.Value)}Stage",
            stages = behaviour.Lifecycle.Stages.Select(stage => new { name = ScreamingSnakeCase(stage.Name.Value) }).ToArray(),
            transitions,
            imports = imports.ToArray(),
            facts_module_name = factsModuleName,
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
        ProjectedAndExpression and => $"({string.Join(" and ", and.Operands.Select(RenderExpression))})",
        _ => throw new NotSupportedException($"'{expression.GetType().Name}' cannot be rendered as a Python expression.")
    };

    private static string SubjectName(string ruleName) => ClassName(ruleName).Replace("Determine", "", StringComparison.Ordinal);

    private static string FactTypeName(FactType type) => type switch
    {
        FactType.Truth => "bool", FactType.Text => "str", FactType.Number => "Decimal", FactType.Date => "date",
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    private static string Identifier(string value) => PythonTemplateNaming.Identifier(value);
    private static string ScreamingSnakeCase(string value) => Identifier(value).ToUpperInvariant();
    private static string ClassName(string value) => PythonTemplateNaming.ClassName(value);
    private static string SnakeCaseFromPascal(string value) => PythonTemplateNaming.SnakeCaseFromPascal(value);
}

public static partial class PythonTemplateNaming
{
    public static string ClassName(string value) => string.Concat(value
        .Split([' ', '-', '_'], StringSplitOptions.RemoveEmptyEntries)
        .Select(word => char.ToUpperInvariant(word[0]) + word[1..]));

    public static string Identifier(string value) => SnakeCaseFromPascal(ClassName(value));

    public static string SnakeCaseFromPascal(string value)
    {
        var withWordBoundaries = LowerToUpperBoundary().Replace(value, "_");
        var withAcronymBoundaries = AcronymBoundary().Replace(withWordBoundaries, "_");
        return withAcronymBoundaries.ToLowerInvariant();
    }

    [GeneratedRegex("(?<=[a-z0-9])(?=[A-Z])")]
    private static partial Regex LowerToUpperBoundary();

    [GeneratedRegex("(?<=[A-Z])(?=[A-Z][a-z])")]
    private static partial Regex AcronymBoundary();
}
