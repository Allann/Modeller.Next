using Modeller.Initiative;
using Xunit;

namespace Modeller.Initiative.Tests;

public class AgentAdvisorTests
{
    [Fact]
    public void AgentAdvisorResult_Failure_WithReadyStatus_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            AgentAdvisorResult<AgentQuestionSuggestion>.Failure(AgentEvaluationStatus.Ready, "not actually a failure"));
    }

    [Fact]
    public void AgentAdvisorResult_Success_Succeeded_IsTrue()
    {
        var result = AgentAdvisorResult<AgentQuestionSuggestion>.Success(new AgentQuestionSuggestion("Why?", InitiativeField.ProblemStatement));

        Assert.True(result.Succeeded);
        Assert.Equal(AgentEvaluationStatus.Ready, result.Status);
    }

    [Fact]
    public void AgentAdvisorResult_Failure_Succeeded_IsFalse()
    {
        var result = AgentAdvisorResult<AgentQuestionSuggestion>.Failure(AgentEvaluationStatus.NotConfigured, "no advisor configured");

        Assert.False(result.Succeeded);
        Assert.Null(result.Value);
    }

    [Fact]
    public void AgentAdvisorException_WithReadyFailureKind_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new AgentAdvisorException(AgentEvaluationStatus.Ready, "Ready is not a failure."));
    }

    [Fact]
    public async Task HumanOnlyAgentAdvisor_EveryMethod_ReturnsNotConfigured_WithoutThrowing()
    {
        var advisor = new HumanOnlyAgentAdvisor();
        var fields = EmptyFields();

        var question = await advisor.ProposeQuestionAsync(new ProposeQuestionRequest("Build a new system", fields, InitiativeField.ProblemStatement), TestContext.Current.CancellationToken);
        var fieldUpdate = await advisor.DraftFieldUpdateAsync(new DraftFieldUpdateRequest(InitiativeField.PainPoints, "It's slow.", []), TestContext.Current.CancellationToken);
        var interventions = await advisor.ProposeInterventionsAsync(new ProposeInterventionsRequest(fields), TestContext.Current.CancellationToken);
        var gate = await advisor.EvaluateGateAsync(new GateEvaluationRequest(GateKind.Discovery, fields), TestContext.Current.CancellationToken);

        Assert.Equal(AgentEvaluationStatus.NotConfigured, question.Status);
        Assert.Equal(AgentEvaluationStatus.NotConfigured, fieldUpdate.Status);
        Assert.Equal(AgentEvaluationStatus.NotConfigured, interventions.Status);
        Assert.Equal(AgentEvaluationStatus.NotConfigured, gate.Status);
    }

    [Fact]
    public async Task FullInitiativeLifecycle_WithHumanOnlyAgentAdvisorWiredIn_CompletesWithoutAnyAiContribution()
    {
        // A HumanOnlyAgentAdvisor is deliberately consulted at every opportunity an AI could
        // contribute, proving the degrade-gracefully contract concretely rather than just never
        // wiring an advisor in at all (which #88's own tests already cover for the aggregate alone).
        IAgentAdvisor advisor = new HumanOnlyAgentAdvisor();

        var session = InitiativeSession.CreateNew("Build us a new approval system");
        var facilitator = Participant.CreateNew("Alex", ParticipantRole.Facilitator);
        session = session.AddParticipant(facilitator).AddParticipant(Participant.CreateNew("Jordan", ParticipantRole.DomainExpert));

        var questionSuggestion = await advisor.ProposeQuestionAsync(
            new ProposeQuestionRequest(session.OriginalChangeRequest, session.BuildStructuredFields(), InitiativeField.PainPoints),
            TestContext.Current.CancellationToken);
        Assert.False(questionSuggestion.Succeeded);

        // The Facilitator proceeds manually since the advisor offered nothing.
        var (afterPropose, questionId) = session.ProposeQuestion("What's painful today?", facilitator.Id, ParticipantRole.Facilitator, InitiativeField.PainPoints);
        session = afterPropose.SendQuestion(questionId);
        ResponseId responseId;
        (session, responseId) = session.SubmitResponse(questionId, "Decisions take twelve days.");

        var draftSuggestion = await advisor.DraftFieldUpdateAsync(
            new DraftFieldUpdateRequest(InitiativeField.PainPoints, "Decisions take twelve days.", []),
            TestContext.Current.CancellationToken);
        Assert.False(draftSuggestion.Succeeded);
        session = session.AcceptResponse(responseId);

        var interventionSuggestions = await advisor.ProposeInterventionsAsync(
            new ProposeInterventionsRequest(session.BuildStructuredFields()), TestContext.Current.CancellationToken);
        Assert.False(interventionSuggestions.Succeeded);
        (session, _) = session.SelectIntervention(InterventionType.Process, "Remove a duplicate approval", "Cuts two days.");

        var gateSuggestion = await advisor.EvaluateGateAsync(
            new GateEvaluationRequest(GateKind.Shape, session.BuildStructuredFields()), TestContext.Current.CancellationToken);
        Assert.False(gateSuggestion.Succeeded);
        session = session.RecordGateEvaluation(new GateEvaluation(
            GateKind.Shape,
            [new GateCheckResult(GateCheck.NoActionWasConsidered, false, "Not explicitly considered.")],
            null, DateTimeOffset.UtcNow, AgentEvaluationStatus.NotConfigured));

        session = session.FinalizeInitiative(DateTimeOffset.UtcNow, "Proceeding without AI assistance.");

        Assert.IsType<WithOpenGateFindings>(session.Finalization);
        Assert.Contains("Decisions take twelve days.", session.BuildStructuredFields().PainPoints);
    }

    private static InitiativeStructuredFields EmptyFields() =>
        new("Build a new system", [], [], [], [], [], [], [], [], [], [], []);
}
