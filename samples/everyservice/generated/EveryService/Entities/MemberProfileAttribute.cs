namespace EveryService;



public sealed record MemberProfileAttribute(
    string Key,
    string Value,
    ProfileVisibility Visibility,
    Guid Member
);

