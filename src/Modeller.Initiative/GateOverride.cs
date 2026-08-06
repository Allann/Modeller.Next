namespace Modeller.Initiative;

/// <summary>
/// A recorded moment where the Facilitator acted against a gate's advice. Both gates are strictly
/// advisory (issue #83/#86: "no gate state or override may ever block or force a Facilitator
/// action" — the same non-negotiable rule Business Statement's Discovery Gate uses), so an override
/// never blocks or forces anything; it only makes the human disagreement durable. Immutable once
/// created; retained rather than deleted.
/// </summary>
public abstract class GateOverride(GateOverrideId id, GateKind kind, string? reason)
{
    public GateOverrideId Id { get; } = id;
    public GateKind Kind { get; } = kind;
    public string? Reason { get; } = reason;

    protected static GateCheckResult RequireFailed(GateCheckResult finding) => finding.Passed
        ? throw new ArgumentException("Only a failed gate finding can be overridden.", nameof(finding))
        : finding;
}

/// <summary>The Facilitator dismissed a flagged gap the gate raised; retained, never deleted.</summary>
public sealed class DismissedGateFinding(GateOverrideId id, GateKind kind, GateCheckResult finding, string? reason)
    : GateOverride(id, kind, reason)
{
    public GateCheckResult Finding { get; } = RequireFailed(finding);
}

/// <summary>The Facilitator finalized the Initiative while a gate recommendation was negative.</summary>
public sealed class FinalizedAgainstGate : GateOverride
{
    public FinalizedAgainstGate(GateOverrideId id, GateKind kind, IReadOnlyList<GateCheckResult> findings, string? reason)
        : base(id, kind, reason)
    {
        if (findings.Count == 0)
        {
            throw new ArgumentException("A finalize override must carry the negative gate findings it overrode.", nameof(findings));
        }

        Findings = findings.Select(RequireFailed).ToList();
    }

    public IReadOnlyList<GateCheckResult> Findings { get; }
}
