namespace EveryService;

public static class ManageCommunityInvitation
{
    public static CommunityInvitationStage Apply(
        CommunityInvitationStage current,
        CommunityMembershipAcceptanceFacts facts) =>
        current == CommunityInvitationStage.InvitationPending && CommunityMembershipAcceptance.Determine(facts)
            ? CommunityInvitationStage.InvitationAccepted
            : 
        current == CommunityInvitationStage.InvitationPending && CommunityMembershipAcceptance.Determine(facts)
            ? CommunityInvitationStage.InvitationDeclined
            : 
        current == CommunityInvitationStage.InvitationPending && CommunityMembershipAcceptance.Determine(facts)
            ? CommunityInvitationStage.InvitationExpired
            : current;
}
