---
title: "Overview"
---

# Overview

## Philosophy

A domain definition should describe **what the business cares about**, not how it's stored or transmitted. Technical concerns like database keys, serialization formats, and interface versioning are important but belong in separate layers.

```
┌─────────────────────────────────────────┐
│           DOMAIN DEFINITION             │  ← What the business understands
│  "A Booking is planned attendance..."   │
├─────────────────────────────────────────┤
│           PERSISTENCE LAYER             │  ← How it's stored
│  Keys, indexes, storage relationships   │
├─────────────────────────────────────────┤
│           INTEGRATION LAYER             │  ← How it's exposed
│  Interfaces, versions, contracts        │
└─────────────────────────────────────────┘
```

## Design Goals

### 1. Domain-First Modeling

Define concepts in business terms:

```
// Good - describes the business concept
entity Adult
    "A person responsible for children at the centre"

    attributes
        Name: text "Person's full name"
        Contact: Contact "How to reach them"
end

// Avoid - leaks persistence concerns
entity Adult
    attributes
        AdultId: int generated     // Technical ID
        FK_OrganisationId: int     // Foreign key
end
```

### 2. Natural Language Orientation

Definitions should read like documentation:

```
entity Booking
    """
    Planned attendance for a child at a centre for a session.
    Transitions through: planned → attending → attended (or absence).
    """

    attributes
        Date: date "When attendance is planned"
        Session: Session "The session being booked"
        Status: BookingStatus "Current state of the booking"

    belongs_to Child
end
```

> Note: What can be done (behaviours) is defined separately - see [Behaviours](03-behaviours.md).

### 3. AI Agent Compatibility

Structured for machine understanding:

- **Predictable grammar** - AI knows what keywords and structure to expect
- **Semantic keywords** - `attributes`, `belongs_to`, `involves` convey meaning
- **Self-documenting** - Every element can have a description
- **Queryable** - "What can be done with a Booking?" → parse related commands

### 4. Separation of Concerns

| File Type | Contains | Example |
|-----------|----------|---------|
| `.entity` | Domain structure | `booking.entity` |
| `.key` | Identity/persistence | `booking.key` |
| `.command` | Commands (mutations) | `cancel-booking.command` |
| `.query` | Queries (reads) | `get-bookings.query` |
| `.enum` | Enumerations | `booking-status.enum` |
| `.service` | Bounded context | `scheduling.service` |
| `.value` | Value objects | `address.value` |
| `.shared` | Lookup data | `country.shared` |
| `.event` | Domain events | `booking-created.event` |
| `.projection` | Read models | `booking-summary.projection` |

### 5. Version Support

Interface evolution without domain pollution:

```
// Domain stays clean
entity Booking
    attributes
        Status: BookingStatus "Current state"
end

// Versioning is separate - for when systems need different views
projection BookingV1
    version "v1"
    from Booking

    fields
        status: Status.name    // v1 returns name only
end

projection BookingV2
    version "v2"
    from Booking

    fields
        status: Status         // v2 returns full status object
end
```

## What This Enables

1. **Generate multiple outputs** from single source of truth
2. **AI can read and generate** valid definitions
3. **Business stakeholders** can review and discuss definitions
4. **Versioned interfaces** without domain model changes
5. **Rules engine integration** for business logic (future)
