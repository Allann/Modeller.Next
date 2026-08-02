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
    private readonly IReadOnlyDictionary<SemanticId, SemanticDefinition> definitions =
        revision.Definitions.ToDictionary(definition => definition.Id);

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
            EntityDefinition entity => Entity(entity),
            EnumerationDefinition enumeration => Enumeration(enumeration),
            RuleDefinition rule => Rule(rule),
            BehaviourDefinition behaviour => Behaviour(behaviour),
            _ => throw new InvalidOperationException($"'{input.Id}' cannot be projected into this Python template pack.")
        };
        return globals;
    }

    private object Entity(EntityDefinition entity)
    {
        var referenced = new List<(string Package, string ModuleName, string ClassName)>();
        string RenderType(DataType type) => PythonDataTypeRenderer.Render(type, id =>
        {
            var target = definitions[id];
            var package = target is EnumerationDefinition ? "enumerations" : "entities";
            var className = ClassName(target.Name.Value);
            referenced.Add((package, Identifier(target.Name.Value), className));
            return className;
        });

        var properties = entity.Fields.Select(field => new
            {
                name = Identifier(field.Name.Value),
                type = RenderType(field.Type),
                nullable = field.IsOptional && field.Type is not StringDataType
            }).Concat(entity.Relationships.Select(relationship => new
            {
                name = Identifier(relationship.Name.Value),
                type = relationship.Cardinality == RelationshipCardinality.Many ? "list[UUID]" : "UUID",
                nullable = relationship.Cardinality == RelationshipCardinality.One && relationship.IsOptional
            })).ToArray();

        var imports = referenced.Distinct()
            .OrderBy(reference => reference.Package, StringComparer.Ordinal).ThenBy(reference => reference.ModuleName, StringComparer.Ordinal)
            .Select(reference => $"from ..{reference.Package}.{reference.ModuleName} import {reference.ClassName}").ToArray();

        return new
        {
            kind = "entity",
            name = ClassName(entity.Name.Value),
            module_name = Identifier(entity.Name.Value),
            properties,
            imports
        };
    }

    private static object Enumeration(EnumerationDefinition enumeration) => new
    {
        kind = "enumeration",
        name = ClassName(enumeration.Name.Value),
        module_name = Identifier(enumeration.Name.Value),
        members = enumeration.Members.OrderBy(member => member.Value)
            .Select(member => new { name = ScreamingSnakeCase(member.Name.Value), value = member.Value }).ToArray()
    };

    private object Rule(RuleDefinition rule)
    {
        var facts = rule.InputFacts.Select(reference => (FactDefinition)definitions[reference.TargetId])
            .Select(fact => new { name = Identifier(fact.Name.Value), type = FactTypeName(fact.Type) }).ToArray();
        var name = ClassName(rule.Name.Value);
        var functionName = SnakeCaseFromPascal(name);
        return new
        {
            kind = "rule",
            name,
            subject_name = name.Replace("Determine", "", StringComparison.Ordinal),
            function_name = functionName,
            module_name = functionName,
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
        var ruleName = ClassName(rule.Name.Value);
        var ruleFunctionName = SnakeCaseFromPascal(ruleName);
        var ruleSubjectName = ruleName.Replace("Determine", "", StringComparison.Ordinal);
        var name = ClassName(behaviour.Name.Value);
        var functionName = SnakeCaseFromPascal(name);
        return new
        {
            kind = "behaviour",
            name,
            function_name = functionName,
            module_name = functionName,
            stage_type = $"{ClassName(entity.Name.Value)}Stage",
            stages = lifecycle.Stages.Select(stage => new { name = ScreamingSnakeCase(stage.Name.Value) }).ToArray(),
            source_stage = ScreamingSnakeCase(source.Name.Value),
            target_stage = ScreamingSnakeCase(target.Name.Value),
            rule_function_name = ruleFunctionName,
            facts_type = $"{ruleSubjectName}Facts"
        };
    }

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
