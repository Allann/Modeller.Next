using Modeller.Api.Initiative;
using Modeller.Initiative;
using Xunit;

namespace Modeller.Api.Tests.Initiative;

public sealed class InitiativeSessionMapperTests
{
    [Fact]
    public void ToDto_ThenToDomain_RoundTripsEveryPieceOfState()
    {
        var session = InitiativeSession.CreateNew("Build us a new approval system");
        var facilitator = Participant.CreateNew("Alex", ParticipantRole.Facilitator);
        session = session.AddParticipant(facilitator).AddParticipant(Participant.CreateNew("Jordan", ParticipantRole.DomainExpert));

        QuestionId questionId;
        (session, questionId) = session.ProposeQuestion("What's painful today?", facilitator.Id, ParticipantRole.Facilitator, InitiativeField.PainPoints);
        session = session.SendQuestion(questionId);
        ResponseId responseId;
        (session, responseId) = session.SubmitResponse(questionId, "Decisions take twelve days.");
        session = session.AcceptResponse(responseId);

        QuestionId rejectedQuestionId;
        (session, rejectedQuestionId) = session.ProposeQuestion("Redundant?", facilitator.Id, ParticipantRole.Facilitator, InitiativeField.Risks);
        session = session.RejectProposedQuestion(rejectedQuestionId);

        InterventionId technologyId;
        (session, technologyId) = session.SelectIntervention(Modeller.Initiative.InterventionType.Technology, "Automate document checks", "Removes a manual step.");
        session = session.LinkDesignWorkspace(technologyId, "system-design/customer-approvals");
        (session, _) = session.SelectIntervention(Modeller.Initiative.InterventionType.NoAction, "Do nothing yet", "Baseline for comparison.");

        session = session.RecordGateEvaluation(new GateEvaluation(
            GateKind.Discovery,
            [new GateCheckResult(GateCheck.AffectedUsersNamed, false, "Not yet named.")],
            null, DateTimeOffset.UtcNow, AgentEvaluationStatus.Ready));
        session = session.DismissGateFinding(GateKind.Discovery, GateCheck.AffectedUsersNamed, "Accepted for now.").Session;

        session = session.RecordGateEvaluation(new GateEvaluation(
            GateKind.Shape,
            [new GateCheckResult(GateCheck.NoActionWasConsidered, true, "Considered.")],
            null, DateTimeOffset.UtcNow, AgentEvaluationStatus.Ready));

        session = session.FinalizeInitiative(DateTimeOffset.UtcNow, reason: null);
        var finalization = session.Finalization!;

        var dto = InitiativeSessionMapper.ToDto(session);
        var restored = InitiativeSessionMapper.ToDomain(dto);

        Assert.Equal(session.Id, restored.Id);
        Assert.Equal(session.OriginalChangeRequest, restored.OriginalChangeRequest);
        Assert.Equal(session.Participants.Count, restored.Participants.Count);
        Assert.Equal(session.Questions.Count, restored.Questions.Count);
        Assert.Equal(session.Responses.Count, restored.Responses.Count);
        Assert.Equal(session.SelectedInterventions.Count, restored.SelectedInterventions.Count);
        Assert.Equal(session.GateOverrides.Count, restored.GateOverrides.Count);
        Assert.IsType<RejectedQuestion>(restored.Questions.Single(q => q.Id == rejectedQuestionId));
        Assert.True(restored.SelectedInterventions.Single(i => i.Id == technologyId).ContinuesToDesignWorkspace);
        Assert.NotNull(restored.LatestDiscoveryGateEvaluation);
        Assert.NotNull(restored.LatestShapeGateEvaluation);
        Assert.IsType(finalization.GetType(), restored.Finalization);
        Assert.Equal(finalization.MarkdownSnapshot, restored.Finalization!.MarkdownSnapshot);

        // The restored session is a live aggregate, not just a data bag: it enforces invariants
        // the same way the original did (finalized until reopened).
        Assert.Throws<InvalidOperationException>(() =>
            restored.ProposeQuestion("Late", facilitator.Id, ParticipantRole.Facilitator, InitiativeField.OpenQuestions));
    }

    [Fact]
    public void ToDomainExpertDto_HidesFacilitatorOnlyContent()
    {
        var session = InitiativeSession.CreateNew("Build us a new approval system");
        var facilitator = Participant.CreateNew("Alex", ParticipantRole.Facilitator);
        session = session.AddParticipant(facilitator).AddParticipant(Participant.CreateNew("Jordan", ParticipantRole.DomainExpert));

        QuestionId sentQuestionId;
        (session, sentQuestionId) = session.ProposeQuestion("What's painful today?", facilitator.Id, ParticipantRole.Facilitator, InitiativeField.PainPoints);
        session = session.SendQuestion(sentQuestionId);
        (session, _) = session.ProposeQuestion("Never sent", facilitator.Id, ParticipantRole.Facilitator, InitiativeField.Risks);

        (session, _) = session.SelectIntervention(Modeller.Initiative.InterventionType.Process, "Remove a duplicate approval", "Cuts two days.");
        session = session.RecordGateEvaluation(new GateEvaluation(
            GateKind.Discovery,
            [new GateCheckResult(GateCheck.AffectedUsersNamed, false, "Not yet named.")],
            null, DateTimeOffset.UtcNow, AgentEvaluationStatus.Ready));

        var domainExpertDto = InitiativeSessionMapper.ToDomainExpertDto(session);

        Assert.Single(domainExpertDto.Questions);
        Assert.Equal(sentQuestionId.Value, domainExpertDto.Questions[0].Id);
        Assert.Empty(domainExpertDto.SelectedInterventions);
        Assert.Empty(domainExpertDto.GateOverrides);
        Assert.Null(domainExpertDto.LatestDiscoveryGateEvaluation);
        Assert.Null(domainExpertDto.LatestShapeGateEvaluation);
    }
}
