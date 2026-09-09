namespace EveryService;

public sealed record CommunityMembershipFacts(
    bool MemberIsNotBlocked
);

public static class CommunityMembership
{
    public static bool Determine(CommunityMembershipFacts facts) =>
        facts.MemberIsNotBlocked;
}
