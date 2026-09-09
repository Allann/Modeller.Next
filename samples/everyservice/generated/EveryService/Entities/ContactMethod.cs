namespace EveryService;



public sealed record ContactMethod(
    ContactMethodType Type,
    string Value,
    bool IsPrimary,
    DateTimeOffset? VerifiedAt,
    DateTimeOffset CreatedAt,
    Guid Member
);

