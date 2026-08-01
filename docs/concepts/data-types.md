---
title: "Data Types"
---

# Data Types

> ⚠️ **Legacy Documentation** - This describes data types in the existing C# implementation. For the future type system, see [Future Data Types](architecture/draft/07-data-types.md).

This document describes the data types available for defining fields in entities.

## Primitive Types

| Type | C# Equivalent | Description |
|------|---------------|-------------|
| `DataType.String` | `string` | Text data |
| `DataType.Bool` | `bool` | Boolean true/false |
| `DataType.Int16` | `short` | 16-bit integer |
| `DataType.Int32` | `int` | 32-bit integer |
| `DataType.Int64` | `long` | 64-bit integer |
| `DataType.Decimal` | `decimal` | High-precision decimal |
| `DataType.Double` | `double` | Double-precision floating point |
| `DataType.Single` | `float` | Single-precision floating point |
| `DataType.Byte` | `byte` | 8-bit unsigned integer |
| `DataType.Char` | `char` | Single character |
| `DataType.UniqueIdentifier` | `Guid` | Globally unique identifier |
| `DataType.DateOnly` | `DateOnly` | Date without time |
| `DataType.TimeOnly` | `TimeOnly` | Time without date |
| `DataType.DateTime` | `DateTime` | Date and time |
| `DataType.DateTimeOffset` | `DateTimeOffset` | Date/time with timezone |
| `DataType.TimeSpan` | `TimeSpan` | Duration |
| `DataType.ByteArray` | `byte[]` | Binary data |

## Type Modifiers

### String Options

```csharp
DataType.String
    .WithMaxLength(100)      // Maximum length
    .WithMinLength(5)        // Minimum length
```

### Numeric Options

```csharp
DataType.Decimal
    .WithPrecision(18, 2)    // Precision and scale
```

### Temporal Options

```csharp
DataType.DateTimeOffset
    .IsTemporal()            // Mark as temporal/effective-dated
```

## Complex Types

### Entity Reference

Reference another entity:

```csharp
DataType.Entity(new Name("Address"))
```

The generator will create appropriate foreign key relationships.

### Enumeration

Reference an enumeration:

```csharp
DataType.Enum.Of(new Name("CustomerStatus"))
```

## Collections

Wrap any type in a collection:

```csharp
// List collection
DataType.String.AsCollection(CollectionTypes.List)

// Entity collection
DataType.Entity(new Name("OrderItem")).AsCollection(CollectionTypes.List)
```

### Collection Types

| Type | C# Equivalent |
|------|---------------|
| `CollectionTypes.None` | Single value |
| `CollectionTypes.List` | `IList<T>` |
| `CollectionTypes.Array` | `T[]` |
| `CollectionTypes.Enumerable` | `IEnumerable<T>` |

## Field Generation

Fields can be auto-generated:

```csharp
.AddField(new("Id"))
    .WithDataType(DataType.Int32)
    .Generated(ValueGeneratedTypes.Add)    // Generated on insert
```

### ValueGeneratedTypes

| Type | Description |
|------|-------------|
| `None` | Not auto-generated |
| `Add` | Generated on insert |
| `AddOrUpdate` | Generated on insert or update |
| `Update` | Generated on update |

## Examples

### Basic Entity with Various Types

```csharp
builder
    .AddEntity(new Name("Product"))
        .AddKey(new Name("Product"), 1)
            .AddField(new("ProductId"))
                .WithDataType(DataType.Int32)
                .Generated(ValueGeneratedTypes.Add)
            .And
        .And
        .AddField(new("Name"))
            .WithDataType(DataType.String.WithMaxLength(200))
            .AsBusinessKey()
        .And
        .AddField(new("Price"))
            .WithDataType(DataType.Decimal.WithPrecision(18, 2))
        .And
        .AddField(new("IsActive"))
            .WithDataType(DataType.Bool)
            .WithDefault("true")
        .And
        .AddField(new("Category"))
            .WithDataType(DataType.Enum.Of(new Name("ProductCategory")))
        .And
        .AddField(new("CreatedAt"))
            .WithDataType(DataType.DateTimeOffset)
            .Generated(ValueGeneratedTypes.Add)
        .And
    .And
```

### Entity with Relationships

```csharp
builder
    .AddEntity(new Name("Order"))
        // ... key fields ...
        .AddField(new("Customer"))
            .WithDataType(DataType.Entity(new Name("Customer")))
        .And
        .AddField(new("Items"))
            .WithDataType(DataType.Entity(new Name("OrderItem"))
                .AsCollection(CollectionTypes.List))
        .And
    .And
```

