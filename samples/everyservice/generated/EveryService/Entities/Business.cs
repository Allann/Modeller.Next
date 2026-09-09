namespace EveryService;



public sealed record Business(
    string TradingName,
    Guid? AustralianBusinessNumber,
    bool IsGSTRegistered,
    Guid Provider
);

