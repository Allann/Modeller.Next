namespace EveryService;



public sealed record BusinessLocation(
    string Name,
    (decimal Latitude, decimal Longitude) Location,
    bool IsPrimary,
    Guid Business
);

