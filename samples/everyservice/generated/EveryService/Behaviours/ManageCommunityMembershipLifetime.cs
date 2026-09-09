namespace EveryService;

public static class ManageCommunityMembershipLifetime
{
    public static CommunityMembershipStage Apply(
        CommunityMembershipStage current,
        CommunityMembershipAcceptanceFacts facts) =>
        current == CommunityMembershipStage.MembershipInvited && CommunityMembershipAcceptance.Determine(facts)
            ? CommunityMembershipStage.MembershipRequested
            : 
        current == CommunityMembershipStage.MembershipRequested && CommunityMembershipAcceptance.Determine(facts)
            ? CommunityMembershipStage.MembershipActive
            : 
        current == CommunityMembershipStage.MembershipActive && CommunityMembershipAcceptance.Determine(facts)
            ? CommunityMembershipStage.MembershipLeft
            : 
        current == CommunityMembershipStage.MembershipActive && CommunityMembershipAcceptance.Determine(facts)
            ? CommunityMembershipStage.MembershipRemoved
            : current;
}
