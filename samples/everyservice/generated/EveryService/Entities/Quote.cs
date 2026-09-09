namespace EveryService;

public enum QuoteStage
{
    QuoteDraft,
    Submitted,
    Accepted,
    Declined,
    Expired,
    QuoteWithdrawn
}



public abstract record Quote(
    int RevisionNumber,
    string CurrencyCode,
    Guid JobRequest,
    Guid Provider
);

public sealed record QuoteQuoteDraft(
    int RevisionNumber,
    string CurrencyCode,
    Guid JobRequest,
    Guid Provider
) : Quote(
    RevisionNumber,
    CurrencyCode,
    JobRequest,
    Provider
);

public sealed record QuoteSubmitted(
    int RevisionNumber,
    string CurrencyCode,
    Guid JobRequest,
    Guid Provider,
    DateTimeOffset ValidUntil,
    decimal Total
) : Quote(
    RevisionNumber,
    CurrencyCode,
    JobRequest,
    Provider
);

public sealed record QuoteAccepted(
    int RevisionNumber,
    string CurrencyCode,
    Guid JobRequest,
    Guid Provider,
    decimal Total
) : Quote(
    RevisionNumber,
    CurrencyCode,
    JobRequest,
    Provider
);

public sealed record QuoteDeclined(
    int RevisionNumber,
    string CurrencyCode,
    Guid JobRequest,
    Guid Provider
) : Quote(
    RevisionNumber,
    CurrencyCode,
    JobRequest,
    Provider
);

public sealed record QuoteExpired(
    int RevisionNumber,
    string CurrencyCode,
    Guid JobRequest,
    Guid Provider
) : Quote(
    RevisionNumber,
    CurrencyCode,
    JobRequest,
    Provider
);

public sealed record QuoteQuoteWithdrawn(
    int RevisionNumber,
    string CurrencyCode,
    Guid JobRequest,
    Guid Provider
) : Quote(
    RevisionNumber,
    CurrencyCode,
    JobRequest,
    Provider
);


