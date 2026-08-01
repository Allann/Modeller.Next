---
title: "Modeller DSL Reference"
---

# Modeller DSL Reference

This is the authoritative reference for all Modeller DSL file formats. Every syntax form shown here is what the parser actually accepts. This document is intended for both human authors and LLMs generating or validating domain definitions.

---

## Common Syntax Rules

### Identifiers

Identifiers must start with a letter or underscore, followed by letters, digits, or underscores. In practice, domain names use PascalCase.

```
ValidName
BookingStatus
RPC_SubmitSessionReport
_internal
```

### Descriptions

Any top-level construct and any attribute can carry an optional quoted description. Descriptions are written on the same line as the thing they describe, after the name or type.

```
entity Booking
  "Planned attendance for a child at a centre for a session"

  Date: date "When attendance is planned"
  Status: BookingStatus "Current state"
end
```

### Comments

Line comments start with `#` and run to end of line.

```
# This is a comment
entity Child  # inline comment
  FirstName: name "First name"
end
```

### Whitespace

Indentation is cosmetic — the parser does not enforce it. All blocks are delimited by keywords and closed with `end`, not by indentation.

### Blocks

All multi-line constructs close with `end`:

```
entity Foo
  ...
end
```

---

## File Types

| Extension | Contains |
|-----------|----------|
| `.def` | Domain root — name, company, version, service list |
| `.entity` | Entity definition — attributes and relationships |
| `.key` | Identity definition — primary key and indexes |
| `.value` | Value object — immutable, no identity |
| `.union` | Discriminated union — multiple named variant shapes |
| `.enum` | Enumeration — named integer values |
| `.flags` | Bit flags — combinable named power-of-two values |
| `.shared` | Shared/lookup data — read-only external projection |
| `.service` | Bounded context — entity ownership and dependencies |
| `.command` | Command — business action that changes state |
| `.query` | Query — read operation that returns data |
| `.event` | Domain event — something that happened |
| `.projection` | Projection — query return shape |

---

## `.def` — Domain Definition

The root file. One per domain.

```
domain ChildCare
  "Comprehensive domain model for managing child care centres and families"

  company "Acme"
  version "1.0.0"

  services
    Scheduling
    Billing
    Organisation
  end
end
```

### Keywords

| Keyword | Required | Description |
|---------|----------|-------------|
| `domain` | Yes | Opens the definition; followed by the domain name |
| `company` | No | Company or namespace string |
| `version` | No | Semantic version string |
| `services` | No | Block listing service names (one per line) |
| `end` | Yes | Closes the `services` block and the `domain` block |

---

## `.entity` — Entity

An entity has identity and a lifecycle. Attributes and relationships are listed directly in the body — no nested `attributes` block.

```
entity Booking
  "Planned attendance for a child at a centre for a session"

  BookingDate: date "Date when attendance is planned"
  Status: BookingStatus "Current state of the booking"
  AdjustedHours: decimal(12,2), optional "Adjusted hours after modifications"
  IsActive: boolean, default(true) "Whether the booking is active"

  belongs_to Child
  has_one Session
  has_many Attendance
  has_one Absence?
  references CentreForScheduling
end
```

### Attribute Syntax

```
FieldName: DataType[, optional][, default(value)] ["Description"]
```

- Multiple modifiers can be combined: `Foo: text, optional, default(bar)`
- `optional` — field may be null
- `default(value)` — value used when not supplied; value can be an identifier, quoted string, or `true`/`false`

### Relationship Syntax

```
keyword TargetName ["Description"]
keyword Alias: TargetName ["Description"]
```

| Keyword | Meaning |
|---------|---------|
| `has_one X` | Single related entity in the same service |
| `has_one X?` | Optional single related entity (`?` suffix) |
| `has_many X` | Collection of related entities in the same service |
| `belongs_to X` | Parent entity — lifecycle tied to parent |
| `many_to_many X` | Many-to-many association |
| `references X` | Cross-service read dependency — reads from a `.shared` type |

An alias gives the relationship a different name from the target type:

```
has_many Attendances: Attendance "All recorded attendances for this booking"
```

---

## `.key` — Key Definition

One per entity. Defines the primary key fields and optional database indexes.

