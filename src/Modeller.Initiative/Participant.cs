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

public sealed record Participant(ParticipantId Id, string DisplayName, ParticipantRole Role)
{
    public string DisplayName { get; init; } = string.IsNullOrWhiteSpace(DisplayName)
        ? throw new ArgumentException("Display name is required.", nameof(DisplayName))
        : DisplayName.Trim();
}
