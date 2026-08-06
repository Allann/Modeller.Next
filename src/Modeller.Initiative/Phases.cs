namespace Modeller.Initiative;

/// <summary>
/// The six-phase breakdown carried over from Business Statement's Discovery Session Phases
/// (docs/vault/Discovery Session Phases.md), grouped per issue #86's resolution into Discover
/// (CaptureRequest, ClarifyProblem, IdentifyImpact) and Frame (DefineOutcomes, SetBoundaries).
/// Discovery Gate sits at the Frame -&gt; Shape boundary; Shape itself is not a phase here because it
/// operates on <see cref="SelectedIntervention"/> rather than a <see cref="StructuredFields"/> field.
/// </summary>
public enum DiscoveryPhase
{
    CaptureRequest,
    ClarifyProblem,
    IdentifyImpact,
    DefineOutcomes,
    SetBoundaries,
}

/// <summary>
/// The first-class structured fields promoted out of Business Statement's flattened per-phase bullet
/// lists, per issue #86's resolution — each field Discovery Gate Criteria checks individually gets
/// its own place instead of being merged into one undifferentiated phase bucket.
/// </summary>
public enum InitiativeField
{
    ProblemStatement,
    AffectedUsers,
    PainPoints,
    Outcomes,
    SuccessCriteria,
    NonGoals,
    Constraints,
    Assumptions,
    OpenQuestions,
    Risks,
}

public static class InitiativeFields
{
    /// <summary>Which Discover/Frame phase a field is elicited during, for phase-progress display and gate grouping.</summary>
    public static DiscoveryPhase PhaseOf(InitiativeField field) => field switch
    {
        InitiativeField.ProblemStatement or InitiativeField.PainPoints => DiscoveryPhase.ClarifyProblem,
        InitiativeField.AffectedUsers => DiscoveryPhase.IdentifyImpact,
        InitiativeField.Outcomes or InitiativeField.SuccessCriteria => DiscoveryPhase.DefineOutcomes,
        InitiativeField.NonGoals or InitiativeField.Constraints or InitiativeField.Assumptions
            or InitiativeField.OpenQuestions or InitiativeField.Risks => DiscoveryPhase.SetBoundaries,
        _ => throw new ArgumentOutOfRangeException(nameof(field)),
    };
}
