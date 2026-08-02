using Modeller.Model;
using Xunit;

namespace Modeller.Rendering.Tests;

public sealed class PythonDataTypeRendererTests
{
    public static TheoryData<DataType, string> PrimitiveMappings => new()
    {
        { new BooleanDataType(), "bool" }, { new StringDataType(), "str" }, { new ByteDataType(), "int" },
        { new Int16DataType(), "int" }, { new Int32DataType(), "int" }, { new Int64DataType(), "int" },
        { new DecimalDataType(), "Decimal" }, { new DateDataType(), "date" }, { new TimeDataType(), "time" },
        { new DateTimeDataType(), "datetime" }, { new DateTimeOffsetDataType(), "datetime" },
        { new UniqueIdentifierDataType(), "UUID" }, { new GeographicCoordinateDataType(), "tuple[Decimal, Decimal]" }
    };

    [Theory, MemberData(nameof(PrimitiveMappings))]
    public void Primitive_types_have_stable_Python_mappings(DataType type, string expected) =>
        Assert.Equal(expected, PythonDataTypeRenderer.Render(type, _ => throw new InvalidOperationException()));

    [Fact]
    public void Semantic_reference_types_use_the_supplied_canonical_name()
    {
        var id = SemanticId.Parse("0191f6d4-4ea0-7000-8000-000000000099");
        Assert.Equal("BookingStatus", PythonDataTypeRenderer.Render(new EnumerationDataType(id), actual => actual == id ? "BookingStatus" : "wrong"));
        Assert.Equal("Booking", PythonDataTypeRenderer.Render(new EntityReferenceDataType(id), _ => "Booking"));
        Assert.Equal("Money", PythonDataTypeRenderer.Render(new ValueTypeReferenceDataType(id), _ => "Money"));
    }
}
