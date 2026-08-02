namespace Modeller.Model;

public abstract record DataType
{
    private protected DataType() { }
}

public sealed record BooleanDataType : DataType;

public sealed record StringDataType : DataType
{
    public StringDataType(int? minimumLength = null, int? maximumLength = null)
    {
        if (minimumLength is < 0) throw new ArgumentOutOfRangeException(nameof(minimumLength));
        if (minimumLength == 0) minimumLength = null;
        if (maximumLength is < 1) throw new ArgumentOutOfRangeException(nameof(maximumLength));
        if (minimumLength > maximumLength) throw new ArgumentException("Minimum length cannot exceed maximum length.");
        MinimumLength = minimumLength;
        MaximumLength = maximumLength;
    }

    public int? MinimumLength { get; }
    public int? MaximumLength { get; }
}

public sealed record ByteDataType : DataType;
public sealed record Int16DataType : DataType;
public sealed record Int32DataType : DataType;
public sealed record Int64DataType : DataType;
public sealed record DateDataType : DataType;
public sealed record TimeDataType : DataType;
public sealed record DateTimeDataType : DataType;
public sealed record DateTimeOffsetDataType : DataType;
public sealed record UniqueIdentifierDataType : DataType;
public sealed record GeographicCoordinateDataType : DataType;

public sealed record DecimalDataType : DataType
{
    public DecimalDataType(int? precision = null, int? scale = null)
    {
        if (precision is null && scale is null) return;
        if (precision is null || precision < 1) throw new ArgumentOutOfRangeException(nameof(precision), "Precision must be greater than zero when decimal constraints are specified.");
        if (scale is null || scale < 0 || scale > precision) throw new ArgumentOutOfRangeException(nameof(scale), "Scale must be between zero and precision when decimal constraints are specified.");
        Precision = precision;
        Scale = scale;
    }

    public int? Precision { get; }
    public int? Scale { get; }
}

public sealed record EnumerationDataType : DataType
{
    public EnumerationDataType(SemanticId enumerationId) => EnumerationId = Required(enumerationId, nameof(enumerationId));
    public SemanticId EnumerationId { get; }
    private static SemanticId Required(SemanticId id, string parameter) => id.Value == Guid.Empty ? throw new ArgumentException("A stable semantic identity is required.", parameter) : id;
}

public sealed record EntityReferenceDataType : DataType
{
    public EntityReferenceDataType(SemanticId entityId) => EntityId = entityId.Value == Guid.Empty ? throw new ArgumentException("A stable semantic identity is required.", nameof(entityId)) : entityId;
    public SemanticId EntityId { get; }
}

public sealed record ValueTypeReferenceDataType : DataType
{
    public ValueTypeReferenceDataType(SemanticId valueTypeId) => ValueTypeId = valueTypeId.Value == Guid.Empty ? throw new ArgumentException("A stable semantic identity is required.", nameof(valueTypeId)) : valueTypeId;
    public SemanticId ValueTypeId { get; }
}
