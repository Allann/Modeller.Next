using Modeller.Model;

namespace Modeller.Rendering;

public static class CSharpDataTypeRenderer
{
    public static string Render(DataType type, Func<SemanticId, string> semanticName) => type switch
    {
        BooleanDataType => "bool",
        StringDataType => "string",
        ByteDataType => "byte",
        Int16DataType => "short",
        Int32DataType => "int",
        Int64DataType => "long",
        DecimalDataType => "decimal",
        DateDataType => "DateOnly",
        TimeDataType => "TimeOnly",
        DateTimeDataType => "DateTime",
        DateTimeOffsetDataType => "DateTimeOffset",
        UniqueIdentifierDataType => "Guid",
        GeographicCoordinateDataType => "(decimal Latitude, decimal Longitude)",
        EnumerationDataType value => semanticName(value.EnumerationId),
        EntityReferenceDataType value => semanticName(value.EntityId),
        ValueTypeReferenceDataType value => semanticName(value.ValueTypeId),
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported canonical DataType.")
    };
}
