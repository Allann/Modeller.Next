namespace ChildCare;

public enum BookingStatus
{
    Planned = 1,
    MissedSignIn = 2,
    Attending = 3,
    MissedSignOut = 4,
    Attended = 5,
    Absence = 6,
    PlannedHoliday = 7,
    PublicHoliday = 8,
    RoomClosure = 9,
    Removed = 10,
    Conflict = 11,
    AbsenceVoid = 12,
}
