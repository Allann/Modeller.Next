using System.Collections.Immutable;
using System.Text.Json;
using Modeller.Model;

namespace Modeller.Contexts;

public static partial class ContextPackageSystem
{
    private static ImmutableArray<ContextImport> ReadImports(JsonElement root) =>
        root.GetProperty("imports").EnumerateArray().Select(import => new ContextImport(
            import.GetProperty("contextId").GetString()!,
            import.GetProperty("versionRange").GetString()!,
            import.GetProperty("concepts").EnumerateArray().Select(concept => new ImportedConcept(
                concept.GetProperty("id").GetString()!,
                concept.GetProperty("kind").GetString()!)).ToImmutableArray())).ToImmutableArray();

    private static ImmutableArray<ExportedConcept> ReadExports(
        JsonElement root,
        AuthoredContextRevision revision) =>
        root.GetProperty("exports").EnumerateArray().Select(item =>
        {
            var id = item.GetString()!;
            var concept = revision.FindConcept(SemanticId.Parse(id)) ??
                throw new ArgumentException($"Exported concept '{id}' is not owned by this context package.");
            return new ExportedConcept(id, concept.Kind.ToString());
        }).ToImmutableArray();

    private static bool VersionRangeIncludes(string range, string version)
    {
        var candidate = Version.Parse(version);
        return range.Split(' ', StringSplitOptions.RemoveEmptyEntries).All(constraint =>
        {
            if (constraint.StartsWith(">=", StringComparison.Ordinal))
            {
                return candidate >= Version.Parse(constraint[2..]);
            }

            if (constraint.StartsWith('<'))
            {
                return candidate < Version.Parse(constraint[1..]);
            }

            return candidate == Version.Parse(constraint.TrimStart('='));
        });
    }

    private static bool IsValidVersionRange(string range)
    {
        try
        {
            var constraints = range.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return constraints.Length is > 0 and <= 2 && constraints.All(constraint =>
            {
                var value = constraint.StartsWith(">=", StringComparison.Ordinal)
                    ? constraint[2..]
                    : constraint.StartsWith('<') || constraint.StartsWith('=')
                        ? constraint[1..]
                        : constraint;
                _ = Version.Parse(value);
                return constraint.StartsWith(">=", StringComparison.Ordinal) ||
                    constraint.StartsWith('<') ||
                    constraint.StartsWith('=') ||
                    constraints.Length == 1;
            });
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or OverflowException)
        {
            return false;
        }
    }

    private static SemanticDefinition ReadDefinition(JsonElement definition)
    {
        var identity = ReadIdentity(definition);
        return definition.GetProperty("kind").GetString() switch
        {
            "Entity" => new EntityDefinition(identity.Id, identity.Name, identity.Slug,
                definition.TryGetProperty("lifecycle", out var lifecycle) ? ReadLifecycle(lifecycle) : null)
            {
                Fields = definition.TryGetProperty("fields", out var fields) ? fields.EnumerateArray().Select(ReadField).ToImmutableArray() : [],
                Relationships = definition.TryGetProperty("relationships", out var relationships) ? relationships.EnumerateArray().Select(ReadRelationship).ToImmutableArray() : []
            },
            "Enumeration" => new EnumerationDefinition(identity.Id, identity.Name, identity.Slug,
                definition.GetProperty("members").EnumerateArray().Select(member =>
                {
                    var memberIdentity = ReadIdentity(member);
                    return new EnumerationMember(memberIdentity.Id, memberIdentity.Name, memberIdentity.Slug, member.GetProperty("value").GetInt32());
                }).ToImmutableArray()),
            "Fact" => new FactDefinition(
                identity.Id,
                identity.Name,
                identity.Slug,
                Enum.Parse<FactType>(definition.GetProperty("type").GetString()!)),
            "Rule" => new RuleDefinition(
                identity.Id,
                identity.Name,
                identity.Slug,
                definition.GetProperty("inputFacts").EnumerateArray()
                    .Select(item => new FactReference(SemanticId.Parse(item.GetString()!)))
                    .ToImmutableArray(),
                definition.GetProperty("conclusions").EnumerateArray()
                    .Select(ReadConclusion)
                    .ToImmutableArray(),
                ReadExpression(definition.GetProperty("expression"))),
            "Decision" => ReadDecision(definition, identity),
            "Behaviour" => ReadBehaviour(definition, identity),
            var kind => throw new ArgumentException($"Unsupported semantic definition kind '{kind}'.")
        };
    }

