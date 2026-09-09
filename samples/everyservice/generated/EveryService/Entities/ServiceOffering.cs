namespace EveryService;

public enum ServiceOfferingStage
{
    Active,
    Paused,
    Retired
}



public abstract record ServiceOffering(
    string Category,
    string Description,
    VisibilityScope Visibility,
    Guid Provider,
    Guid? Business
);

public sealed record ServiceOfferingActive(
    string Category,
    string Description,
    VisibilityScope Visibility,
    Guid Provider,
    Guid? Business,
    decimal StartingPrice
) : ServiceOffering(
    Category,
    Description,
    Visibility,
    Provider,
    Business
);

public sealed record ServiceOfferingPaused(
    string Category,
    string Description,
    VisibilityScope Visibility,
    Guid Provider,
    Guid? Business
) : ServiceOffering(
    Category,
    Description,
    Visibility,
    Provider,
    Business
);

public sealed record ServiceOfferingRetired(
    string Category,
    string Description,
    VisibilityScope Visibility,
    Guid Provider,
    Guid? Business
) : ServiceOffering(
    Category,
    Description,
    Visibility,
    Provider,
    Business
);


