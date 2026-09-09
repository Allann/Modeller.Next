namespace EveryService;

public enum ConnectionStage
{
    ConnectionDraft,
    Proposed,
    AcceptedPrivate,
    ConnectionPublishedScoped,
    ConnectionRejected,
    Revoked
}



public abstract record Connection(
    Guid Requester,
    Guid Recipient
);

public sealed record ConnectionConnectionDraft(
    Guid Requester,
    Guid Recipient
) : Connection(
    Requester,
    Recipient
);

public sealed record ConnectionProposed(
    Guid Requester,
    Guid Recipient,
    string ProposalMessage
) : Connection(
    Requester,
    Recipient
);

public sealed record ConnectionAcceptedPrivate(
    Guid Requester,
    Guid Recipient
) : Connection(
    Requester,
    Recipient
);

public sealed record ConnectionConnectionPublishedScoped(
    Guid Requester,
    Guid Recipient,
    VisibilityScope VisibilityScope
) : Connection(
    Requester,
    Recipient
);

public sealed record ConnectionConnectionRejected(
    Guid Requester,
    Guid Recipient
) : Connection(
    Requester,
    Recipient
);

public sealed record ConnectionRevoked(
    Guid Requester,
    Guid Recipient,
    string RevocationReason
) : Connection(
    Requester,
    Recipient
);


