---
title: "Domain Concepts"
---

# Domain Concepts

The domain layer consists of four primary building blocks: **Entities**, **Value Objects**, **Union Types**, and **Shared Data**.

---

## Entities

An entity has **identity** and a **lifecycle**. Two entities with the same attributes are still different if they have different identities.

### Characteristics

- Has a unique identity (defined separately in a `.key` file)
- Has a lifecycle (created, modified, potentially deleted)
- Mutable — state can change over time
- Equality based on identity, not attributes

### Definition Structure

```
entity Booking
  "Planned attendance for a child at a centre for a session"

  Date: date "When attendance is planned"
  Status: BookingStatus "Current state of the booking"
  AdjustedHours: decimal, optional "Total adjusted hours after modifications"

  has_one Session "The session being booked"
  has_many Attendance "Recorded attendance instances"
  has_one Absence? "Absence record if child did not attend"
  belongs_to Child
  references CentreForScheduling "The centre where the booking occurs"
end
```

### Relationship Keywords

| Keyword | Meaning |
|---------|---------|
| `has_one X` | Single related entity in the same service |
| `has_one X?` | Optional single related entity in the same service |
| `has_many X` | Collection of related entities in the same service |
| `belongs_to X` | Parent entity — this entity's lifecycle is tied to it |
| `references X` | Cross-service read dependency — reads from another bounded context's shared type |

### Optional Fields

Add `, optional` after the type to mark a field as nullable:

```
entity Child
  "A child enrolled at a centre"

  FirstName: name "First name"
  MiddleName: name, optional "Middle name"
  DateOfBirth: date, optional "Date of birth"
  Image: image, optional "Child's photo"
end
```

---

## Value Objects

A value object has **no identity**. It is defined entirely by its attributes. Two value objects with the same attributes are equal. Value objects are immutable — if you need to change one, you replace it.

### Characteristics

- No unique identity
- Immutable once created
- Equality based on all attributes
- Embedded within entities or other value objects

### Definition Structure

```
value Address
  "A physical or postal address"

  Street: text(100) "Street number and name"
  Suburb: text(60) "Suburb or city"
  State: text(3) "State or territory code"
  PostCode: text(4) "Postal code"
  Country: text(60), default("Australia") "Country name"
end
```

### Standard Library Value Types

The standard library provides pre-defined value types for common domain concepts:

| Type | Description |
|------|-------------|
| `money` | ISO 4217 monetary amount — integer minor units + `text(3)` ISO currency code |
| `percentage` | Percentage stored as `decimal(7,4)` |
| `geospatial` | WGS84 coordinate pair — latitude and longitude |

Use these directly as field types:

```
entity RoomSessionFee
  "Fee schedule for a room session"

  Amount: money "The scheduled fee"
  CCSRate: percentage "The CCS subsidy rate"
end

entity Centre
  "A childcare centre"

  Name: name "Centre name"
  Location: geospatial, optional "Geographic coordinates"
end
```

---

## Union Types

A union type has **multiple possible shapes** (variants). Only one variant is active at a time. Unlike a value object, you cannot know the full structure of a union field without knowing which variant it holds.

### Characteristics

- One of several named variants is active
- Each variant has its own set of attributes
- The generator selects the appropriate variant based on infrastructure configuration
- Useful for types whose storage strategy varies (e.g. inline vs. external storage)

### Standard Library Union Types

| Type | Variants | Use case |
|------|----------|----------|
| `image` | `Embedded` (binary inline) / `Reference` (storage key) | Photos, thumbnails |
| `document` | `Embedded` (binary inline) / `Reference` (storage key) | PDFs, attachments |

```
entity Child
  "A child enrolled at a centre"

  FirstName: name "First name"
  Image: image, optional "Child's photo"
end

entity CertificateSupportingEvidenceDocument
  "Supporting evidence document for an ACCS certificate"

  Document: document "The attached evidence file"
  UploadedAt: datetime "When the document was uploaded"
end
```

### Defining Custom Union Types

```
# unions/contact-method.union
union ContactMethod
  "How to reach a person"

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

## Shared Data

Shared data is **owned by another bounded context** but used within this one. It is read-only from this context's perspective.

### Two kinds of shared data

**Global lookup tables** — static reference data shared across the whole domain, such as countries, states, or reason codes. These do not belong to any specific service:

```
shared AbsenceReason
  "Standard reasons for child absences"

  Code: text(10) "Short code"
  Description: text(100) "Display name"
  IsChargeable: boolean "Whether this reason incurs charges"
end
```

**Per-consumer projections** — a bounded view of another service's entity, containing only the fields this consumer needs. Named using the convention `{Entity}For{Consumer}`:

```
shared OrganisationForScheduling
  "Organisation reference data as used by the Scheduling service"

  Name: name "Organisation name"
  PhoneNumber: text(20), optional "Contact number"
end
```

### Declaring cross-service dependencies

Services declare which per-consumer shared types they consume in a `references` block:

```
service Scheduling
  "Manages bookings, attendance, and sessions"

  entities
    Booking
    Attendance
    Session
  end

  references
    OrganisationForScheduling
    CentreForScheduling
    ChildForScheduling
  end
end
```

Entities then use the `references` keyword to declare the cross-service relationship:

```
entity Booking
  Date: date "Booking date"
  Status: BookingStatus "Current state"

  belongs_to Child
  references CentreForScheduling
end
```

---

## Comparison

| Aspect | Entity | Value Object | Union | Shared Data |
|--------|--------|--------------|-------|-------------|
| Identity | Has unique ID (in `.key` file) | No ID | No ID | External ID |
| Mutability | Mutable | Immutable | Immutable | Read-only |
| Lifecycle | Managed here | Created/replaced | Created/replaced | Managed elsewhere |
| Ownership | Owned by this service | Embedded in owner | Embedded in owner | External |
| Equality | By identity | By all attributes | By all attributes | By external ID |
| Shape | Fixed | Fixed | Variant-dependent | Fixed projection |

---

## Enumerations

Enumerations define a fixed set of named integer values.

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

### Flags

Flags are enumerations whose values can be combined (bitwise). Values must be powers of two.

```
flags DaysOfWeek
  "Set of days — multiple values can be selected"

  Monday: 1 "Monday"
  Tuesday: 2 "Tuesday"
  Wednesday: 4 "Wednesday"
  Thursday: 8 "Thursday"
  Friday: 16 "Friday"
  Saturday: 32 "Saturday"
  Sunday: 64 "Sunday"
end
```
