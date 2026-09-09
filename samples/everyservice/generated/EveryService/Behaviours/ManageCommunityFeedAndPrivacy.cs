namespace EveryService;

public static class ManageCommunityFeedAndPrivacy
{
    public static CommunityStage Apply(
        CommunityStage current,
        CommunityFeedPublicationFacts facts) =>
        current == CommunityStage.PrivateGroup && CommunityFeedPublication.Determine(facts)
            ? CommunityStage.UnlistedGroup
            : 
        current == CommunityStage.UnlistedGroup && CommunityFeedPublication.Determine(facts)
            ? CommunityStage.DiscoverableGroup
            : current;
}
