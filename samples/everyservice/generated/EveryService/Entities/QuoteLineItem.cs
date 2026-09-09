namespace EveryService;



public sealed record QuoteLineItem(
    string Description,
    int Quantity,
    decimal UnitRate,
    bool IsTaxable,
    Guid Quote
);

