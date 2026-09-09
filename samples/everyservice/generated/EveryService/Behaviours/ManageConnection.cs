namespace EveryService;

public static class ManageConnection
{
    public static ConnectionStage Apply(
        ConnectionStage current,
        RecommendationSubmissionFacts facts) =>
        current == ConnectionStage.ConnectionDraft && RecommendationSubmission.Determine(facts)
            ? ConnectionStage.Proposed
            : 
        current == ConnectionStage.Proposed && RecommendationSubmission.Determine(facts)
            ? ConnectionStage.AcceptedPrivate
            : 
        current == ConnectionStage.AcceptedPrivate && RecommendationSubmission.Determine(facts)
            ? ConnectionStage.ConnectionPublishedScoped
            : 
        current == ConnectionStage.Proposed && RecommendationSubmission.Determine(facts)
            ? ConnectionStage.ConnectionRejected
            : 
        current == ConnectionStage.ConnectionPublishedScoped && RecommendationSubmission.Determine(facts)
            ? ConnectionStage.Revoked
            : current;
}
