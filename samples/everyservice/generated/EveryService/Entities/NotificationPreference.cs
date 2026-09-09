namespace EveryService;



public sealed record NotificationPreference(
    NotificationTopic Topic,
    bool IsEnabled,
    DateTimeOffset ChangedAt,
    Guid Member
);

