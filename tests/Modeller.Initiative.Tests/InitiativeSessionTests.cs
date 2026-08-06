using Modeller.Initiative;
using Xunit;

namespace Modeller.Initiative.Tests;

public class InitiativeSessionTests
{
    private static InitiativeSession CreateWithFacilitatorAndDomainExpert(out Participant facilitator, out Participant domainExpert)
    {
        var session = InitiativeSession.Create(InitiativeId.New(), "Build us a new approval system");
        facilitator = new Participant(ParticipantId.New(), "Alex", ParticipantRole.Facilitator);
        domainExpert = new Participant(ParticipantId.New(), "Jordan", ParticipantRole.DomainExpert);
        session.AddParticipant(facilitator);
        session.AddParticipant(domainExpert);
        return session;
    }

    [Fact]
    public void FullLifecycle_DiscoverThroughFrameThroughShapeThroughFinalize_HumanOnly_Succeeds()
    {
        // Entirely human-only: only Facilitator and Domain Expert act. No Agent Participant
        // proposal is ever made, proving the app works without AI per issues #83/#86.
        var session = CreateWithFacilitatorAndDomainExpert(out var facilitator, out var domainExpert);

        // Discover
        var painQuestionId = session.ProposeQuestion(
            "What's painful about the current approval process?", facilitator.Id, ParticipantRole.Facilitator, InitiativeField.PainPoints);
        session.SendQuestion(painQuestionId);
        var painResponseId = session.SubmitResponse(painQuestionId, "Decisions take twelve days on average.");
        session.AcceptResponse(painResponseId);

        var usersQuestionId = session.ProposeQuestion(
            "Who is affected?", facilitator.Id, ParticipantRole.Facilitator, InitiativeField.AffectedUsers);
        session.SendQuestion(usersQuestionId);
        var usersResponseId = session.SubmitResponse(usersQuestionId, "Customers awaiting a decision, and the assessors making it.");
        session.AcceptResponse(usersResponseId);

        // Frame
        var outcomeQuestionId = session.ProposeQuestion(
            "What would success look like?", facilitator.Id, ParticipantRole.Facilitator, InitiativeField.Outcomes);
        session.SendQuestion(outcomeQuestionId);
        var outcomeResponseId = session.SubmitResponse(outcomeQuestionId, "Decide within forty-eight hours.");
        session.AcceptResponse(outcomeResponseId);

        var discoveryGate = new GateEvaluation(
            GateKind.Discovery,
            [new GateCheckResult(GateCheck.AffectedUsersNamed, Passed: true, "Affected users are named.")],
            RecommendedQuestionId: null,
            DateTimeOffset.UtcNow,
            AgentEvaluationStatus.Ready);
        session.RecordGateEvaluation(discoveryGate);
        Assert.True(session.LatestDiscoveryGateEvaluation!.AllPassed);

        // Shape: a mixed response, matching the shipped landing-page example.
        var processId = session.SelectIntervention(InterventionType.Process, "Remove a duplicate approval", "Cuts two days out of the cycle on its own.");
        var peopleId = session.SelectIntervention(InterventionType.People, "Delegate low-risk decisions", "Assessors can decide without escalation for low-risk cases.");
        var technologyId = session.SelectIntervention(InterventionType.Technology, "Automate document checks", "Removes the slowest manual step.");
        session.LinkDesignWorkspace(technologyId, "system-design/customer-approvals/application-decision");

        Assert.Equal(3, session.SelectedInterventions.Count);
        Assert.False(session.SelectedInterventions.Single(i => i.Id == processId).ContinuesToDesignWorkspace);
        Assert.True(session.SelectedInterventions.Single(i => i.Id == technologyId).ContinuesToDesignWorkspace);

        var shapeGate = new GateEvaluation(
            GateKind.Shape,
            [
                new GateCheckResult(GateCheck.SelectedTechnologyInterventionsHaveRationale, Passed: true, "Every Technology intervention has a rationale."),
                new GateCheckResult(GateCheck.NoActionWasConsidered, Passed: false, "No action was never explicitly considered as a baseline."),
            ],
            RecommendedQuestionId: null,
            DateTimeOffset.UtcNow,
            AgentEvaluationStatus.Ready);
        session.RecordGateEvaluation(shapeGate);
        Assert.False(session.LatestShapeGateEvaluation!.AllPassed);

        // Finalize despite the failing Shape Gate check — the gate is advisory, never blocking.
        var finalization = session.FinalizeInitiative(DateTimeOffset.UtcNow, reason: "Team accepted the risk; revisit no-action next cycle.");

        Assert.IsType<WithOpenGateFindings>(finalization);
        var overrideRecord = Assert.IsType<FinalizedAgainstGate>(Assert.Single(session.GateOverrides));
        Assert.Equal(GateKind.Shape, overrideRecord.Kind);
        Assert.Contains(overrideRecord.Findings, f => f.Check == GateCheck.NoActionWasConsidered);

        var structured = session.BuildStructuredFields();
        Assert.Contains("Decisions take twelve days on average.", structured.PainPoints);
        Assert.Contains("Decide within forty-eight hours.", structured.Outcomes);
        Assert.Contains(structured.SelectedInterventions, i => i.Id == technologyId && i.ContinuesToDesignWorkspace);
        Assert.Contains("Automate document checks", finalization.MarkdownSnapshot);
    }

