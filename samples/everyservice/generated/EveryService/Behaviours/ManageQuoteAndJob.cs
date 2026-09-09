namespace EveryService;

public static class ManageQuoteAndJob
{
    public static QuoteStage Apply(
        QuoteStage current,
        QuoteAcceptanceFacts facts) =>
        current == QuoteStage.QuoteDraft && QuoteAcceptance.Determine(facts)
            ? QuoteStage.Submitted
            : 
        current == QuoteStage.Submitted && QuoteAcceptance.Determine(facts)
            ? QuoteStage.Accepted
            : 
        current == QuoteStage.Submitted && QuoteAcceptance.Determine(facts)
            ? QuoteStage.Declined
            : 
        current == QuoteStage.Submitted && QuoteAcceptance.Determine(facts)
            ? QuoteStage.Expired
            : 
        current == QuoteStage.Submitted && QuoteAcceptance.Determine(facts)
            ? QuoteStage.QuoteWithdrawn
            : current;
}
