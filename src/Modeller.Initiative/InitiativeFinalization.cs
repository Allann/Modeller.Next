namespace Modeller.Initiative;

/// <summary>
/// Null while the Initiative is active. The gate never blocks finalization (issue #83/#86): if either
/// gate's latest evaluation has failing, non-dismissed checks, the Initiative still finalizes, just as
/// <see cref="WithOpenGateFindings"/> rather than <see cref="Clean"/>, and the disagreement is
/// recorded as a <see cref="FinalizedAgainstGate"/> override.
/// </summary>
public abstract record InitiativeFinalization
{
    public string MarkdownSnapshot { get; }
    public DateTimeOffset FinalizedAt { get; }

    protected InitiativeFinalization(string markdownSnapshot, DateTimeOffset finalizedAt)
    {
        MarkdownSnapshot = markdownSnapshot;
        FinalizedAt = finalizedAt;
    }
}

public sealed record Clean : InitiativeFinalization
{
    public Clean(string markdownSnapshot, DateTimeOffset finalizedAt) : base(markdownSnapshot, finalizedAt)
    {
    }
}

public sealed record WithOpenGateFindings : InitiativeFinalization
{
    public WithOpenGateFindings(string markdownSnapshot, DateTimeOffset finalizedAt) : base(markdownSnapshot, finalizedAt)
    {
    }
}