    [Fact]
    public void FinalizeInitiative_WithAllGateChecksPassing_RecordsCleanFinalization()
    {
        var session = CreateWithFacilitatorAndDomainExpert(out _, out _);

        session.RecordGateEvaluation(new GateEvaluation(
            GateKind.Discovery,
            [new GateCheckResult(GateCheck.OriginalChangeRequestCaptured, true, "Captured.")],
            null, DateTimeOffset.UtcNow, AgentEvaluationStatus.Ready));
        session.RecordGateEvaluation(new GateEvaluation(
            GateKind.Shape,
            [new GateCheckResult(GateCheck.NoActionWasConsidered, true, "Considered and rejected.")],
            null, DateTimeOffset.UtcNow, AgentEvaluationStatus.Ready));

        var finalization = session.FinalizeInitiative(DateTimeOffset.UtcNow, reason: null);

        Assert.IsType<Clean>(finalization);
        Assert.Empty(session.GateOverrides);
    }

    [Fact]
    public void DismissGateFinding_ThenReDismissingTheSameFinding_Throws()
    {
        var session = CreateWithFacilitatorAndDomainExpert(out _, out _);
        session.RecordGateEvaluation(new GateEvaluation(
            GateKind.Discovery,
            [new GateCheckResult(GateCheck.RisksAreListed, false, "No risks captured yet.")],
            null, DateTimeOffset.UtcNow, AgentEvaluationStatus.Ready));

        session.DismissGateFinding(GateKind.Discovery, GateCheck.RisksAreListed, "Accepted for now.");

        Assert.Throws<InvalidOperationException>(() =>
            session.DismissGateFinding(GateKind.Discovery, GateCheck.RisksAreListed, "Accepted again."));
    }

    [Fact]
    public void ProposeQuestion_ByDomainExpert_Throws()
    {
        var session = CreateWithFacilitatorAndDomainExpert(out _, out var domainExpert);

        Assert.Throws<InvalidOperationException>(() =>
            session.ProposeQuestion("Why?", domainExpert.Id, ParticipantRole.DomainExpert, InitiativeField.ProblemStatement));
    }

    [Fact]
    public void AddParticipant_SecondFacilitator_Throws()
    {
        var session = CreateWithFacilitatorAndDomainExpert(out _, out _);

        Assert.Throws<InvalidOperationException>(() =>
            session.AddParticipant(new Participant(ParticipantId.New(), "Sam", ParticipantRole.Facilitator)));
    }

    [Fact]
    public void EnsureActive_MutatingAFinalizedInitiative_ThrowsUntilReopened()
    {
        var session = CreateWithFacilitatorAndDomainExpert(out var facilitator, out _);
        session.FinalizeInitiative(DateTimeOffset.UtcNow, reason: null);

        Assert.Throws<InvalidOperationException>(() =>
            session.ProposeQuestion("Late question", facilitator.Id, ParticipantRole.Facilitator, InitiativeField.Risks));

        session.Reopen();
        var questionId = session.ProposeQuestion("Late question", facilitator.Id, ParticipantRole.Facilitator, InitiativeField.Risks);
        Assert.NotEqual(default, questionId);
    }

    [Fact]
    public void SelectIntervention_WithoutTechnologyType_CannotLinkDesignWorkspace()
    {
        var session = CreateWithFacilitatorAndDomainExpert(out _, out _);
        var processId = session.SelectIntervention(InterventionType.Process, "Remove a duplicate approval", "Cuts two days.");

        Assert.Throws<InvalidOperationException>(() => session.LinkDesignWorkspace(processId, "system-design/whatever"));
    }
}
