namespace EveryService;



public sealed record MemberBlock(
    DateTimeOffset CreatedAt,
    Guid Blocker,
    Guid BlockedMember
);

