namespace Modeller.Initiative;

/// <summary>
/// <see cref="ProposedBy"/> is the question's provenance: the session-scoped identity that proposed
/// it — the Agent Participant for AI-proposed questions (issue #89), or the Facilitator for a manual
/// one. Required at creation and immutable across every state transition, so human-vs-AI provenance
/// is never ambiguous in the Initiative record. Business Statement's Send Reason and Practice Level
/// snapshot are deliberately not carried over — both are deferred per issue #83's resolution.
/// </summary>
public abstract class PromptedQuestion(QuestionId id, string text, ParticipantId proposedBy, ParticipantRole authorRole, InitiativeField field)
{
    public QuestionId Id { get; } = id;
    public string Text { get; } = text;
    public ParticipantId ProposedBy { get; } = proposedBy;
    public ParticipantRole AuthorRole { get; } = authorRole;
    public InitiativeField Field { get; } = field;
}

/// <summary>Not yet visible to the Domain Expert; only the Facilitator sees it.</summary>
public sealed class ProposedQuestion(QuestionId id, string text, ParticipantId proposedBy, ParticipantRole authorRole, InitiativeField field)
    : PromptedQuestion(id, text, proposedBy, authorRole, field)
{
    public SentQuestion SendToDomainExpert() => new(Id, Text, ProposedBy, AuthorRole, Field);

    public ProposedQuestion WithText(string newText) => new(Id, newText, ProposedBy, AuthorRole, Field);

    public RejectedQuestion Reject() => new(Id, Text, ProposedBy, AuthorRole, Field);
}

/// <summary>Visible to the Domain Expert, who may now submit a response to it.</summary>
public sealed class SentQuestion(QuestionId id, string text, ParticipantId proposedBy, ParticipantRole authorRole, InitiativeField field)
    : PromptedQuestion(id, text, proposedBy, authorRole, field);

/// <summary>Declined by the Facilitator while still queued; retained for the session's audit trail.</summary>
public sealed class RejectedQuestion(QuestionId id, string text, ParticipantId proposedBy, ParticipantRole authorRole, InitiativeField field)
    : PromptedQuestion(id, text, proposedBy, authorRole, field);
