---
title: "Behaviours"
---

# Behaviours

Behaviours define **what can be done** within the domain. They are separated into:

- **Commands** - Actions that change things (e.g., "Cancel a booking")
- **Queries** - Questions that retrieve information (e.g., "Show me today's bookings")
- **Workflows** - Processes that orchestrate multiple commands

This separation reflects how business naturally thinks: doing things, asking questions, and following processes.

## Commands

Commands represent **business actions** that change state. They are named using action verbs that describe what the business does.

### Characteristics

- Changes state
- Has a single responsibility
- May publish events
- Named as business actions (not technical CRUD)
- Preconditions handled by rules engine (future)

### Definition Structure

```
command RecordAttendance
    "Records that a child has arrived for their booked session"

    involves
        Booking accessed_through
        Attendance creates
        Child reads
        Adult reads "The person signing in"

    input
        Booking: Booking "The booking to record attendance for"
        TimeIn: time "When the child arrived"
        SignedInBy: Adult "Adult who signed the child in"

    outcome
        creates Attendance
        changes Booking.Status to Attending
        publishes AttendanceRecorded
end
```

### The `involves` Keyword

The `involves` section defines which entities participate in a behaviour and how:

| Relationship | Meaning |
|--------------|---------|
| `accessed_through` | This is the main entity; others are accessed via it |
| `reads` | Information is read from this entity |
| `creates` | A new instance of this entity is created |
| `updates` | This entity is modified |
| `removes` | This entity is deleted |

### Business Actions vs CRUD

Prefer business language over technical operations:

| Instead of | Use |
|------------|-----|
| CreateBooking | PlaceBooking, ScheduleAttendance |
| UpdateBooking | RescheduleBooking, AdjustSession |
| DeleteBooking | CancelBooking, WithdrawBooking |
| UpdateStatus | RecordAttendance, RecordAbsence |

### Simple CRUD Entities

For straightforward lookup/shared data where CRUD is appropriate:

```
entity AbsenceReason
    "Reasons why a child may be absent"
    crud all  // Generates standard Create, Update, Delete commands

    attributes
        Name: text "Display name of the reason"
end
```

This generates standard commands: Create, Update, Delete.

---

## Queries

Queries retrieve data without modifying state. They return projections of domain data.

### Characteristics

- Read-only, no side effects
- Returns a projection (may differ from entity structure)
- Can aggregate data from multiple entities
- Defines what information is returned

### Definition Structure

```
query GetActualBookings
    "Retrieves bookings with attendance and absence data"
    owner Booking

    parameters
        Centre: Centre "The centre to query"
        DateRange: DateRange "Period to include"
        IncludeAbsences: bool = true "Whether to include absence records"

    returns list of ActualBookingResult
        "Bookings grouped by room"
end
```

### Projections

Queries return projections, not raw entities:

```
projection ActualBookingResult
    "Booking with attendance details for reporting"

    fields
        BookingDate from Booking.Date
        ChildName from Booking.Child.Name format "{FirstName} {LastName}"
        SessionName from Booking.Session.Name
        Status from Booking.Status
        Attendances from Booking.Attendances as AttendanceResult
        HasAbsence computed Booking.Absence is not null
end
```

---

## Command vs Query Summary

| Aspect | Command | Query |
|--------|---------|-------|
| Purpose | Change state | Read state |
| Side effects | Yes | No |
| Returns | Success/failure | Data |
| Naming | Imperative verb | Get/Find/List |
| Caching | Not cacheable | Cacheable |
| Events | May publish | Never publishes |

---

## Workflows

Workflows are behaviours that orchestrate multiple commands in a sequence. They represent business processes.

```
workflow ProcessEnrolment
    "Complete enrolment of a child at a centre"

    involves
        Child reads, updates
        Centre reads
        Arrangement creates
        Family reads, notifies

    steps
        VerifyChildDetails
            "Ensure child information is complete"
            command ValidateChild

        CreateArrangement
            "Set up the care arrangement"
            command CreateArrangement
            after VerifyChildDetails

        NotifyParties
            "Inform relevant parties"
            commands NotifyCentre, NotifyFamily
            parallel
            after CreateArrangement

    outcome
        publishes EnrolmentCompleted when successful
        publishes EnrolmentFailed when unsuccessful
end
```

---

## Events

Events signal that something has happened. They are always named in **past tense**.

### Event Types

| Type | Purpose | Subscribers |
|------|---------|-------------|
| **Domain Event** | Something happened in this domain | Other parts of same domain |
| **Integration Event** | Notify other systems | External systems/domains |
| **Notification Event** | Trigger communication | Notification services |

### Domain Event

```
event AttendanceRecorded
    "A child's attendance has been recorded"
    type domain
    source RecordAttendance

    data
        Booking: Booking "The booking attendance was recorded for"
        Attendance: Attendance "The attendance record created"
        RecordedAt: datetime "When the attendance was recorded"
end
```

### Integration Event

```
event ChildEnrolmentChanged
    "Notifies external systems of enrolment changes"
    type integration
    source ProcessEnrolment

    data
        ChildId: text "External identifier for the child"
        CentreId: text "External identifier for the centre"
        ChangeType: text "Type of change (New, Modified, Ended)"
end
```

### Event Lifecycle

1. **Published** - Command completes and publishes event
2. **Delivered** - Event is sent to subscribers
3. **Handled** - Subscriber processes the event
4. **Acknowledged** - Processing confirmed complete

### Subscribing to Events

Other behaviours can react to events:

```
command CalculateBookingCharges
    "Calculate charges when attendance is recorded"
    triggered_by AttendanceRecorded

    involves
        Attendance reads
        Booking reads
        Charges creates
end
```
