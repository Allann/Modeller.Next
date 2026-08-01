---
title: "AI Agent Integration"
---

# AI Agent Integration

This document describes how the domain definition format is designed for AI agent consumption and generation.

## Design for AI

### Predictable Grammar

AI agents work best with consistent, predictable structures:

```
// Every entity follows the same pattern
entity [Name]
    "[Description]"

    attributes
        [FieldName]: [DataType] "[Description]"
        [FieldName]: [DataType]? "[Optional field description]"

    has_one [EntityName] "[Description]"
    has_many [EntityName] as [CollectionName] "[Description]"

    belongs_to [ParentEntity]
end
```

> Note: What can be done with an entity is defined in separate behaviour files (commands, queries).

### Semantic Keywords

Keywords convey meaning that AI can understand:

| Keyword | Semantic Meaning |
|---------|------------------|
| `entity`, `value`, `shared` | Type of domain concept |
| `attributes` | Data the type holds |
| `belongs_to` | Parent relationship |
| `has_one` | Single relationship |
| `has_many` | Collection relationship |
| `involves` | Entities participating in a behaviour |
| `uses` | Reference data fields |
| `publishes` | Events produced |
| `changes` | State mutations |
| `?` suffix | Optional/nullable |

### Self-Documenting

Every element can have a description (in quotes), enabling AI to:
- Explain what concepts mean
- Generate appropriate implementations
- Answer questions about the domain

---

## AI Capabilities

### 1. Domain Understanding

AI can answer questions about the domain:

```
Q: "What is a Booking?"
A: Parse entity.description → "Planned attendance for a child..."

Q: "What can you do with a Booking?"
A: Parse related commands → "PlaceBooking, CancelBooking, RecordAttendance, RecordAbsence"

Q: "What does a Booking contain?"
A: Parse entity.attributes → "Date, Status, Session times..."
```

### 2. Definition Generation

AI can generate definitions from natural language:

```
User: "I need to track vehicle maintenance. A vehicle belongs to a
       depot and can have scheduled services. Each service has a
       date, type, and cost."

AI generates:
- vehicle.entity
- maintenance-service.entity
- service-type.enum
- schedule-service.command
- get-vehicle-services.query
```

### 3. Validation

AI can validate definitions for:
- Required fields present
- Relationship consistency
- Naming conventions
- Description completeness

### 4. Code Generation Guidance

AI understands what code to generate from behaviours:

```
command CancelBooking
    "Cancels a booking before attendance is recorded"
    owner Booking

    // Generates:
    //   → CancelBooking command handler
    //   → BookingCancelled event
    //   → Booking.Cancel() domain method
end
```

---

## Query Interface

Structured queries AI can execute against definitions:

```
// List all entities in a service
query
    type entities
    service Scheduling

// Find commands that affect an entity
query
    type commands
    affects Booking

// Get relationships for an entity
    type relationships
    entity Booking

// Find all events in domain
query
    type events

// Search by description
query
    type search
    text "attendance"
```

---

## Conversation Patterns

### Exploring the Domain

```
User: "Describe the scheduling domain"
AI: [Reads scheduling.service]
    "The Scheduling service manages bookings, attendance, and sessions.
     It owns entities: Booking, Attendance, Absence, Session, Room.
     Key operations include placing bookings and recording attendance."

User: "How does attendance work?"
AI: [Reads booking.entity, record-attendance.command]
    "When a child arrives, the RecordAttendance command is used.
     It requires the booking, arrival time, and the adult who signed in.
     This changes the booking status to 'Attending' and creates an
     Attendance record."
```

### Modifying the Domain

```
User: "Add a notes field to Booking"
AI: [Modifies booking.entity]
    "I've added an optional Notes attribute to Booking:
     Notes: text(500)? 'Additional notes about the booking'"

User: "Bookings should be able to be rescheduled"
AI: [Creates reschedule-booking.command]
    "I've created a RescheduleBooking command with:
     - Input: Booking, NewDate, NewSession, Reason
     - Changes: Booking.Date, Booking.Session
     - Publishes: BookingRescheduled"
```

---

## Tooling Support

The DSL is parsed using [Pidgin](https://github.com/benjamin-hodgson/Pidgin) parser combinators, which provides:
- Precise error messages with line/column information
- Consistent parsing across all definition types
- Easy extension for new syntax

The VS Code extension provides:
- Syntax highlighting for all file types
- Custom file icons for visual identification
- Language configuration for comments and brackets

> Note: Behaviours (what can be done with an entity) are defined in separate command/query files, not in the entity definition.

This enables:
- IDE syntax highlighting
- Parser-based validation
- AI-assisted editing
