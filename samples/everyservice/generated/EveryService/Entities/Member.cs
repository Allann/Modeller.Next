namespace EveryService;

public enum MemberStage
{
    MemberPendingActivation,
    MemberActive,
    MemberSuspended,
    MemberDeactivated,
    MemberBanned
}



public abstract record Member(
    string DisplayName,
    string EmailAddress,
    MemberStatus Status,
    DateTimeOffset CreatedAt,
    bool IsVerifiedProfessional,
    bool ConnectionsHidden,
    bool ShowServiceOfferingFeedEvents,
    string AvatarImageReference,
    DateOnly JoinedOn
);

public sealed record MemberMemberPendingActivation(
    string DisplayName,
    string EmailAddress,
    MemberStatus Status,
    DateTimeOffset CreatedAt,
    bool IsVerifiedProfessional,
    bool ConnectionsHidden,
    bool ShowServiceOfferingFeedEvents,
    string AvatarImageReference,
    DateOnly JoinedOn
) : Member(
    DisplayName,
    EmailAddress,
    Status,
    CreatedAt,
    IsVerifiedProfessional,
    ConnectionsHidden,
    ShowServiceOfferingFeedEvents,
    AvatarImageReference,
    JoinedOn
);

public sealed record MemberMemberActive(
    string DisplayName,
    string EmailAddress,
    MemberStatus Status,
    DateTimeOffset CreatedAt,
    bool IsVerifiedProfessional,
    bool ConnectionsHidden,
    bool ShowServiceOfferingFeedEvents,
    string AvatarImageReference,
    DateOnly JoinedOn,
    DateTimeOffset ActivatedAt,
    DateTimeOffset ReactivatedAt
) : Member(
    DisplayName,
    EmailAddress,
    Status,
    CreatedAt,
    IsVerifiedProfessional,
    ConnectionsHidden,
    ShowServiceOfferingFeedEvents,
    AvatarImageReference,
    JoinedOn
);

public sealed record MemberMemberSuspended(
    string DisplayName,
    string EmailAddress,
    MemberStatus Status,
    DateTimeOffset CreatedAt,
    bool IsVerifiedProfessional,
    bool ConnectionsHidden,
    bool ShowServiceOfferingFeedEvents,
    string AvatarImageReference,
    DateOnly JoinedOn,
    DateTimeOffset SuspendedAt
) : Member(
    DisplayName,
    EmailAddress,
    Status,
    CreatedAt,
    IsVerifiedProfessional,
    ConnectionsHidden,
    ShowServiceOfferingFeedEvents,
    AvatarImageReference,
    JoinedOn
);

public sealed record MemberMemberDeactivated(
    string DisplayName,
    string EmailAddress,
    MemberStatus Status,
    DateTimeOffset CreatedAt,
    bool IsVerifiedProfessional,
    bool ConnectionsHidden,
    bool ShowServiceOfferingFeedEvents,
    string AvatarImageReference,
    DateOnly JoinedOn,
    DateTimeOffset DeactivatedAt
) : Member(
    DisplayName,
    EmailAddress,
    Status,
    CreatedAt,
    IsVerifiedProfessional,
    ConnectionsHidden,
    ShowServiceOfferingFeedEvents,
    AvatarImageReference,
    JoinedOn
);

public sealed record MemberMemberBanned(
    string DisplayName,
    string EmailAddress,
    MemberStatus Status,
    DateTimeOffset CreatedAt,
    bool IsVerifiedProfessional,
    bool ConnectionsHidden,
    bool ShowServiceOfferingFeedEvents,
    string AvatarImageReference,
    DateOnly JoinedOn,
    DateTimeOffset BannedAt
) : Member(
    DisplayName,
    EmailAddress,
    Status,
    CreatedAt,
    IsVerifiedProfessional,
    ConnectionsHidden,
    ShowServiceOfferingFeedEvents,
    AvatarImageReference,
    JoinedOn
);


