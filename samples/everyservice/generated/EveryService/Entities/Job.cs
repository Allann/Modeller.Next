namespace EveryService;

public enum JobStage
{
    Scheduled,
    InProgress,
    CompletedByProvider,
    Confirmed
}



public abstract record Job(
    decimal AgreedTotal,
    Guid JobRequest,
    Guid Quote,
    Guid Consumer,
    Guid Provider
);

public sealed record JobScheduled(
    decimal AgreedTotal,
    Guid JobRequest,
    Guid Quote,
    Guid Consumer,
    Guid Provider,
    DateTimeOffset ScheduledFor
) : Job(
    AgreedTotal,
    JobRequest,
    Quote,
    Consumer,
    Provider
);

public sealed record JobInProgress(
    decimal AgreedTotal,
    Guid JobRequest,
    Guid Quote,
    Guid Consumer,
    Guid Provider
) : Job(
    AgreedTotal,
    JobRequest,
    Quote,
    Consumer,
    Provider
);

public sealed record JobCompletedByProvider(
    decimal AgreedTotal,
    Guid JobRequest,
    Guid Quote,
    Guid Consumer,
    Guid Provider,
    DateTimeOffset ProviderCompletedAt
) : Job(
    AgreedTotal,
    JobRequest,
    Quote,
    Consumer,
    Provider
);

public sealed record JobConfirmed(
    decimal AgreedTotal,
    Guid JobRequest,
    Guid Quote,
    Guid Consumer,
    Guid Provider,
    DateTimeOffset ConsumerConfirmedAt
) : Job(
    AgreedTotal,
    JobRequest,
    Quote,
    Consumer,
    Provider
);


