using Modeller.Initiative;
using Xunit;

namespace Modeller.Initiative.Tests;

public class InitiativeSessionTests
{
    private static InitiativeSession CreateWithFacilitatorAndDomainExpert(out Participant facilitator, out Participant domainExpert)
    {
        var session = InitiativeSession.CreateNew("Build us a new approval system");
        facilitator = Participant.CreateNew("Alex", ParticipantRole.Facilitator);
        domainExpert = Participant.CreateNew("Jordan", ParticipantRole.DomainExpert);
        session = session.AddParticipant(facilitator).AddParticipant(domainExpert);
        return session;
    }

    [Fact]
    public void FullLifecycle_DiscoverThroughFrameThroughShapeThroughFinalize_HumanOnly_Succeeds()
    {
        // Entirely human-only: only Facilitator and Domain Expert act. No Agent Participant
        // proposal is ever made, proving the app works without AI per issues #83/#86.
        var session = CreateWithFacilitatorAndDomainExpert(out var facilitator, out _);

        // Discover
        QuestionId painQuestionId;
        (session, painQuestionId) = session.ProposeQuestion(
            "What's painful about the current approval process?", facilitator.Id, ParticipantRole.Facilitator, InitiativeField.PainPoints);
        session = session.SendQuestion(painQuestionId);
        ResponseId painResponseId;
        (session, painResponseId) = session.SubmitResponse(painQuestionId, "Decisions take twelve days on average.");
        session = session.AcceptResponse(painResponseId);

        QuestionId usersQuestionId;
        (session, usersQuestionId) = session.ProposeQuestion(
            "Who is affected?", facilitator.Id, ParticipantRole.Facilitator, InitiativeField.AffectedUsers);
        session = session.SendQuestion(usersQuestionId);
        ResponseId usersResponseId;
        (session, usersResponseId) = session.SubmitResponse(usersQuestionId, "Customers awaiting a decision, and the assessors making it.");
        session = session.AcceptResponse(usersResponseId);

        // Frame
        QuestionId outcomeQuestionId;
        (session, outcomeQuestionId) = session.ProposeQuestion(
            "What would success look like?", facilitator.Id, ParticipantRole.Facilitator, InitiativeField.Outcomes);
        session = session.SendQuestion(outcomeQuestionId);
        ResponseId outcomeResponseId;
        (session, outcomeResponseId) = session.SubmitResponse(outcomeQuestionId, "Decide within forty-eight hours.");
        session = session.AcceptResponse(outcomeResponseId);

        var discoveryGate = new GateEvaluation(
            GateKind.Discovery,
            [new GateCheckResult(GateCheck.AffectedUsersNamed, Passed: true, "Affected users are named.")],
            RecommendedQuestionId: null,
            DateTimeOffset.UtcNow,
            AgentEvaluationStatus.Ready);
        session = session.RecordGateEvaluation(discoveryGate);
        Assert.True(session.LatestDiscoveryGateEvaluation!.AllPassed);

        // Shape: a mixed response, matching the shipped landing-page example.
        InterventionId processId, technologyId;
        (session, processId) = session.SelectIntervention(InterventionType.Process, "Remove a duplicate approval", "Cuts two days out of the cycle on its own.");
        (session, _) = session.SelectIntervention(InterventionType.People, "Delegate low-risk decisions", "Assessors can decide without escalation for low-risk cases.");
        (session, technologyId) = session.SelectIntervention(InterventionType.Technology, "Automate document checks", "Removes the slowest manual step.");
        session = session.LinkDesignWorkspace(technologyId, "system-design/customer-approvals/application-decision");

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
        session = session.RecordGateEvaluation(shapeGate);
        Assert.False(session.LatestShapeGateEvaluation!.AllPassed);

        // Finalize despite the failing Shape Gate check — the gate is advisory, never blocking.
        session = session.FinalizeInitiative(DateTimeOffset.UtcNow, reason: "Team accepted the risk; revisit no-action next cycle.");
        var finalization = session.Finalization!;

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

        session = session.RecordGateEvaluation(new GateEvaluation(
            GateKind.Discovery,
            [new GateCheckResult(GateCheck.OriginalChangeRequestCaptured, true, "Captured.")],
            null, DateTimeOffset.UtcNow, AgentEvaluationStatus.Ready));
        session = session.RecordGateEvaluation(new GateEvaluation(
            GateKind.Shape,
            [new GateCheckResult(GateCheck.NoActionWasConsidered, true, "Considered and rejected.")],
            null, DateTimeOffset.UtcNow, AgentEvaluationStatus.Ready));

        session = session.FinalizeInitiative(DateTimeOffset.UtcNow, reason: null);

        Assert.IsType<Clean>(session.Finalization);
        Assert.Empty(session.GateOverrides);
    }

