namespace Modeller.Initiative;

/// <summary>
/// A recorded moment where the Facilitator acted against a gate's advice. Both gates are strictly
/// advisory (issue #83/#86: "no gate state or override may ever block or force a Facilitator
/// action" — the same non-negotiable rule Business Statement's Discovery Gate uses), so an override
/// never blocks or forces anything; it only makes the human disagreement durable. Immutable once
/// created; retained rather than deleted.
/// </summary>
public abstract record GateOverride
{
    public GateOverrideId Id { get; }
    public GateKind Kind { get; }
    public string? Reason { get; }

    protected GateOverride(GateOverrideId id, GateKind kind, string? reason)
    {
        Id = id;
        Kind = kind;
        Reason = reason;
    }

    protected static GateCheckResult RequireFailed(GateCheckResult finding) => finding.Passed
        ? throw new ArgumentException("Only a failed gate finding can be overridden.", nameof(finding))
        : finding;
}

/// <summary>The Facilitator dismissed a flagged gap the gate raised; retained, never deleted.</summary>
public sealed record DismissedGateFinding : GateOverride
{
    public GateCheckResult Finding { get; }

    public DismissedGateFinding(GateOverrideId id, GateKind kind, GateCheckResult finding, string? reason)
        : base(id, kind, reason)
    {
        Finding = RequireFailed(finding);
    }
}

/// <summary>The Facilitator finalized the Initiative while a gate recommendation was negative.</summary>
public sealed record FinalizedAgainstGate : GateOverride
{
    public IReadOnlyList<GateCheckResult> Findings { get; }

    public FinalizedAgainstGate(GateOverrideId id, GateKind kind, IReadOnlyList<GateCheckResult> findings, string? reason)
        : base(id, kind, reason)
    {
        if (findings.Count == 0)
        {
            throw new ArgumentException("A finalize override must carry the negative gate findings it overrode.", nameof(findings));
        }

        Findings = findings.Select(RequireFailed).ToList();
    }
}
