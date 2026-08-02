using Modeller.Model;
using Xunit;

namespace Modeller.Model.Tests;

public sealed class DataTypeTests
{
    public static TheoryData<DataType, Type> Variants => new()
    {
        { new BooleanDataType(), typeof(BooleanDataType) }, { new StringDataType(), typeof(StringDataType) },
        { new ByteDataType(), typeof(ByteDataType) }, { new Int16DataType(), typeof(Int16DataType) },
        { new Int32DataType(), typeof(Int32DataType) }, { new Int64DataType(), typeof(Int64DataType) },
        { new DecimalDataType(), typeof(DecimalDataType) }, { new DateDataType(), typeof(DateDataType) },
        { new TimeDataType(), typeof(TimeDataType) }, { new DateTimeDataType(), typeof(DateTimeDataType) },
        { new DateTimeOffsetDataType(), typeof(DateTimeOffsetDataType) }, { new UniqueIdentifierDataType(), typeof(UniqueIdentifierDataType) },
        { new GeographicCoordinateDataType(), typeof(GeographicCoordinateDataType) }
    };

    [Theory, MemberData(nameof(Variants))]
    public void Primitive_variants_are_closed_runtime_types(DataType value, Type expected) => Assert.IsType(expected, value);

    [Fact]
    public void String_defaults_are_unconstrained() => Assert.Equal((null, null), (new StringDataType().MinimumLength, new StringDataType().MaximumLength));

    [Theory]
    [InlineData(-1, null)] [InlineData(null, 0)] [InlineData(10, 5)]
    public void String_rejects_invalid_length_ranges(int? minimum, int? maximum) =>
        Assert.ThrowsAny<ArgumentException>(() => new StringDataType(minimum, maximum));

    [Fact]
    public void String_preserves_legacy_zero_minimum_normalization() => Assert.Null(new StringDataType(0, 100).MinimumLength);

    [Fact]
    public void String_accepts_a_valid_range() => Assert.Equal((10, 255), (new StringDataType(10, 255).MinimumLength, new StringDataType(10, 255).MaximumLength));

    [Fact]
    public void Decimal_defaults_are_unconstrained() => Assert.Equal((null, null), (new DecimalDataType().Precision, new DecimalDataType().Scale));

    [Theory]
    [InlineData(0, 0)] [InlineData(12, -1)] [InlineData(12, 13)] [InlineData(12, null)] [InlineData(null, 2)]
    public void Decimal_rejects_invalid_or_partial_constraints(int? precision, int? scale) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new DecimalDataType(precision, scale));

    [Fact]
    public void Decimal_accepts_valid_precision_and_scale() => Assert.Equal((12, 2), (new DecimalDataType(12, 2).Precision, new DecimalDataType(12, 2).Scale));

    [Fact]
    public void Semantic_reference_types_require_and_retain_stable_identity()
    {
        var id = SemanticId.Parse("0191f6d4-4ea0-7000-8000-000000000099");
        Assert.Equal(id, new EnumerationDataType(id).EnumerationId);
        Assert.Equal(id, new EntityReferenceDataType(id).EntityId);
        Assert.Equal(id, new ValueTypeReferenceDataType(id).ValueTypeId);
    }

    [Fact]
    public void Semantic_reference_types_reject_the_default_identity()
    {
        Assert.Throws<ArgumentException>(() => new EnumerationDataType(default));
        Assert.Throws<ArgumentException>(() => new EntityReferenceDataType(default));
        Assert.Throws<ArgumentException>(() => new ValueTypeReferenceDataType(default));
    }
}
