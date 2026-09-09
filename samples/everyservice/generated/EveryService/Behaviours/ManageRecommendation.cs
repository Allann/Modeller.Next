namespace EveryService;

public static class ManageRecommendation
{
    public static RecommendationStage Apply(
        RecommendationStage current,
        RecommendationSubmissionFacts facts) =>
        current == RecommendationStage.RecommendationDraft && RecommendationSubmission.Determine(facts)
            ? RecommendationStage.PendingApproval
            : 
        current == RecommendationStage.PendingApproval && RecommendationSubmission.Determine(facts)
            ? RecommendationStage.ApprovedPrivate
            : 
        current == RecommendationStage.ApprovedPrivate && RecommendationSubmission.Determine(facts)
            ? RecommendationStage.RecommendationPublishedScoped
            : 
        current == RecommendationStage.PendingApproval && RecommendationSubmission.Determine(facts)
            ? RecommendationStage.RecommendationRejected
            : 
        current == RecommendationStage.RecommendationPublishedScoped && RecommendationSubmission.Determine(facts)
            ? RecommendationStage.RecommendationWithdrawn
            : 
        current == RecommendationStage.RecommendationPublishedScoped && RecommendationSubmission.Determine(facts)
            ? RecommendationStage.Invalidated
            : current;
}
