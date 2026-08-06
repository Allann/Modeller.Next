using Modeller.Initiative;

namespace Modeller.Api.Initiative;

public static class InitiativeSessionMapper
{
    public static InitiativeSessionDto ToDto(InitiativeSession session) => new(
        session.Id.Value,
        session.OriginalChangeRequest,
        [.. session.Participants.Select(p => new ParticipantDto(p.Id.Value, p.DisplayName, p.Role.ToString()))],
        [.. session.Questions.Select(ToDto)],
        [.. session.Responses.Select(ToDto)],
        [.. session.SelectedInterventions.Select(ToDto)],
        [.. session.GateOverrides.Select(ToDto)],
        session.LatestDiscoveryGateEvaluation is { } discovery ? ToDto(discovery) : null,
        session.LatestShapeGateEvaluation is { } shape ? ToDto(shape) : null,
        session.Finalization is { } finalization ? ToDto(finalization) : null);

    public static InitiativeSession ToDomain(InitiativeSessionDto dto) => InitiativeSession.Restore(
        InitiativeId.FromExisting(dto.Id),
        dto.OriginalChangeRequest,
        dto.Participants.Select(ToDomain),
        dto.Questions.Select(ToDomain),
        dto.Responses.Select(ToDomain),
        dto.SelectedInterventions.Select(ToDomain),
        dto.GateOverrides.Select(ToDomain),
        dto.LatestDiscoveryGateEvaluation is { } discovery ? ToDomain(discovery) : null,
        dto.LatestShapeGateEvaluation is { } shape ? ToDomain(shape) : null,
        dto.Finalization is { } finalization ? ToDomain(finalization) : null);

    private static QuestionDto ToDto(PromptedQuestion question) => new(
        question.Id.Value,
        question.Text,
        question.ProposedBy.Value,
        question.AuthorRole.ToString(),
        question.Field.ToString(),
        question switch
        {
            ProposedQuestion => "Proposed",
            SentQuestion => "Sent",
            RejectedQuestion => "Rejected",
            _ => throw new NotSupportedException($"Unknown PromptedQuestion type {question.GetType()}."),
        });

    private static PromptedQuestion ToDomain(QuestionDto dto)
    {
        var id = QuestionId.FromExisting(dto.Id);
        var proposedBy = ParticipantId.FromExisting(dto.ProposedBy);
        var authorRole = Enum.Parse<ParticipantRole>(dto.AuthorRole);
        var field = Enum.Parse<InitiativeField>(dto.Field);
        return dto.Status switch
        {
            "Proposed" => new ProposedQuestion(id, dto.Text, proposedBy, authorRole, field),
            "Sent" => new SentQuestion(id, dto.Text, proposedBy, authorRole, field),
            "Rejected" => new RejectedQuestion(id, dto.Text, proposedBy, authorRole, field),
            _ => throw new NotSupportedException($"Unknown question status '{dto.Status}'."),
        };
    }

    private static ResponseDto ToDto(DomainExpertResponse response) => new(
        response.Id.Value,
        response.QuestionId.Value,
        response.Text,
        response switch
        {
            PendingResponse => "Pending",
            AcceptedResponse => "Accepted",
            _ => throw new NotSupportedException($"Unknown DomainExpertResponse type {response.GetType()}."),
        });

    private static DomainExpertResponse ToDomain(ResponseDto dto)
    {
        var id = ResponseId.FromExisting(dto.Id);
        var questionId = QuestionId.FromExisting(dto.QuestionId);
        return dto.Status switch
        {
            "Pending" => new PendingResponse(id, questionId, dto.Text),
            "Accepted" => new AcceptedResponse(id, questionId, dto.Text),
            _ => throw new NotSupportedException($"Unknown response status '{dto.Status}'."),
        };
    }

    private static SelectedInterventionDto ToDto(SelectedIntervention intervention) => new(
        intervention.Id.Value, intervention.Type.ToString(), intervention.Description, intervention.Rationale, intervention.DesignWorkspaceReference);

    private static SelectedIntervention ToDomain(SelectedInterventionDto dto) => new(
        InterventionId.FromExisting(dto.Id),
        Enum.Parse<InterventionType>(dto.Type),
        dto.Description,
        dto.Rationale,
        dto.DesignWorkspaceReference);

    private static GateCheckResultDto ToDto(GateCheckResult result) => new(result.Check.ToString(), result.Passed, result.Reason);

    private static GateCheckResult ToDomain(GateCheckResultDto dto) => new(Enum.Parse<GateCheck>(dto.Check), dto.Passed, dto.Reason);

    private static GateEvaluationDto ToDto(GateEvaluation evaluation) => new(
        evaluation.Kind.ToString(),
        [.. evaluation.Results.Select(ToDto)],
        evaluation.RecommendedQuestionId?.Value,
        evaluation.EvaluatedAt,
        evaluation.AgentStatus.ToString());

    private static GateEvaluation ToDomain(GateEvaluationDto dto) => new(
        Enum.Parse<GateKind>(dto.Kind),
        [.. dto.Results.Select(ToDomain)],
        dto.RecommendedQuestionId is { } id ? QuestionId.FromExisting(id) : null,
        dto.EvaluatedAt,
        Enum.Parse<AgentEvaluationStatus>(dto.AgentStatus));

    private static GateOverrideDto ToDto(GateOverride gateOverride) => gateOverride switch
    {
        DismissedGateFinding dismissed => new(
            dismissed.Id.Value, dismissed.Kind.ToString(), "Dismissed", ToDto(dismissed.Finding), null, dismissed.Reason),
        FinalizedAgainstGate finalized => new(
            finalized.Id.Value, finalized.Kind.ToString(), "FinalizedAgainstGate", null, [.. finalized.Findings.Select(ToDto)], finalized.Reason),
        _ => throw new NotSupportedException($"Unknown GateOverride type {gateOverride.GetType()}."),
    };

    private static GateOverride ToDomain(GateOverrideDto dto)
    {
        var id = GateOverrideId.FromExisting(dto.Id);
        var kind = Enum.Parse<GateKind>(dto.Kind);
        return dto.OverrideType switch
        {
            "Dismissed" => new DismissedGateFinding(id, kind, ToDomain(dto.DismissedFinding!), dto.Reason),
            "FinalizedAgainstGate" => new FinalizedAgainstGate(id, kind, [.. dto.FinalizedFindings!.Select(ToDomain)], dto.Reason),
            _ => throw new NotSupportedException($"Unknown gate override type '{dto.OverrideType}'."),
        };
    }

    private static FinalizationDto ToDto(InitiativeFinalization finalization) => new(
        finalization is Clean ? "Clean" : "WithOpenGateFindings",
        finalization.MarkdownSnapshot,
        finalization.FinalizedAt);

    private static InitiativeFinalization ToDomain(FinalizationDto dto) => dto.Status switch
    {
        "Clean" => new Clean(dto.MarkdownSnapshot, dto.FinalizedAt),
        "WithOpenGateFindings" => new WithOpenGateFindings(dto.MarkdownSnapshot, dto.FinalizedAt),
        _ => throw new NotSupportedException($"Unknown finalization status '{dto.Status}'."),
    };

    private static Participant ToDomain(ParticipantDto dto) => new(
        ParticipantId.FromExisting(dto.Id), dto.DisplayName, Enum.Parse<ParticipantRole>(dto.Role));

}
