---
title: "File Structure"
---

# File Structure

Domain definitions are organised in a hierarchical file structure that separates concerns and enables modular composition.

---

## Directory Layout

```
domain/
├── domain.def                       # Domain metadata and service list
│
├── services/                        # Bounded contexts
│   ├── scheduling.service
│   ├── billing.service
│   └── organisation.service
│
├── entities/                        # Domain entities
│   ├── booking.entity
│   ├── attendance.entity
│   └── child.entity
│
├── keys/                            # Identity definitions (one per entity)
│   ├── booking.key
│   ├── attendance.key
│   └── child.key
│
├── values/                          # Value objects
│   ├── address.value
│   ├── date-range.value
│   └── person-name.value
│
├── shared/                          # Shared/lookup data
│   ├── absence-reason.shared        # Global lookup table
│   └── organisation-for-scheduling.shared  # Per-consumer cross-service view
│
├── behaviours/                      # Commands and queries
│   ├── place-booking.command
│   ├── cancel-booking.command
│   ├── get-bookings.query
│   └── get-actual-bookings.query
│
├── events/                          # Domain events
│   ├── booking-placed.event
│   └── attendance-recorded.event
│
├── enums/                           # Enumerations
│   ├── booking-status.enum
│   └── care-type.enum
│
├── flags/                           # Bitwise flags
│   └── days-of-week.flags
│
├── projections/                     # Query return shapes
│   ├── actual-booking-result.projection
│   └── invoice-summary.projection
│
└── unions/                          # Discriminated union types (optional, user-defined)
    └── contact-method.union
```

---

## File Types

| Extension | Purpose | Contains |
|-----------|---------|----------|
| `.def` | Domain definition | Domain metadata, version, service list |
| `.entity` | Entity definition | Attributes, relationships |
| `.key` | Entity identity | Primary key fields, indexes |
| `.value` | Value object | Immutable attributes, no identity |
| `.union` | Discriminated union | Two or more named variants, each with attributes |
| `.shared` | Shared/lookup data | Read-only external data projection |
| `.service` | Bounded context | Groups entities, enums, cross-service references |
| `.command` | Command | Input, output, events published |
| `.query` | Query | Input parameters, return type |
| `.event` | Domain event | Event payload attributes |
| `.enum` | Enumeration | Named integer values |
| `.flags` | Flags | Bitwise combinable values |
| `.projection` | Projection | Query return shape |

The standard library in `lib/` provides pre-defined `.value` and `.union` files for commonly used types (`money`, `percentage`, `geospatial`, `image`, `document`).

---

## Domain Definition (`.def`)

```
# child-care.def
domain ChildCare
  "Domain model for childcare centre management"

  company "Acme"
  version "1.0.0"

  services
    Scheduling
    Billing
    Organisation
    Family
  end
end
```

---

## Service Definition (`.service`)

```
# services/scheduling.service
service Scheduling
  "Manages bookings, attendance, and sessions"

  entities
    Booking
    Attendance
    Absence
    Session
    Room
  end

  enums
    BookingStatus
    CareType
    AbsenceType
  end

  references
    ChildForScheduling
    CentreForScheduling
    AdultForScheduling
  end
end
```

The `references` block lists the per-consumer shared types this service reads from other bounded contexts. Each name corresponds to a `.shared` file.

---

## Entity Definition (`.entity`)

```
# entities/booking.entity
entity Booking
  "Planned attendance for a child at a session"

  Date: date "When attendance is planned"
  Status: BookingStatus "Current state of the booking"
  Notes: text(500), optional "Free text notes"

  has_one Session
  has_many Attendance
  has_one Absence?
  belongs_to Child
  references CentreForScheduling
end
```

---

## Key Definition (`.key`)

```
# keys/booking.key
key Booking
  BookingId: id

  index [Child, Date, Session] unique
end
```

The `id` type is shorthand for a generated GUID. The `index` block declares database indexes.

---

## Value Object (`.value`)

```
# values/date-range.value
value DateRange
  "A period between two dates"

  Start: date "Start of the range"
  End: date "End of the range (inclusive)"
end
```

---

## Union Type (`.union`)

```
# lib/image.union  (standard library)
union Image
  "A binary image stored inline or by external reference"

  variant Embedded
    Data: binary "Raw image bytes"
    MimeType: text(100) "MIME content type"
    FileName: text(255) "Original filename"
    SizeBytes: long "File size in bytes"
  end

  variant Reference
    StorageKey: text(500) "External storage key or URI"
    MimeType: text(100) "MIME content type"
    FileName: text(255) "Original filename"
    SizeBytes: long "File size in bytes"
  end
end
```

The generator selects the appropriate variant based on the target infrastructure.

---

## Shared Data (`.shared`)

Two kinds of shared files exist:

### Global lookup tables

Static reference data used across the whole domain:

```
# shared/absence-reason.shared
shared AbsenceReason
  "Standard reasons for child absences"

  Code: text(10) "Short code"
  Description: text(100) "Display name"
  IsChargeable: boolean "Whether this reason incurs charges"
end
```

### Per-consumer cross-service projections

A bounded view of another service's entity, containing only the fields this consumer needs:

```
# shared/organisation-for-scheduling.shared
shared OrganisationForScheduling
  "Organisation reference as used by the Scheduling service"

  Name: name "Organisation name"
  PhoneNumber: text(20), optional "Contact number"
end
```

Named using the convention `{entity}-for-{consumer}.shared`.

---

## Separation of Domain and Persistence

Entity files define the domain model. Key files define persistence identity. This separation allows:

- Domain stays clean of persistence concerns
- Keys can vary by storage technology
- Code generators combine both as needed

```
# Domain (entities/booking.entity)
entity Booking
  Date: date "When attendance is planned"
  Status: BookingStatus "Current state"

  belongs_to Child
  has_one Session
end

# Persistence (keys/booking.key)
key Booking
  BookingId: id

  index [Child, Date] unique
end
```