```
key Booking
  BookingId: id

  index [Child, Date, Session] unique
end
```

### Key Field Types

| Type | Meaning |
|------|---------|
| `id` | Shorthand for `guid, generated` — auto-assigned GUID |
| `guid` | A GUID (must add `, generated` if auto-assigned) |
| `integer` | Integer key |
| `text(N)` | String key |

### Index Syntax

```
index [Field1, Field2, ...] unique
index [Field1, Field2, ...]
```

`unique` is optional. Multiple index lines are allowed.

---

## `.value` — Value Object

An immutable object with no identity. Equality is by all attributes.

```
value DateRange
  "A period between two dates"

  Start: date "Start of the range (inclusive)"
  End: date "End of the range (inclusive)"
end
```

Attributes use the same syntax as entity attributes (same types, `optional`, `default`). Value objects cannot have relationships.

---

## `.union` — Discriminated Union

A type with multiple possible shapes (variants). Only one variant is active at a time. The code generator selects the variant based on infrastructure configuration.

```
union ContactMethod
  "How to reach a person"

  variant Email
    "Contact via email"
    Address: email "Email address"
  end

  variant Phone
    "Contact via phone"
    Number: text(20) "Phone number"
    Extension: text(10), optional "Extension"
  end
end
```

### Keywords

| Keyword | Description |
|---------|-------------|
| `union` | Opens the union type definition |
| `variant` | Opens a named variant block; each variant has its own attributes |
| `end` | Closes a `variant` block or the `union` block |

---

## `.enum` — Enumeration

A fixed set of named integer values.

```
enum BookingStatus
  "Possible states of a booking"

  Planned: 1 "Booking is scheduled for the future"
  Attending: 2 "Child has signed in"
  Attended: 3 "Child has signed out"
  Absence: 4 "Child did not attend"
  Cancelled: 5 "Booking was cancelled"
end
```

### Value Syntax

```
ValueName: IntegerValue ["Description"]
```

Values can be any non-negative integer. By convention, start from 1.

---

## `.flags` — Bit Flags

Like an enum but values can be combined. Values must be powers of two (1, 2, 4, 8, 16, …).

```
flags DaysOfWeek
  "Set of days — multiple values can be selected simultaneously"

  Monday: 1 "Monday"
  Tuesday: 2 "Tuesday"
  Wednesday: 4 "Wednesday"
  Thursday: 8 "Thursday"
  Friday: 16 "Friday"
  Saturday: 32 "Saturday"
  Sunday: 64 "Sunday"
end
```

---

## `.shared` — Shared / Lookup Data

Read-only data owned by another bounded context or shared globally. There are two kinds:

**Global lookup table** — static reference data not owned by any specific service:

```
shared AbsenceReason
  "Standard reasons for child absences"

  Code: text(10) "Short code"
  Description: text(100) "Display name"
  IsChargeable: boolean "Whether this reason incurs charges"
end
```

**Per-consumer projection** — a bounded view of another service's entity containing only the fields this consumer needs. Named using the convention `{Entity}For{Consumer}`:

```
shared OrganisationForScheduling
  "Organisation reference data as used by the Scheduling service"

  Name: name "Organisation name"
  PhoneNumber: text(20), optional "Contact number"
end
```

Attributes use the same syntax as entity attributes.

---

## `.service` — Service Definition

A bounded context. Groups entities and enums it owns, declares cross-service data it reads, and lists RPC operations it calls or implements.

```
service Scheduling
  "Manages bookings, attendance, and sessions"

  entities
    Booking
    Attendance
    Session
    Room
  end

  enums
    BookingStatus
    CareType
  end

  references
    ChildForScheduling
    CentreForScheduling
    OrganisationForScheduling
  end

  calls
    FileStorageRPC
    CreateUserNotification
  end

  implements
    Gov_Submit_SessionReport
  end
end
```

### Blocks

| Block | Description |
|-------|-------------|
| `entities` | Entity names owned by this service |
| `enums` | Enum and flags names owned by this service |
| `references` | Per-consumer shared type names this service reads from other contexts |
| `calls` | Command/query names this service invokes on other services (as a client) |
| `implements` | Command/query names this service handles (as the provider) |

All blocks are optional. Each block contains one name per line and closes with `end`.

---

## `.command` — Command

