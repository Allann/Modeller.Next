namespace Modeller.Initiative;

/// <summary>
/// <see cref="ProposedBy"/> is the question's provenance: the session-scoped identity that proposed
/// it — the Agent Participant for AI-proposed questions (issue #89), or the Facilitator for a manual
/// one. Required at creation and immutable across every state transition, so human-vs-AI provenance
/// is never ambiguous in the Initiative record. Business Statement's Send Reason and Practice Level
/// snapshot are deliberately not carried over — both are deferred per issue #83's resolution.
/// </summary>
public abstract record PromptedQuestion
{
    public QuestionId Id { get; }
    public string Text { get; }
    public ParticipantId ProposedBy { get; }
    public ParticipantRole AuthorRole { get; }
    public InitiativeField Field { get; }

    protected PromptedQuestion(QuestionId id, string text, ParticipantId proposedBy, ParticipantRole authorRole, InitiativeField field)
    {
        Id = id;
        Text = string.IsNullOrWhiteSpace(text)
            ? throw new ArgumentException("Question text is required.", nameof(text))
            : text.Trim();
        ProposedBy = proposedBy;
        AuthorRole = authorRole;
        Field = field;
    }
}

/// <summary>Not yet visible to the Domain Expert; only the Facilitator sees it.</summary>
public sealed record ProposedQuestion : PromptedQuestion
{
    public ProposedQuestion(QuestionId id, string text, ParticipantId proposedBy, ParticipantRole authorRole, InitiativeField field)
        : base(id, text, proposedBy, authorRole, field)
    {
    }

    public SentQuestion SendToDomainExpert() => new(Id, Text, ProposedBy, AuthorRole, Field);

    /// <summary>Re-validates the new text through the constructor rather than an unvalidated
    /// <c>with</c> expression — see docs/coding-standards/domain-modeling/constructor-validation-and-invariants.md.</summary>
    public ProposedQuestion WithText(string newText) => new(Id, newText, ProposedBy, AuthorRole, Field);

    public RejectedQuestion Reject() => new(Id, Text, ProposedBy, AuthorRole, Field);
}

/// <summary>Visible to the Domain Expert, who may now submit a response to it.</summary>
public sealed record SentQuestion : PromptedQuestion
{
    public SentQuestion(QuestionId id, string text, ParticipantId proposedBy, ParticipantRole authorRole, InitiativeField field)
        : base(id, text, proposedBy, authorRole, field)
    {
    }
}

/// <summary>Declined by the Facilitator while still queued; retained for the session's audit trail.</summary>
public sealed record RejectedQuestion : PromptedQuestion
{
    public RejectedQuestion(QuestionId id, string text, ParticipantId proposedBy, ParticipantRole authorRole, InitiativeField field)
        : base(id, text, proposedBy, authorRole, field)
    {
    }
}
