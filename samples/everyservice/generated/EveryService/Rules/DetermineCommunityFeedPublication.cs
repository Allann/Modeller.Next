namespace EveryService;

public sealed record CommunityFeedPublicationFacts(
    bool CommunityMemberMayPublish
);

public static class CommunityFeedPublication
{
    public static bool Determine(CommunityFeedPublicationFacts facts) =>
        facts.CommunityMemberMayPublish;
}
