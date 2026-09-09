namespace EveryService;

public sealed record CommunityMembershipAcceptanceFacts(
    bool CommunityInvitationIsCurrent,
    bool CommunityOwnerApprovedMembership
);

public static class CommunityMembershipAcceptance
{
    public static bool Determine(CommunityMembershipAcceptanceFacts facts) =>
        facts.CommunityInvitationIsCurrent &&
        facts.CommunityOwnerApprovedMembership;
}
