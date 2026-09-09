namespace EveryService;

public static class ManageMemberLifetime
{
    public static MemberStage Apply(
        MemberStage current,
        MemberActivationFacts facts) =>
        current == MemberStage.MemberPendingActivation && MemberActivation.Determine(facts)
            ? MemberStage.MemberActive
            : 
        current == MemberStage.MemberActive && MemberActivation.Determine(facts)
            ? MemberStage.MemberSuspended
            : 
        current == MemberStage.MemberActive && MemberActivation.Determine(facts)
            ? MemberStage.MemberDeactivated
            : 
        current == MemberStage.MemberSuspended && MemberActivation.Determine(facts)
            ? MemberStage.MemberActive
            : 
        current == MemberStage.MemberDeactivated && MemberActivation.Determine(facts)
            ? MemberStage.MemberActive
            : 
        current == MemberStage.MemberActive && MemberActivation.Determine(facts)
            ? MemberStage.MemberBanned
            : current;
}
