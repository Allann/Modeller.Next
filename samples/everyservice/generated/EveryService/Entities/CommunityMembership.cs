namespace EveryService;

public enum CommunityMembershipStage
{
    MembershipInvited,
    MembershipRequested,
    MembershipActive,
    MembershipLeft,
    MembershipRemoved
}



public abstract record CommunityMembership(
    Guid Community,
    Guid Member
);

public sealed record CommunityMembershipMembershipInvited(
    Guid Community,
    Guid Member
) : CommunityMembership(
    Community,
    Member
);

public sealed record CommunityMembershipMembershipRequested(
    Guid Community,
    Guid Member
) : CommunityMembership(
    Community,
    Member
);

public sealed record CommunityMembershipMembershipActive(
    Guid Community,
    Guid Member,
    DateTimeOffset JoinedAt
) : CommunityMembership(
    Community,
    Member
);

public sealed record CommunityMembershipMembershipLeft(
    Guid Community,
    Guid Member,
    DateTimeOffset LeftAt
) : CommunityMembership(
    Community,
    Member
);

public sealed record CommunityMembershipMembershipRemoved(
    Guid Community,
    Guid Member,
    string RemovalReason
) : CommunityMembership(
    Community,
    Member
);


