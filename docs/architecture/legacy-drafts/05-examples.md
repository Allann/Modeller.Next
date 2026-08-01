---
title: "Examples"
---

# Examples

Concrete examples of domain definitions using the DSL format.

## Complete Entity Example

### Domain Definition

```
// entities/booking.entity
entity Booking
    """
    Planned attendance for a child at a centre for a session.
    A booking represents the intention for a child to attend.
    It transitions through states as attendance is recorded.
    """

    attributes
        Date: date "Date when attendance is planned"
        SessionStartTime: time "Start time taken from session"
        SessionEndTime: time "End time taken from session"
        AdjustedStartTime: time? "Adjusted start accounting for closures"
        AdjustedEndTime: time? "Adjusted end accounting for closures"
        AdjustedHours: decimal(5,2)? "Total adjusted hours"
        Status: BookingStatus "Current state of the booking"
        AdjustedReason: BookingAdjustedReason? "Why hours were adjusted"

    has_one Session "The session being booked"
    has_one Room "Room where attendance occurs"
    has_many Attendance as Attendances "Recorded attendance instances"
    has_one Absence? "Absence record if applicable"

    belongs_to Child
end
```

> Note: What can be done with a Booking is defined in behaviours, not in the entity itself.

### Key Definition

```
// keys/booking.key
key Booking

    identity
        BookingId: int generated

    ownership
        parent Child

    indexes
        [Date, Session, Child] unique as UX_Booking_DateSessionChild
end
```

---

## Value Object Example

```
// values/address.value
value Address
    "A physical or postal address"

    attributes
        Line1: text(100) "Street address line 1"
        Line2: text(100)? "Street address line 2"
        Suburb: text(50) "Suburb or city"
        State: AustralianState "State or territory"
        PostCode: text(4) pattern "[0-9]{4}" "Postal code"
        Country: text(50) = "Australia"
end
```

---

## Reference Data Example

```
// shared/organisation.shared
shared Organisation
    "Organisation data from OrganisationService"
    source OrganisationService

    attributes
        Name: text "Organisation display name"
        Code: text "Short identifier"
        ABN: text "Australian Business Number"
        Status: OrganisationStatus "Active/Inactive"
end
```

---

## Command Example

```
// behaviours/record-attendance.command
command RecordAttendance
    "Records that a child has arrived for their booking"
    owner Booking

    input
        Booking: Booking "The booking to record attendance for"
        TimeIn: time "When the child arrived"
        SignedInBy: Adult "Adult who signed the child in"
        AuthorisedBy: Adult? "Adult authorising if different from signer"

    outcome
            Attendance

        changes
            Booking.Status to Attending

        publishes
            AttendanceRecorded
end
```

---

## Query Example

```
// behaviours/get-actual-bookings.query
query GetActualBookings
    "Retrieves bookings with attendance and absence data"
    owner Booking

    parameters
        Centre: Centre "Centre to query bookings for"
        FromDate: date "Start of date range"
        ToDate: date "End of date range"
        IncludeCancelled: bool = false "Include cancelled bookings"

    returns list of ActualBookingResult
end
```

---

## Enumeration Example

```
// enums/booking-status.enum
enum BookingStatus
    "Possible states of a booking"

    Planned "Booking is scheduled for the future"
    Attending "Child has signed in, not yet signed out"
    Attended "Child has signed in and out"
    Absence "Child did not attend"
    Cancelled "Booking was cancelled before attendance"
end
```

---

## Service Example

```
// services/scheduling.service
service Scheduling
    """
    Manages the scheduling of child attendance including
    bookings, sessions, and attendance recording.
    """

    owns
        Booking
        Attendance
        Absence
        Session
        Room
        RoutineBookingSession
        CasualBookingSession

    values
        TimeRange

    uses
        Child [Name, DateOfBirth, ChildNumber]
        Centre [Name, Token]
        Adult [Name, Contact]

    enums
        BookingStatus
        BookingAdjustedReason
        AbsenceType
        CareType

    commands
        PlaceBooking
        CancelBooking
        RecordAttendance
        RecordAbsence
        AdjustBooking

    queries
        GetBookings
        GetActualBookings
        GetAttendanceReport
        GetAbsenceSummary
end
```
