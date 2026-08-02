using Modeller.Model;
using Xunit;

namespace Modeller.Rendering.Tests;

public sealed class CSharpDataTypeRendererTests
{
    public static TheoryData<DataType, string> PrimitiveMappings => new()
    {
        { new BooleanDataType(), "bool" }, { new StringDataType(), "string" }, { new ByteDataType(), "byte" },
        { new Int16DataType(), "short" }, { new Int32DataType(), "int" }, { new Int64DataType(), "long" },
        { new DecimalDataType(), "decimal" }, { new DateDataType(), "DateOnly" }, { new TimeDataType(), "TimeOnly" },
        { new DateTimeDataType(), "DateTime" }, { new DateTimeOffsetDataType(), "DateTimeOffset" },
        { new UniqueIdentifierDataType(), "Guid" }, { new GeographicCoordinateDataType(), "(decimal Latitude, decimal Longitude)" }
    };

    [Theory, MemberData(nameof(PrimitiveMappings))]
    public void Primitive_types_have_stable_CSharp_mappings(DataType type, string expected) =>
        Assert.Equal(expected, CSharpDataTypeRenderer.Render(type, _ => throw new InvalidOperationException()));

    [Fact]
    public void Semantic_reference_types_use_the_supplied_canonical_name()
    {
        var id = SemanticId.Parse("0191f6d4-4ea0-7000-8000-000000000099");
        Assert.Equal("BookingStatus", CSharpDataTypeRenderer.Render(new EnumerationDataType(id), actual => actual == id ? "BookingStatus" : "wrong"));
        Assert.Equal("Booking", CSharpDataTypeRenderer.Render(new EntityReferenceDataType(id), _ => "Booking"));
        Assert.Equal("Money", CSharpDataTypeRenderer.Render(new ValueTypeReferenceDataType(id), _ => "Money"));
    }
}
