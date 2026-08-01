---
title: "Glossary"
---

# Glossary

Key terms used throughout the domain definition language.

---

## Domain Building Blocks

### Entity
A thing with identity and a lifecycle. Two entities with the same attributes but different identities are different things. Identity is defined separately in a `.key` file.

> *Example: Two children named "John Smith" born on the same day are still different children — they have different `ChildId` values.*

### Value Object
A thing defined entirely by its attributes. Has no identity. Immutable — if you need to change it, you replace it.

> *Example: An address. If you move, you don't update the address — you have a new address.*

### Union Type
A value with multiple possible shapes (variants). Only one variant is active at a time. The generator selects the appropriate variant based on infrastructure configuration.

> *Example: An `image` can be stored as binary bytes (`Embedded`) or as a reference to an external storage service (`Reference`), depending on the target database.*

### Standard Library
A set of pre-defined `.value` and `.union` files shipped with Modeller in the `lib/` folder. These define canonical representations for commonly-used domain concepts such as monetary amounts, percentages, geographic coordinates, images, and documents. Use them by type name — the generator resolves their structure automatically.

> *Examples: `money`, `percentage`, `geospatial`, `image`, `document`.*

### Shared Data
Data that is used by this service but owned and managed by another bounded context. Read-only from this service's perspective.

Two kinds exist: **global lookup tables** (static reference data like countries or reason codes, not owned by any specific service) and **per-consumer projections** (a bounded view of another service's entity, containing only the fields this consumer needs, named `{Entity}For{Consumer}`).

> *Example: `OrganisationForScheduling` is a projection of the Organisation entity containing only the `Name` field, as needed by the Scheduling service.*

### Service
A collection of related entities, enums, and cross-service dependencies that together represent a business capability. Also called a "bounded context".

> *Example: The Scheduling service owns Booking, Attendance, and Session entities.*

---

## Behaviours

### Command
A business action that changes something. Named using action verbs.

> *Examples: "Place a booking", "Cancel an enrolment", "Record attendance"*

### Query
A business question that retrieves information without changing anything.

> *Examples: "Show today's bookings", "List children at centre", "Get attendance report"*

---

## Events

Events signal that something has happened. Always named in past tense.

### Domain Event
Something significant that happened within this domain. Other parts of the same domain may react to it.

> *Example: `AttendanceRecorded` — the Billing component might react to calculate charges.*

### Integration Event
A notification sent to other domains or external systems. Represents a contract with the outside world.

> *Example: `ChildEnrolmentChanged` — sent to government reporting systems.*

---

## Relationships

### Belongs To
Indicates ownership. The child entity's lifecycle is tied to the parent — if the parent is removed, so are its children.

> *Example: Attendance belongs to Booking — if the booking is removed, its attendance records go with it.*

### Has One / Has Many
Indicates association with another entity **within the same service**. `has_one` for a single related entity, `has_many` for a collection.

> *Example: Booking has many Attendances. Booking has one Session.*

### Has One? (Optional)
A `has_one` relationship where the related entity may not exist.

> *Example: `has_one Absence?` — a booking may or may not have an associated absence record.*

### References
A cross-service read dependency. The entity reads data from another bounded context's per-consumer shared type. The relationship is ID-only at the domain level — full navigation is not available within this service.

> *Example: `references OrganisationForScheduling` — the Booking entity reads the organisation's name from the Scheduling service's projection of Organisation data.*

---

## Data Concepts

### Projection
A view of data shaped for a specific query purpose. May combine fields from multiple entities. Defined in `.projection` files.

> *Example: "Attendance Report" projection combines Booking, Child, and Attendance data into a flat view for display.*

### Lookup
A global shared type used for selection or validation. Presented as dropdown lists or option sets in UIs.

> *Example: List of absence reasons, care types, or states.*

### Per-Consumer Projection
A `.shared` file that represents a subset of another service's entity, containing only the fields this consumer actually needs. Named `{Entity}For{Consumer}`.

> *Example: `ChildForKiosk` contains only `FirstName`, `LastName`, and `Image` — the fields the Kiosk service needs when displaying a child at sign-in.*

---

## Technical Terms (for implementers)

| Technical Term | Business-Friendly Alternative |
|----------------|------------------------------|
| Aggregate Root | Entry point / Main entity |
| DTO | Data shape / Response |
| Anti-Corruption Layer | Cross-service projection |
| CRUD | Create, Update, Delete (or specific actions) |
| Foreign Key | Relationship / Link |
| Persistence | Storage |
| Discriminated Union | Union type |
