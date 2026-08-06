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
/// interventions can be selected per Initiative, matching the shipped mixed-intervention example),
/// "each independently flagged for whether it continues into a design workspace" (#83's own wording).
/// <see cref="ContinuesToDesignWorkspace"/> is that independent flag — chosen at selection time,
/// possible only for a <see cref="InterventionType.Technology"/> intervention (the only type any
/// design workspace exists for today) but not implied by it: a Technology intervention can be
/// selected without opening System Design, matching the agreed design that continuation is
/// independently selectable, not automatic. The flag is deliberately a separate field from
/// <see cref="DesignWorkspaceReference"/>, which is only ever populated later, once a real workspace
/// exists to link to. Conflating the two (deriving "continues" from "has a reference") would make it
/// impossible to show an intervention as queued for System Design before that workspace has actually
/// been created.
///
/// Per docs/coding-standards/domain-modeling/constructor-validation-and-invariants.md: the raw
/// constructor is private; <see cref="CreateNew"/> mints a fresh selection, <see cref="CreateExisting"/>
/// rehydrates a previously-persisted one — both funnel through the same validation, so a persisted
/// combination that live selection could never produce (a non-Technology intervention continuing to
/// System Design, or a design workspace reference on one that isn't continuing) is rejected rather
/// than silently restored.
/// </summary>
public sealed record SelectedIntervention
{
    public InterventionId Id { get; private init; }
    public InterventionType Type { get; private init; }
    public string Description { get; private init; } = null!;
    public string Rationale { get; private init; } = null!;
    public bool ContinuesToDesignWorkspace { get; private init; }
    public string? DesignWorkspaceReference { get; private init; }

    private SelectedIntervention()
    {
    }

    public static SelectedIntervention CreateNew(InterventionType type, string description, string rationale, bool continuesToDesignWorkspace) => new()
    {
        Id = InterventionId.New(),
        Type = type,
        Description = ValidText(description, nameof(description)),
        Rationale = ValidText(rationale, nameof(rationale)),
        ContinuesToDesignWorkspace = ValidContinuation(type, continuesToDesignWorkspace),
    };

    public static SelectedIntervention CreateExisting(
        InterventionId id,
        InterventionType type,
        string description,
        string rationale,
        bool continuesToDesignWorkspace,
        string? designWorkspaceReference)
    {
        var reference = string.IsNullOrWhiteSpace(designWorkspaceReference) ? null : designWorkspaceReference.Trim();
        if (reference is not null && !continuesToDesignWorkspace)
        {
            throw new ArgumentException("A design workspace reference cannot be set unless the intervention continues to a design workspace.", nameof(designWorkspaceReference));
        }

        return new SelectedIntervention
        {
            Id = id,
            Type = type,
            Description = ValidText(description, nameof(description)),
            Rationale = ValidText(rationale, nameof(rationale)),
            ContinuesToDesignWorkspace = ValidContinuation(type, continuesToDesignWorkspace),
            DesignWorkspaceReference = reference,
        };
    }

    public SelectedIntervention WithDesignWorkspaceReference(string reference)
    {
        if (!ContinuesToDesignWorkspace)
        {
            throw new InvalidOperationException("Only an intervention flagged to continue into a design workspace can be linked.");
        }

        if (string.IsNullOrWhiteSpace(reference))
        {
            throw new ArgumentException("A design workspace reference is required.", nameof(reference));
        }

        return this with { DesignWorkspaceReference = reference.Trim() };
    }

    private static string ValidText(string value, string paramName) => string.IsNullOrWhiteSpace(value)
        ? throw new ArgumentException($"A value for '{paramName}' is required.", paramName)
        : value.Trim();

    private static bool ValidContinuation(InterventionType type, bool continuesToDesignWorkspace) =>
        continuesToDesignWorkspace && type != InterventionType.Technology
            ? throw new ArgumentException("Only a Technology intervention can continue into a design workspace.", nameof(continuesToDesignWorkspace))
            : continuesToDesignWorkspace;
}