    private static BehaviourDefinition ReadBehaviour(JsonElement definition, Identity identity) =>
        new(
            identity.Id,
            identity.Name,
            identity.Slug,
            new EntityReference(SemanticId.Parse(definition.GetProperty("entity").GetString()!)),
            definition.GetProperty("outcomes").EnumerateArray().Select(ReadOutcome).ToImmutableArray(),
            definition.TryGetProperty("effects", out var effects) ? effects.EnumerateArray().Select(ReadEffect).ToImmutableArray() : [],
            definition.TryGetProperty("publishedEvents", out var events) ? events.EnumerateArray().Select(ReadEvent).ToImmutableArray() : [],
            definition.GetProperty("transitions").EnumerateArray().Select(ReadTransition).ToImmutableArray(),
            definition.GetProperty("ruleBindings").EnumerateArray().Select(ReadRuleBinding).ToImmutableArray());

    private static DecisionDefinition ReadDecision(JsonElement definition, Identity identity)
    {
        var table = definition.GetProperty("table");
        return new DecisionDefinition(
            identity.Id,
            identity.Name,
            identity.Slug,
            definition.GetProperty("inputFacts").EnumerateArray()
                .Select(item => new FactReference(SemanticId.Parse(item.GetString()!))).ToImmutableArray(),
            definition.GetProperty("conclusions").EnumerateArray().Select(ReadConclusion).ToImmutableArray(),
            new DecisionTable(
                Enum.Parse<DecisionHitPolicy>(table.GetProperty("hitPolicy").GetString()!),
                table.GetProperty("rows").EnumerateArray().Select(ReadDecisionRow).ToImmutableArray()));
    }

    private static DecisionRow ReadDecisionRow(JsonElement row)
    {
        var identity = ReadIdentity(row);
        return new DecisionRow(
            identity.Id,
            identity.Name,
            identity.Slug,
            row.GetProperty("conditions").EnumerateArray().Select(condition => new TruthDecisionCondition(
                new FactReference(SemanticId.Parse(condition.GetProperty("factId").GetString()!)),
                condition.GetProperty("expected").ValueKind == JsonValueKind.String
                    ? null
                    : condition.GetProperty("expected").GetBoolean(),
                OptionalString(condition, "missingFindingCode"))).ToImmutableArray(),
            new ConclusionReference(SemanticId.Parse(row.GetProperty("conclusion").GetString()!)),
            row.GetProperty("findingCode").GetString()!);
    }

    private static LifecycleDefinition ReadLifecycle(JsonElement element)
    {
        var identity = ReadIdentity(element);
        return new LifecycleDefinition(
            identity.Id,
            identity.Name,
            identity.Slug,
            element.GetProperty("stages").EnumerateArray()
                .Select(stage =>
                {
                    var stageIdentity = ReadIdentity(stage);
                    return new LifecycleStage(stageIdentity.Id, stageIdentity.Name, stageIdentity.Slug);
                })
                .ToImmutableArray());
    }

    private static FieldDefinition ReadField(JsonElement element)
    {
        var identity = ReadIdentity(element);
        DataType type = element.GetProperty("type").GetString()! switch
        {
            "Boolean" => new BooleanDataType(),
            "String" => new StringDataType(
                element.TryGetProperty("minimumLength", out var minimumLength) ? minimumLength.GetInt32() : null,
                element.TryGetProperty("maximumLength", out var maximumLength) ? maximumLength.GetInt32() : null),
            "Byte" => new ByteDataType(),
            "Int16" => new Int16DataType(),
            "Int32" => new Int32DataType(),
            "Int64" => new Int64DataType(),
            "Date" => new DateDataType(),
            "Time" => new TimeDataType(),
            "DateTime" => new DateTimeDataType(),
            "DateTimeOffset" => new DateTimeOffsetDataType(),
            "UniqueIdentifier" => new UniqueIdentifierDataType(),
            "GeographicCoordinate" => new GeographicCoordinateDataType(),
            "Decimal" => new DecimalDataType(
                element.TryGetProperty("precision", out var precision) ? precision.GetInt32() : null,
                element.TryGetProperty("scale", out var scale) ? scale.GetInt32() : null),
            "Enumeration" => new EnumerationDataType(SemanticId.Parse(element.GetProperty("namedType").GetString()!)),
            "EntityReference" => new EntityReferenceDataType(SemanticId.Parse(element.GetProperty("namedType").GetString()!)),
            "ValueTypeReference" => new ValueTypeReferenceDataType(SemanticId.Parse(element.GetProperty("namedType").GetString()!)),
            var kind => throw new ArgumentException($"Unsupported data type '{kind}'.")
        };
        return new(identity.Id, identity.Name, identity.Slug,
            type,
            element.TryGetProperty("optional", out var optional) && optional.GetBoolean());
    }