A business action that changes state.

```
command PlaceBooking
  "Place a booking for a child at a session"

  input
    ChildId: id "The child to book"
    SessionId: id "The session being booked"
    Date: date "The booking date"
    Notes: text(500), optional "Additional notes"
  end

  output
    BookingResult "The created booking summary"
  end

  errors
    ChildNotEnrolled "Child is not enrolled at the centre"
    SessionFull "No capacity remaining in the session"
  end

  publishes
    BookingPlaced
  end
end
```

### gRPC Command

```
command GovSubmitSessionReport
  "Submits session reports to government"
  transport grpc

  input
    Request: RPC_SubmitSessionReportRequest "The request payload"
  end

  output
    RPC_SubmitSessionReportResponse "The response payload"
  end
end
```

### Streaming Command

```
command StreamAttendanceUpdates
  "Stream attendance updates as they occur"
  transport grpc
  streaming server

  input
    CentreId: id "Centre to watch"
  end

  output
    AttendanceUpdate "Individual attendance event"
  end
end
```

### Keywords

| Keyword | Description |
|---------|-------------|
| `command` | Opens the definition |
| `transport` | Wire protocol: `http` (default) or `grpc` |
| `streaming` | Streaming mode: `none` (default), `server`, `client`, `bidirectional` |
| `input` | Block of input attributes (same attribute syntax as entities) |
| `output` | The return type name and optional description |
| `errors` | Block of named error conditions — one per line: `ErrorName "Description"` |
| `publishes` | Block of event names published on success — one name per line |
| `end` | Closes each block and the command |

`transport` and `streaming` are optional single-line modifiers, not blocks. They do not need `end`.

---

## `.query` — Query

A read operation. Does not change state.

```
query GetActualBookings
  "Bookings combined with attendance and absence data"

  input
    CentreToken: text "The centre identifier"
    Date: date "The date to query"
    IncludeAbsences: boolean, default(true) "Whether to include absence records"
  end

  returns ActualBookingResult
end
```

### Returning a Collection

```
query ListChildren
  "All children enrolled at a centre"

  input
    CentreId: id "The centre to query"
  end

  returns many Child
end
```

`returns many TypeName` signals the query returns a collection.

### Keywords

| Keyword | Description |
|---------|-------------|
| `query` | Opens the definition |
| `transport` | Wire protocol: `http` (default) or `grpc` |
| `streaming` | Streaming mode: `none` (default), `server`, `client`, `bidirectional` |
| `input` | Block of input attributes |
| `returns` | Return type — `returns TypeName` or `returns many TypeName` |
| `end` | Closes each block and the query |

---

## `.event` — Domain Event

Something significant that happened. Named in past tense.

```
event AttendanceRecorded
  "A child's attendance has been recorded for a booked session"

  BookingId: id "The booking attendance was recorded against"
  ChildId: id "The child who attended"
  TimeIn: time "When the child arrived"
  TimeOut: time, optional "When the child left"
  RecordedAt: datetime "When this record was created"
end
```

Attributes use the same syntax as entity attributes.

---

## `.projection` — Projection

A read model shaped for a specific query purpose. May combine fields from multiple entities.

```
projection AttendanceReport
  "Attendance data flattened for reporting"

  ChildName: name "Child's full name"
  Date: date "Attendance date"
  SessionName: text(100) "Session name"
  TimeIn: time "Sign-in time"
  TimeOut: time, optional "Sign-out time"
  CentreName: text(60) "Centre name"
  IsAbsent: boolean "Whether this was an absence"
end
```

Attributes use the same syntax as entity attributes.

---

## Data Types

### Primitive Types

| Type | Description |
|------|-------------|
| `text` | Variable-length string (unbounded) |
| `text(N)` | String with maximum length N |
| `integer` | 32-bit whole number |
| `long` | 64-bit whole number |
| `decimal` | Arbitrary-precision decimal |
| `decimal(P,S)` | Decimal with precision P and scale S |
| `boolean` | True or false |
| `date` | Date without time |
| `time` | Time without date |
| `datetime` | Date and time |
| `guid` | Globally unique identifier |
| `name` | Smart name type with casing variants (maps to `nvarchar(100)`) |
| `binary` | Raw binary data |
| `id` | Generated unique identifier — key fields only; shorthand for `guid, generated` |
| `email` | Email address string |
| `url` | Web address string |

