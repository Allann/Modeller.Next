namespace Modeller.Initiative;

public readonly record struct InitiativeId
{
    private InitiativeId(Guid value) => Value = value;

    public Guid Value { get; }

    public static InitiativeId New() => new(Guid.CreateVersion7());

    /// <summary>Rehydrates a previously-minted id (e.g. loaded from persistence). Never use this to
    /// mint a new identity — use <see cref="New"/>.</summary>
    public static InitiativeId FromExisting(Guid value) => new(Identifiers.RequireNonEmpty(value));

    public override string ToString() => Value.ToString("D");
}

public readonly record struct ParticipantId
{
    private ParticipantId(Guid value) => Value = value;

    public Guid Value { get; }

    public static ParticipantId New() => new(Guid.CreateVersion7());

    public static ParticipantId FromExisting(Guid value) => new(Identifiers.RequireNonEmpty(value));

    public override string ToString() => Value.ToString("D");
}

public readonly record struct QuestionId
{
    private QuestionId(Guid value) => Value = value;

    public Guid Value { get; }

    public static QuestionId New() => new(Guid.CreateVersion7());

    public static QuestionId FromExisting(Guid value) => new(Identifiers.RequireNonEmpty(value));

    public override string ToString() => Value.ToString("D");
}

public readonly record struct ResponseId
{
    private ResponseId(Guid value) => Value = value;

    public Guid Value { get; }

    public static ResponseId New() => new(Guid.CreateVersion7());

    public static ResponseId FromExisting(Guid value) => new(Identifiers.RequireNonEmpty(value));

    public override string ToString() => Value.ToString("D");
}

public readonly record struct InterventionId
{
    private InterventionId(Guid value) => Value = value;

    public Guid Value { get; }

    public static InterventionId New() => new(Guid.CreateVersion7());

    public static InterventionId FromExisting(Guid value) => new(Identifiers.RequireNonEmpty(value));

    public override string ToString() => Value.ToString("D");
}

public readonly record struct GateOverrideId
{
    private GateOverrideId(Guid value) => Value = value;

    public Guid Value { get; }

    public static GateOverrideId New() => new(Guid.CreateVersion7());

    public static GateOverrideId FromExisting(Guid value) => new(Identifiers.RequireNonEmpty(value));

    public override string ToString() => Value.ToString("D");
}

file static class Identifiers
{
    public static Guid RequireNonEmpty(Guid value) => value == Guid.Empty
        ? throw new ArgumentException("An identifier cannot be an empty GUID.", nameof(value))
        : value;
}
