using Modeller.Model;

namespace Modeller.Rendering;

public static class PythonDataTypeRenderer
{
    public static string Render(DataType type, Func<SemanticId, string> semanticName) => type switch
    {
        BooleanDataType => "bool",
        StringDataType => "str",
        ByteDataType => "int",
        Int16DataType => "int",
        Int32DataType => "int",
        Int64DataType => "int",
        DecimalDataType => "Decimal",
        DateDataType => "date",
        TimeDataType => "time",
        DateTimeDataType => "datetime",
        DateTimeOffsetDataType => "datetime",
        UniqueIdentifierDataType => "UUID",
        GeographicCoordinateDataType => "tuple[Decimal, Decimal]",
        EnumerationDataType value => semanticName(value.EnumerationId),
        EntityReferenceDataType value => semanticName(value.EntityId),
        ValueTypeReferenceDataType value => semanticName(value.ValueTypeId),
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported canonical DataType.")
    };
}
