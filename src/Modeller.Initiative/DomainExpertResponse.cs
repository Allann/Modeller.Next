namespace Modeller.Initiative;

public abstract record DomainExpertResponse
{
    public ResponseId Id { get; }
    public QuestionId QuestionId { get; }
    public string Text { get; }

    protected DomainExpertResponse(ResponseId id, QuestionId questionId, string text)
    {
        Id = id;
        QuestionId = questionId;
        Text = string.IsNullOrWhiteSpace(text)
            ? throw new ArgumentException("Response text is required.", nameof(text))
            : text.Trim();
    }
}

/// <summary>Submitted by the Domain Expert but not yet accepted into the Initiative's structured fields.</summary>
public sealed record PendingResponse : DomainExpertResponse
{
    /// <summary>Per docs/coding-standards/domain-modeling/constructor-validation-and-invariants.md: the
    /// raw constructor is internal — a repository/mapper outside this assembly must go through
    /// <see cref="CreateExisting"/>, never construct directly.</summary>
    internal PendingResponse(ResponseId id, QuestionId questionId, string text) : base(id, questionId, text)
    {
    }

    /// <summary>Rehydrates a previously-persisted Pending response.</summary>
    public static PendingResponse CreateExisting(ResponseId id, QuestionId questionId, string text) => new(id, questionId, text);

    public AcceptedResponse Accept() => new(Id, QuestionId, Text);

    public PendingResponse Edit(string text) => new(Id, QuestionId, text);
}

/// <summary>Facilitator-accepted; contributes to the Initiative's official structured fields.</summary>
public sealed record AcceptedResponse : DomainExpertResponse
{
    internal AcceptedResponse(ResponseId id, QuestionId questionId, string text) : base(id, questionId, text)
    {
    }

    /// <summary>Rehydrates a previously-persisted Accepted response.</summary>
    public static AcceptedResponse CreateExisting(ResponseId id, QuestionId questionId, string text) => new(id, questionId, text);
}
