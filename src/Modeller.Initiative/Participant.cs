namespace Modeller.Initiative;

/// <summary>
/// v1 scope per issue #83/#86: Facilitator, Domain Expert, and Agent only. Business Statement's
/// richer role set (IT Team Participant, Observer, Mentor, Role Ladder) is deliberately not modelled
/// here — see issue #88's "explicitly not built here" list.
/// </summary>
public enum ParticipantRole
{
    Facilitator,
    DomainExpert,
    Agent,
}

/// <summary>
/// Per docs/coding-standards/domain-modeling/constructor-validation-and-invariants.md: the raw
/// constructor is private; <see cref="CreateNew"/> mints a fresh identity, <see cref="CreateExisting"/>
/// rehydrates a previously-minted one (e.g. from persistence) — both validate <see cref="DisplayName"/>
/// identically, so a corrupt persisted record can never produce an invalid <see cref="Participant"/>.
/// </summary>
public sealed record Participant
{
    public ParticipantId Id { get; private init; }
    public string DisplayName { get; private init; } = null!;
    public ParticipantRole Role { get; private init; }

    private Participant()
    {
    }

    public static Participant CreateNew(string displayName, ParticipantRole role) => new()
    {
        Id = ParticipantId.New(),
        DisplayName = ValidDisplayName(displayName),
        Role = role,
    };

    public static Participant CreateExisting(ParticipantId id, string displayName, ParticipantRole role) => new()
    {
        Id = id,
        DisplayName = ValidDisplayName(displayName),
        Role = role,
    };

    private static string ValidDisplayName(string displayName) => string.IsNullOrWhiteSpace(displayName)
        ? throw new ArgumentException("Display name is required.", nameof(displayName))
        : displayName.Trim();
}
