namespace EveryService;

public sealed record RecommendationSubmissionFacts(
    bool ActiveConnectionExists
);

public static class RecommendationSubmission
{
    public static bool Determine(RecommendationSubmissionFacts facts) =>
        facts.ActiveConnectionExists;
}