    private static RelationshipDefinition ReadRelationship(JsonElement element)
    {
        var identity = ReadIdentity(element);
        return new(identity.Id, identity.Name, identity.Slug, SemanticId.Parse(element.GetProperty("target").GetString()!),
            Enum.Parse<RelationshipCardinality>(element.GetProperty("cardinality").GetString()!),
            element.TryGetProperty("optional", out var optional) && optional.GetBoolean());
    }

    private static ConclusionDefinition ReadConclusion(JsonElement element)
    {
        var identity = ReadIdentity(element);
        return new ConclusionDefinition(identity.Id, identity.Name, identity.Slug);
    }

    private static RuleExpression ReadExpression(JsonElement element) =>
        element.GetProperty("kind").GetString() switch
        {
            "Fact" => new FactExpression(
                new FactReference(SemanticId.Parse(element.GetProperty("factId").GetString()!)),
                OptionalString(element, "trueFindingCode"),
                OptionalString(element, "falseFindingCode"),
                OptionalString(element, "missingFindingCode")),
            "And" => new AndExpression(element.GetProperty("operands").EnumerateArray().Select(ReadExpression).ToImmutableArray()),
            var kind => throw new ArgumentException($"Unsupported rule expression kind '{kind}'.")
        };

    private static string? OptionalString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) ? property.GetString() : null;

    private static OutcomeDefinition ReadOutcome(JsonElement element)
    {
        var identity = ReadIdentity(element);
        return new OutcomeDefinition(identity.Id, identity.Name, identity.Slug);
    }

    private static EffectDefinition ReadEffect(JsonElement element)
    {
        var identity = ReadIdentity(element);
        return new EffectDefinition(identity.Id, identity.Name, identity.Slug);
    }

    private static EventDefinition ReadEvent(JsonElement element)
    {
        var identity = ReadIdentity(element);
        return new EventDefinition(identity.Id, identity.Name, identity.Slug);
    }

    private static TransitionDefinition ReadTransition(JsonElement element)
    {
        var identity = ReadIdentity(element);
        return new TransitionDefinition(
            identity.Id,
            identity.Name,
            identity.Slug,
            new LifecycleReference(SemanticId.Parse(element.GetProperty("lifecycle").GetString()!)),
            new LifecycleStageReference(SemanticId.Parse(element.GetProperty("sourceStage").GetString()!)),
            new LifecycleStageReference(SemanticId.Parse(element.GetProperty("targetStage").GetString()!)),
            new OutcomeReference(SemanticId.Parse(element.GetProperty("outcome").GetString()!)));
    }

    private static RuleBinding ReadRuleBinding(JsonElement element)
    {
        var bindings = element.GetProperty("factBindings").EnumerateObject().ToImmutableDictionary(
            property => new FactReference(SemanticId.Parse(property.Name)),
            property => new FactReference(SemanticId.Parse(property.Value.GetString()!)));
        return new RuleBinding(
            new RuleReference(SemanticId.Parse(element.GetProperty("rule").GetString()!)),
            Enum.Parse<RuleBindingPurpose>(element.GetProperty("purpose").GetString()!),
            bindings);
    }

    private static Identity ReadIdentity(JsonElement element) =>
        new(
            SemanticId.Parse(element.GetProperty("id").GetString()!),
            new SemanticName(element.GetProperty("name").GetString()!),
            new SemanticSlug(element.GetProperty("slug").GetString()!));

    private sealed record Identity(SemanticId Id, SemanticName Name, SemanticSlug Slug);
}
