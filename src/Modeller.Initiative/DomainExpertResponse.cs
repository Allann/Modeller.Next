namespace Modeller.Initiative;

public abstract class DomainExpertResponse(ResponseId id, QuestionId questionId, string text)
{
    public ResponseId Id { get; } = id;
    public QuestionId QuestionId { get; } = questionId;
    public string Text { get; } = text;
}

/// <summary>Submitted by the Domain Expert but not yet accepted into the Initiative's structured fields.</summary>
public sealed class PendingResponse(ResponseId id, QuestionId questionId, string text)
    : DomainExpertResponse(id, questionId, text)
{
    public AcceptedResponse Accept() => new(Id, QuestionId, Text);

    public PendingResponse Edit(string text) => new(Id, QuestionId, text);
}

/// <summary>Facilitator-accepted; contributes to the Initiative's official structured fields.</summary>
public sealed class AcceptedResponse(ResponseId id, QuestionId questionId, string text)
    : DomainExpertResponse(id, questionId, text);