### Standard Library Types

These are defined in `lib/` and used by name — the code generator resolves their structure.

**Value types** (defined in `.value` files):

| Type | Structure | Library file |
|------|-----------|-------------|
| `money` | `Amount: integer` + `Currency: text(3)` (ISO 4217) | `lib/money.value` |
| `percentage` | `Value: decimal(7,4)` | `lib/percentage.value` |
| `geospatial` | `Latitude: decimal(10,7)` + `Longitude: decimal(10,7)` (WGS84) | `lib/geospatial.value` |

**Union types** (defined in `.union` files, variant selected by infrastructure config):

| Type | Variants | Library file |
|------|----------|-------------|
| `image` | `Embedded` (binary inline) / `Reference` (storage key) | `lib/image.union` |
| `document` | `Embedded` (binary inline) / `Reference` (storage key) | `lib/document.union` |

Usage:

```
entity Child
  FirstName: name "First name"
  Image: image, optional "Child's photo"
end

entity RoomSessionFee
  Amount: money "The fee amount"
  CCSRate: percentage "The CCS subsidy rate"
end

entity Centre
  Location: geospatial, optional "Geographic coordinates"
end
```

### Implementation Mapping

| DSL Type | C# | SQL Server | SQLite |
|----------|----|------------|--------|
| `text` | `string` | `nvarchar(max)` | `TEXT` |
| `text(N)` | `string` | `nvarchar(N)` | `TEXT` |
| `integer` | `int` | `int` | `INTEGER` |
| `long` | `long` | `bigint` | `INTEGER` |
| `decimal` | `decimal` | `decimal(18,6)` | `NUMERIC` |
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
| `money` | `Money` (value object) | `bigint` + `char(3)` | two columns |
| `percentage` | `Percentage` (value object) | `decimal(7,4)` | `NUMERIC` |
| `geospatial` | `Geospatial` (value object) | two `decimal(10,7)` columns | two columns |
| `image` | `Image` (union) | variant-dependent | variant-dependent |
| `document` | `Document` (union) | variant-dependent | variant-dependent |

---

## Quick Reference — All Keywords

### Top-Level Declarations

```
domain   entity   key   value   union   enum   flags
shared   service  command   query   event   projection
```

### Block Keywords

```
services   entities   enums   references   calls   implements
input   output   errors   publishes   returns   index
variant
```

### Relationship Keywords

```
has_one   has_many   belongs_to   many_to_many   references
```

### Field Modifiers

```
optional   default(value)   generated
```

### Transport / Streaming (commands and queries)

```
transport http        # default — REST over HTTP
transport grpc        # gRPC binary protocol

streaming none        # default — request/response
streaming server      # server streams multiple responses
streaming client      # client streams multiple requests
streaming bidirectional
```

### Service Content Keywords

```
company   version                       # .def metadata
index [Fields, ...] unique             # .key indexes
returns   returns many                 # .query return type
```

### Block Terminator

```
end
```

---

## Structural Conventions

### File Naming

Files use kebab-case matching the defined type name:

| Type name | File name |
|-----------|-----------|
| `BookingStatus` | `booking-status.enum` |
| `PlaceBooking` | `place-booking.command` |
| `OrganisationForScheduling` | `organisation-for-scheduling.shared` |

### Per-Consumer Shared Files

Cross-service projections follow the naming convention `{Entity}For{Consumer}`:

```
# shared/organisation-for-scheduling.shared
shared OrganisationForScheduling
  "Organisation data as needed by the Scheduling service"

  Name: name "Organisation name"
end
```

The consuming service lists this in its `references` block:

```
service Scheduling
  references
    OrganisationForScheduling
  end
end
```

Entities reference the shared type using `references`:

```
entity Booking
  references OrganisationForScheduling
end
```

### RPC Commands

Service-to-service operations use `transport grpc`. The consuming service lists the command in its `calls` block; the providing service lists it in `implements`:

```
# Consuming service
service CCSSOrchestration
  calls
    GovSubmitSessionReport
  end
end

# Providing service
service GovSyncCore
  implements
    GovSubmitSessionReport
  end
end
```
