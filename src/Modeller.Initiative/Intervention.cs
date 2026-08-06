namespace Modeller.Initiative;

/// <summary>
/// The fixed taxonomy locked in by issue #83's resolution, matching the shipped landing-page copy
/// (issue #82). Deliberately not extensible per-Initiative — introducing a different or open taxonomy
/// in the product would contradict what has already been told to visitors.
/// </summary>
public enum InterventionType
{
    Process,
    People,
    Organisation,
    Policy,
    Information,
    Technology,
    Experiment,
    NoAction,
}

/// <summary>
/// One of possibly several interventions selected for this Initiative (issue #83: multiple
/// interventions can be selected per Initiative, matching the shipped mixed-intervention example).
/// <see cref="DesignWorkspaceReference"/> is only ever set for a <see cref="InterventionType.Technology"/>
/// intervention and is a recorded cross-reference link only — never auto-scaffolded model content,
/// per issue #83's resolution.
/// </summary>
public sealed record SelectedIntervention(
    InterventionId Id,
    InterventionType Type,
    string Description,
    string Rationale,
    string? DesignWorkspaceReference = null)
{
    public string Description { get; init; } = string.IsNullOrWhiteSpace(Description)
        ? throw new ArgumentException("An intervention description is required.", nameof(Description))
        : Description.Trim();

    public string Rationale { get; init; } = string.IsNullOrWhiteSpace(Rationale)
        ? throw new ArgumentException("An intervention's rationale is required.", nameof(Rationale))
        : Rationale.Trim();

    public bool ContinuesToDesignWorkspace => DesignWorkspaceReference is not null;

    public SelectedIntervention WithDesignWorkspaceReference(string reference)
    {
        if (Type != InterventionType.Technology)
        {
            throw new InvalidOperationException("Only a Technology intervention can reference a design workspace.");
        }

        if (string.IsNullOrWhiteSpace(reference))
        {
            throw new ArgumentException("A design workspace reference is required.", nameof(reference));
        }

        return this with { DesignWorkspaceReference = reference.Trim() };
    }
}
