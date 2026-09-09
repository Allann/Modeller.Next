namespace EveryService;



public sealed record ServiceCoverage(
    (decimal Latitude, decimal Longitude) Centre,
    decimal RadiusKilometres,
    bool IsActive,
    Guid ServiceOffering
);

