---
title: "Data Types"
---

# Data Types

This document defines the type system for domain definitions.

---

## Primitive Types

| Type | Description | Example |
|------|-------------|---------|
| `text` | Variable-length string | `Name: text` |
| `text(N)` | String with max length | `Code: text(10)` |
| `integer` | 32-bit whole number | `Count: integer` |
| `long` | 64-bit whole number | `SizeBytes: long` |
| `decimal` | Arbitrary-precision decimal | `Rate: decimal` |
| `decimal(P,S)` | Decimal with precision and scale | `Rate: decimal(10,4)` |
| `boolean` | True or false | `IsActive: boolean` |
| `date` | Date without time | `StartDate: date` |
| `time` | Time without date | `OpenTime: time` |
| `datetime` | Date and time | `CreatedAt: datetime` |
| `guid` | Globally unique identifier | `Token: guid` |
| `name` | Smart name with casing variants | `FirstName: name` |
| `binary` | Raw binary data | `Data: binary` |
| `id` | Generated unique identifier (key fields only) | `ChildId: id` |
| `email` | Email address | `ContactEmail: email` |
| `url` | Web address | `WebsiteUrl: url` |

---

## Standard Library Types

These types are defined in the `lib/` standard library. Use them by name — the code generator resolves their canonical definition.

### Value Types

| Type | Description | Library file |
|------|-------------|-------------|
| `money` | ISO 4217 monetary amount — integer minor units + `text(3)` currency code | `lib/money.value` |
| `percentage` | Percentage value stored as `decimal(7,4)` | `lib/percentage.value` |
| `geospatial` | WGS84 coordinate pair — latitude and longitude as `decimal(10,7)` | `lib/geospatial.value` |

```
# Using standard library value types in an entity
entity RoomSessionFee
  "Fee schedule for a room session"

  Schedule1Amount: money "The schedule 1 fee amount"
  CCSPercentage: percentage "The CCS entitlement percentage"
end

entity Centre
  "A childcare centre"

  Name: name "Centre name"
  Location: geospatial, optional "Geographic coordinates"
end
```

### Union Types

Union types represent values with multiple possible shapes. The generator selects the appropriate variant based on infrastructure configuration.

| Type | Description | Variants | Library file |
|------|-------------|----------|-------------|
| `image` | Binary image — inline or external reference | `Embedded`, `Reference` | `lib/image.union` |
| `document` | Binary document — inline or external reference | `Embedded`, `Reference` | `lib/document.union` |

```
# Using union types in an entity
entity Child
  "A child enrolled at a centre"

  FirstName: name "First name"
  Image: image, optional "Child's photo"
end

entity CertificateSupportingEvidenceDocument
  "A supporting document for an ACCS certificate"

  Document: document "The attached evidence document"
end
```

The `Embedded` variant stores binary data directly in the data store (supported by SQL Server). The `Reference` variant stores a storage key pointing to an external blob store. Infrastructure rules determine which variant is used at generation time.

---

## Defining Your Own Types

### Value Objects (`.value`)

User-defined immutable value objects composed of primitives:

```
value DateRange
  "A period between two dates"

  Start: date "Start of the range"
  End: date "End of the range (inclusive)"
end
```

### Union Types (`.union`)

User-defined discriminated unions with two or more named variants:

```
union ContactMethod
  "How to contact a person"

  variant Email
    Address: email "Email address"
  end

  variant Phone
    Number: text(20) "Phone number"
    Extension: text(10), optional "Extension"
  end
end
```

---

## Field Modifiers

| Modifier | Syntax | Meaning |
|----------|--------|---------|
| Optional | `, optional` | Field may be null/absent |
| Default value | `, default(value)` | Value used when not supplied |

```
entity Booking
  "A planned attendance"

  Status: BookingStatus, default(Planned) "Current state"
  Notes: text(500), optional "Free text notes"
  IsActive: boolean, default(true) "Whether the booking is active"
end
```

---

## The `id` Type

`id` is a shorthand for a generated unique identifier used exclusively in `.key` files. It implies `guid, generated` — the field is a GUID and is automatically assigned on creation.

```
key Child
  ChildId: id
end
```

Both forms are equivalent; `id` is preferred in new files:

```
# equivalent (older style)
key Child
  ChildId: guid, generated
end
```

---

## Implementation Mapping

| DSL Type | C# | SQL Server | SQLite |
|----------|----|------------|--------|
| `text` | `string` | `nvarchar(max)` | `TEXT` |
| `text(N)` | `string` | `nvarchar(N)` | `TEXT` |
| `integer` | `int` | `int` | `INTEGER` |
| `long` | `long` | `bigint` | `INTEGER` |
| `decimal(P,S)` | `decimal` | `decimal(P,S)` | `NUMERIC` |
| `boolean` | `bool` | `bit` | `INTEGER` |
| `date` | `DateOnly` | `date` | `TEXT` |
| `time` | `TimeOnly` | `time` | `TEXT` |
| `datetime` | `DateTime` | `datetime2` | `TEXT` |
| `guid` | `Guid` | `uniqueidentifier` | `TEXT` |
| `name` | `string` | `nvarchar(100)` | `TEXT` |
| `binary` | `byte[]` | `varbinary(max)` | `BLOB` |
| `id` | `Guid` | `uniqueidentifier` | `TEXT` |
| `email` | `string` | `nvarchar(254)` | `TEXT` |
| `url` | `string` | `nvarchar(2048)` | `TEXT` |
| `money` | `Money` (value object) | two columns: `bigint` + `char(3)` | two columns |
| `percentage` | `Percentage` (value object) | `decimal(7,4)` | `NUMERIC` |
| `geospatial` | `Geospatial` (value object) | two columns: `decimal(10,7)` × 2 | two columns |
| `image` | `Image` (union) | variant-dependent | variant-dependent |
| `document` | `Document` (union) | variant-dependent | variant-dependent |
