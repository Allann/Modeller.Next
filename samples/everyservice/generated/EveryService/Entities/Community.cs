namespace EveryService;

public enum CommunityStage
{
    PrivateGroup,
    UnlistedGroup,
    DiscoverableGroup
}



public abstract record Community(
    string Name,
    string Description,
    Guid Owner
);

public sealed record CommunityPrivateGroup(
    string Name,
    string Description,
    Guid Owner
) : Community(
    Name,
    Description,
    Owner
);

public sealed record CommunityUnlistedGroup(
    string Name,
    string Description,
    Guid Owner,
    string InviteLink
) : Community(
    Name,
    Description,
    Owner
);

public sealed record CommunityDiscoverableGroup(
    string Name,
    string Description,
    Guid Owner,
    string InviteLink,
    bool SearchListing
) : Community(
    Name,
    Description,
    Owner
);


