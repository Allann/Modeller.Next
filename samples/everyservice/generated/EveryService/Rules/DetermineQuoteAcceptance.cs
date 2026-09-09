namespace EveryService;

public sealed record QuoteAcceptanceFacts(
    bool JobRequestIsOpen,
    bool QuoteIsCurrent,
    bool QuoteCurrencyMatchesLineItems
);

public static class QuoteAcceptance
{
    public static bool Determine(QuoteAcceptanceFacts facts) =>
        facts.JobRequestIsOpen &&
        facts.QuoteIsCurrent &&
        facts.QuoteCurrencyMatchesLineItems;
}
