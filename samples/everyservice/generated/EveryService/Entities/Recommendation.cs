namespace EveryService;

public enum RecommendationStage
{
    RecommendationDraft,
    PendingApproval,
    ApprovedPrivate,
    RecommendationPublishedScoped,
    RecommendationRejected,
    RecommendationWithdrawn,
    Invalidated
}



public abstract record Recommendation(
    string Text,
    Guid Author,
    Guid Recipient,
    Guid Connection
);

public sealed record RecommendationRecommendationDraft(
    string Text,
    Guid Author,
    Guid Recipient,
    Guid Connection
) : Recommendation(
    Text,
    Author,
    Recipient,
    Connection
);

public sealed record RecommendationPendingApproval(
    string Text,
    Guid Author,
    Guid Recipient,
    Guid Connection
) : Recommendation(
    Text,
    Author,
    Recipient,
    Connection
);

public sealed record RecommendationApprovedPrivate(
    string Text,
    Guid Author,
    Guid Recipient,
    Guid Connection
) : Recommendation(
    Text,
    Author,
    Recipient,
    Connection
);

public sealed record RecommendationRecommendationPublishedScoped(
    string Text,
    Guid Author,
    Guid Recipient,
    Guid Connection,
    VisibilityScope VisibilityScope
) : Recommendation(
    Text,
    Author,
    Recipient,
    Connection
);

public sealed record RecommendationRecommendationRejected(
    string Text,
    Guid Author,
    Guid Recipient,
    Guid Connection
) : Recommendation(
    Text,
    Author,
    Recipient,
    Connection
);

public sealed record RecommendationRecommendationWithdrawn(
    string Text,
    Guid Author,
    Guid Recipient,
    Guid Connection
) : Recommendation(
    Text,
    Author,
    Recipient,
    Connection
);

public sealed record RecommendationInvalidated(
    string Text,
    Guid Author,
    Guid Recipient,
    Guid Connection,
    string InvalidatedReason
) : Recommendation(
    Text,
    Author,
    Recipient,
    Connection
);


