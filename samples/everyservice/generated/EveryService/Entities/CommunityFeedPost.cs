namespace EveryService;



public sealed record CommunityFeedPost(
    string PostType,
    string Text,
    DateTimeOffset CreatedAt,
    Guid Community,
    Guid Author
);

