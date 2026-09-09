namespace EveryService;

public enum JobRequestStage
{
    Open,
    Fulfilled,
    Closed
}



public abstract record JobRequest(
    string Name,
    string Description,
    VisibilityScope VisibilityPlan,
    Guid Consumer,
    Guid? PreferredProvider
);

public sealed record JobRequestOpen(
    string Name,
    string Description,
    VisibilityScope VisibilityPlan,
    Guid Consumer,
    Guid? PreferredProvider,
    decimal BudgetAmount,
    (decimal Latitude, decimal Longitude) Location,
    DateOnly PreferredDate,
    TimeOnly PreferredStartTime,
    DateTime PostedAt
) : JobRequest(
    Name,
    Description,
    VisibilityPlan,
    Consumer,
    PreferredProvider
);

public sealed record JobRequestFulfilled(
    string Name,
    string Description,
    VisibilityScope VisibilityPlan,
    Guid Consumer,
    Guid? PreferredProvider
) : JobRequest(
    Name,
    Description,
    VisibilityPlan,
    Consumer,
    PreferredProvider
);

public sealed record JobRequestClosed(
    string Name,
    string Description,
    VisibilityScope VisibilityPlan,
    Guid Consumer,
    Guid? PreferredProvider
) : JobRequest(
    Name,
    Description,
    VisibilityPlan,
    Consumer,
    PreferredProvider
);


