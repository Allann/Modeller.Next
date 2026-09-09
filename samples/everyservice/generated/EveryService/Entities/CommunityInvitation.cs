namespace EveryService;

public enum CommunityInvitationStage
{
    InvitationPending,
    InvitationAccepted,
    InvitationDeclined,
    InvitationExpired
}



public abstract record CommunityInvitation(
    Guid Community,
    Guid Inviter,
    Guid? Invitee
);

public sealed record CommunityInvitationInvitationPending(
    Guid Community,
    Guid Inviter,
    Guid? Invitee,
    string InviteLink,
    DateTimeOffset ExpiresAt
) : CommunityInvitation(
    Community,
    Inviter,
    Invitee
);

public sealed record CommunityInvitationInvitationAccepted(
    Guid Community,
    Guid Inviter,
    Guid? Invitee
) : CommunityInvitation(
    Community,
    Inviter,
    Invitee
);

public sealed record CommunityInvitationInvitationDeclined(
    Guid Community,
    Guid Inviter,
    Guid? Invitee
) : CommunityInvitation(
    Community,
    Inviter,
    Invitee
);

public sealed record CommunityInvitationInvitationExpired(
    Guid Community,
    Guid Inviter,
    Guid? Invitee
) : CommunityInvitation(
    Community,
    Inviter,
    Invitee
);