    [Fact]
    public void DismissedGateFinding_IsExcludedFromFinalizedAgainstGate()
    {
        // Regression test: finalizing must not re-raise a finding the Facilitator already dismissed
        // via DismissGateFinding — a dismissal is the Facilitator's recorded judgment call, not
        // something a later finalize should silently override a second time.
        var session = CreateWithFacilitatorAndDomainExpert(out _, out _);
        session = session.RecordGateEvaluation(new GateEvaluation(
            GateKind.Discovery,
            [
                new GateCheckResult(GateCheck.RisksAreListed, false, "No risks captured yet."),
                new GateCheckResult(GateCheck.AssumptionsAreListed, false, "No assumptions captured yet."),
            ],
            null, DateTimeOffset.UtcNow, AgentEvaluationStatus.Ready));

        session = session.DismissGateFinding(GateKind.Discovery, GateCheck.RisksAreListed, "Accepted for now.").Session;
        session = session.FinalizeInitiative(DateTimeOffset.UtcNow, reason: "Proceeding with one open item.");

        Assert.IsType<WithOpenGateFindings>(session.Finalization);
        var finalizeOverride = Assert.Single(session.GateOverrides.OfType<FinalizedAgainstGate>());
        Assert.DoesNotContain(finalizeOverride.Findings, f => f.Check == GateCheck.RisksAreListed);
        Assert.Contains(finalizeOverride.Findings, f => f.Check == GateCheck.AssumptionsAreListed);
    }

    [Fact]
    public void DismissGateFinding_ThenReDismissingTheSameFinding_Throws()
    {
        var session = CreateWithFacilitatorAndDomainExpert(out _, out _);
        session = session.RecordGateEvaluation(new GateEvaluation(
            GateKind.Discovery,
            [new GateCheckResult(GateCheck.RisksAreListed, false, "No risks captured yet.")],
            null, DateTimeOffset.UtcNow, AgentEvaluationStatus.Ready));

        session = session.DismissGateFinding(GateKind.Discovery, GateCheck.RisksAreListed, "Accepted for now.").Session;

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
            session.AddParticipant(Participant.CreateNew("Sam", ParticipantRole.Facilitator)));
    }

    [Fact]
    public void EnsureActive_MutatingAFinalizedInitiative_ThrowsUntilReopened()
    {
        var session = CreateWithFacilitatorAndDomainExpert(out var facilitator, out _);
        session = session.FinalizeInitiative(DateTimeOffset.UtcNow, reason: null);

        Assert.Throws<InvalidOperationException>(() =>
            session.ProposeQuestion("Late question", facilitator.Id, ParticipantRole.Facilitator, InitiativeField.Risks));

        session = session.Reopen();
        var (reopened, questionId) = session.ProposeQuestion("Late question", facilitator.Id, ParticipantRole.Facilitator, InitiativeField.Risks);
        Assert.NotEqual(default, questionId);
        Assert.Contains(reopened.Questions, q => q.Id == questionId);
    }

    [Fact]
    public void SelectIntervention_WithoutTechnologyType_CannotLinkDesignWorkspace()
    {
        var session = CreateWithFacilitatorAndDomainExpert(out _, out _);
        var (updated, processId) = session.SelectIntervention(InterventionType.Process, "Remove a duplicate approval", "Cuts two days.");

        Assert.Throws<InvalidOperationException>(() => updated.LinkDesignWorkspace(processId, "system-design/whatever"));
    }

    [Fact]
    public void SelectIntervention_TechnologyType_IsFlaggedToContinue_BeforeAnyWorkspaceReferenceExists()
    {
        // The continuation flag is independent of the reference — it must be readable (e.g. by a UI
        // showing "queued for System Design") before any workspace has actually been linked.
        var session = CreateWithFacilitatorAndDomainExpert(out _, out _);
        var (updated, technologyId) = session.SelectIntervention(InterventionType.Technology, "Automate document checks", "Removes a manual step.");

        var intervention = updated.SelectedInterventions.Single(i => i.Id == technologyId);
        Assert.True(intervention.ContinuesToDesignWorkspace);
        Assert.Null(intervention.DesignWorkspaceReference);
    }

    [Fact]
    public void CreateExisting_WithTwoFacilitators_Throws()
    {
        // Restore must re-validate the same participant-cardinality invariants AddParticipant
        // enforces during live use, so a hand-edited or corrupted persisted document cannot produce
        // an invalid session.
        var agent = Participant.CreateNew("Agent", ParticipantRole.Agent);
        var facilitatorOne = Participant.CreateNew("Alex", ParticipantRole.Facilitator);
        var facilitatorTwo = Participant.CreateNew("Sam", ParticipantRole.Facilitator);

        Assert.Throws<ArgumentException>(() => InitiativeSession.CreateExisting(
            InitiativeId.New(), "Build us a new approval system", [agent, facilitatorOne, facilitatorTwo], [], []));
    }

    [Fact]
    public void CreateExisting_WithNoAgentParticipant_Throws()
    {
        var facilitator = Participant.CreateNew("Alex", ParticipantRole.Facilitator);

        Assert.Throws<ArgumentException>(() => InitiativeSession.CreateExisting(
            InitiativeId.New(), "Build us a new approval system", [facilitator], [], []));
    }
}
