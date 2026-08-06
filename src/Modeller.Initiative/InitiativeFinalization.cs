namespace Modeller.Initiative;

/// <summary>
/// Null while the Initiative is active. The gate never blocks finalization (issue #83/#86): if either
/// gate's latest evaluation has failing checks, the Initiative still finalizes, just as
/// <see cref="WithOpenGateFindings"/> rather than <see cref="Clean"/>, and the disagreement is
/// recorded as a <see cref="FinalizedAgainstGate"/> override.
/// </summary>
public abstract class InitiativeFinalization(string markdownSnapshot, DateTimeOffset finalizedAt)
{
    public string MarkdownSnapshot { get; } = markdownSnapshot;
    public DateTimeOffset FinalizedAt { get; } = finalizedAt;
}

public sealed class Clean(string markdownSnapshot, DateTimeOffset finalizedAt)
    : InitiativeFinalization(markdownSnapshot, finalizedAt);

public sealed class WithOpenGateFindings(string markdownSnapshot, DateTimeOffset finalizedAt)
    : InitiativeFinalization(markdownSnapshot, finalizedAt);
