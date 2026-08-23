using Modeller.Model;
using Modeller.Parsing;
using Xunit;

namespace Modeller.Samples.ChildCare.Acceptance.Features;

/// <summary>Shared helpers for the "compile a small in-memory RML fragment, then check the
/// compiled shape" pattern used by every entity-shape acceptance story (Adult, User, ...): find
/// the entity definition by name, then check one field's declared type and optionality against
/// what the scenario expects.</summary>
internal static class EntityShapeAssertions
{
    public static EntityDefinition FindEntity(this ParseResult result, string entityName) =>
        result.Package!.AuthoredRevision.Definitions.OfType<EntityDefinition>().Single(entity => entity.Name.Value == entityName);

    public static void AssertField(this EntityDefinition entity, string fieldName, Func<DataType, bool> typeMatches, bool optional)
    {
        var field = entity.Fields.Single(candidate => candidate.Name.Value == fieldName);
        Assert.True(typeMatches(field.Type), $"Field '{fieldName}' has an unexpected type: {field.Type}.");
        Assert.Equal(optional, field.IsOptional);
    }

    public static void AssertRelationship(this EntityDefinition entity, string relationshipName, RelationshipCardinality cardinality, bool optional)
    {
        var relationship = entity.Relationships.Single(candidate => candidate.Name.Value == relationshipName);
        Assert.Equal(cardinality, relationship.Cardinality);
        Assert.Equal(optional, relationship.IsOptional);
    }

    public static string RelationshipTargetName(this ParseResult result, string entityName, string relationshipName)
    {
        var entity = result.FindEntity(entityName);
        var relationship = entity.Relationships.Single(candidate => candidate.Name.Value == relationshipName);
        return result.Package!.AuthoredRevision.Definitions.OfType<EntityDefinition>()
            .Single(candidate => candidate.Id == relationship.TargetId).Name.Value;
    }
}
