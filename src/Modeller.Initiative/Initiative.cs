namespace Modeller.Initiative;

/// <summary>
/// The single growing record an Initiative is, per issue #86's resolution: there is no separately-named
/// "Business Problem Brief" — Discover and Frame populate this record's structured fields directly, and
/// Shape adds selected interventions to the same record. Adapted from Business Statement's
/// <c>DiscoverySession</c> (M:\business-statement\src\BusinessStatement.Domain\DiscoverySession.cs),
/// scoped to v1's three roles and mechanics per issue #83's resolution — see issue #88's "explicitly not
/// built here" list for what was deliberately left out.
/// </summary>
public sealed class InitiativeSession
{
    private static readonly Participant AgentParticipant = new(ParticipantId.New(), "Agent", ParticipantRole.Agent);

    /// <summary>The fixed session-scoped identity that AI-proposed Prompted Questions are attributed to.</summary>
    public static ParticipantId AgentParticipantId => AgentParticipant.Id;

    private readonly List<Participant> _participants = [];
    private readonly List<PromptedQuestion> _questions = [];
    private readonly List<DomainExpertResponse> _responses = [];
    private readonly List<SelectedIntervention> _selectedInterventions = [];
    private readonly List<GateOverride> _gateOverrides = [];

    public InitiativeId Id { get; }
    public string OriginalChangeRequest { get; }
    public IReadOnlyList<Participant> Participants => _participants;
    public IReadOnlyList<PromptedQuestion> Questions => _questions;
    public IReadOnlyList<DomainExpertResponse> Responses => _responses;
    public IReadOnlyList<SelectedIntervention> SelectedInterventions => _selectedInterventions;
    public IReadOnlyList<GateOverride> GateOverrides => _gateOverrides;
    public GateEvaluation? LatestDiscoveryGateEvaluation { get; private set; }
    public GateEvaluation? LatestShapeGateEvaluation { get; private set; }

    /// <summary>Null while the Initiative is active; set once finalized (reopen clears it — not irreversible in v1).</summary>
    public InitiativeFinalization? Finalization { get; private set; }

    private InitiativeSession(InitiativeId id, string originalChangeRequest)
    {
        Id = id;
        OriginalChangeRequest = string.IsNullOrWhiteSpace(originalChangeRequest)
            ? throw new ArgumentException("The original change request is required.", nameof(originalChangeRequest))
            : originalChangeRequest.Trim();
    }

    public static InitiativeSession Create(InitiativeId id, string originalChangeRequest)
    {
        var session = new InitiativeSession(id, originalChangeRequest);
        session._participants.Add(AgentParticipant);
        return session;
    }

    /// <summary>Rehydrates an Initiative from previously persisted state without re-running live-mutation invariants.</summary>
    public static InitiativeSession Restore(
        InitiativeId id,
        string originalChangeRequest,
        IEnumerable<Participant> participants,
        IEnumerable<PromptedQuestion> questions,
        IEnumerable<DomainExpertResponse> responses,
        IEnumerable<SelectedIntervention>? selectedInterventions = null,
        IEnumerable<GateOverride>? gateOverrides = null,
        GateEvaluation? latestDiscoveryGateEvaluation = null,
        GateEvaluation? latestShapeGateEvaluation = null,
        InitiativeFinalization? finalization = null)
    {
        var session = new InitiativeSession(id, originalChangeRequest);
        session._participants.Clear();
        session._participants.AddRange(participants);
        session._questions.AddRange(questions);
        session._responses.AddRange(responses);
        session._selectedInterventions.AddRange(selectedInterventions ?? []);
        session._gateOverrides.AddRange(gateOverrides ?? []);
        session.LatestDiscoveryGateEvaluation = latestDiscoveryGateEvaluation;
        session.LatestShapeGateEvaluation = latestShapeGateEvaluation;
        session.Finalization = finalization;
        return session;
    }

    public void AddParticipant(Participant participant)
    {
        EnsureActive();
        if (participant.Role == ParticipantRole.Agent)
        {
            throw new InvalidOperationException("The Agent participant is fixed at creation and cannot be added.");
        }

        if (_participants.Any(p => p.Role == participant.Role))
        {
            throw new InvalidOperationException($"A session can only have one {participant.Role} participant.");
        }

        _participants.Add(participant);
    }

    public QuestionId ProposeQuestion(string text, ParticipantId proposedBy, ParticipantRole authorRole, InitiativeField field)
    {
        EnsureActive();
        if (authorRole is not (ParticipantRole.Facilitator or ParticipantRole.Agent))
        {
            throw new InvalidOperationException("Only the Facilitator or the Agent Participant can propose a Prompted Question.");
        }

        var participant = _participants.SingleOrDefault(p => p.Id == proposedBy)
            ?? throw new InvalidOperationException("Unknown participant.");
        if (participant.Role != authorRole)
        {
            throw new InvalidOperationException("Question provenance must match the actual participant role.");
        }

        var question = new ProposedQuestion(QuestionId.New(), text, proposedBy, authorRole, field);
        _questions.Add(question);
        return question.Id;
    }

    public void SendQuestion(QuestionId questionId)
    {
        EnsureActive();
        var index = _questions.FindIndex(q => q.Id == questionId);
        if (index < 0 || _questions[index] is not ProposedQuestion proposed)
        {
            throw new InvalidOperationException("Only a proposed question can be sent to the Domain Expert.");
        }

        _questions[index] = proposed.SendToDomainExpert();
    }

    public void EditProposedQuestion(QuestionId questionId, string newText)
    {
        EnsureActive();
        var index = _questions.FindIndex(q => q.Id == questionId);
        if (index < 0 || _questions[index] is not ProposedQuestion proposed)
        {
            throw new InvalidOperationException("Only a proposed question can be edited.");
        }

        _questions[index] = proposed.WithText(newText);
    }

    public void RejectProposedQuestion(QuestionId questionId)
    {
        EnsureActive();
        var index = _questions.FindIndex(q => q.Id == questionId);
        if (index < 0 || _questions[index] is not ProposedQuestion proposed)
        {
            throw new InvalidOperationException("Only a proposed question can be rejected.");
        }

        _questions[index] = proposed.Reject();
    }

    public ResponseId SubmitResponse(QuestionId questionId, string text)
    {
        EnsureActive();
        if (_questions.SingleOrDefault(q => q.Id == questionId) is not SentQuestion)
        {
            throw new InvalidOperationException("A response can only be submitted for a question sent to the Domain Expert.");
        }

        var response = new PendingResponse(ResponseId.New(), questionId, text);
        _responses.Add(response);
        return response.Id;
    }

    public void AcceptResponse(ResponseId responseId)
    {
        EnsureActive();
        var index = _responses.FindIndex(r => r.Id == responseId);
        if (index < 0 || _responses[index] is not PendingResponse pending)
        {
            throw new InvalidOperationException("Only a pending response can be accepted.");
        }

        _responses[index] = pending.Accept();
    }

    public InterventionId SelectIntervention(InterventionType type, string description, string rationale)
    {
        EnsureActive();
        var intervention = new SelectedIntervention(InterventionId.New(), type, description, rationale);
        _selectedInterventions.Add(intervention);
        return intervention.Id;
    }

    public void WithdrawIntervention(InterventionId interventionId)
    {
        EnsureActive();
        var removed = _selectedInterventions.RemoveAll(i => i.Id == interventionId);
        if (removed == 0)
        {
            throw new InvalidOperationException("Unknown intervention.");
        }
    }

    /// <summary>Records a cross-reference link into a design workspace. Never scaffolds model content (issue #83).</summary>
    public void LinkDesignWorkspace(InterventionId interventionId, string reference)
    {
        EnsureActive();
        var index = _selectedInterventions.FindIndex(i => i.Id == interventionId);
        if (index < 0)
        {
            throw new InvalidOperationException("Unknown intervention.");
        }

        _selectedInterventions[index] = _selectedInterventions[index].WithDesignWorkspaceReference(reference);
    }

    /// <summary>
    /// Records a gate evaluation. If a recommendation is provided, it is proposed as a new Prompted
    /// Question attributed to the Agent Participant — the gate stays strictly advisory either way.
    /// </summary>
    public void RecordGateEvaluation(GateEvaluation evaluation, string? recommendedQuestionText = null, InitiativeField? recommendedQuestionField = null)
    {
        EnsureActive();
        QuestionId? recommendedQuestionId = null;
        if (!string.IsNullOrWhiteSpace(recommendedQuestionText) && recommendedQuestionField is not null)
        {
            recommendedQuestionId = ProposeQuestion(recommendedQuestionText, AgentParticipant.Id, ParticipantRole.Agent, recommendedQuestionField.Value);
        }

        var recorded = evaluation with { RecommendedQuestionId = recommendedQuestionId };
        switch (recorded.Kind)
        {
            case GateKind.Discovery:
                LatestDiscoveryGateEvaluation = recorded;
                break;
            case GateKind.Shape:
                LatestShapeGateEvaluation = recorded;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(evaluation));
        }
    }

    /// <summary>
    /// Records the Facilitator's dismissal of a flagged gap as a <see cref="DismissedGateFinding"/>
    /// override. The dismissal is retained, never deleted, and never changes the evaluation itself —
    /// both gates stay strictly advisory.
    /// </summary>
    public GateOverrideId DismissGateFinding(GateKind kind, GateCheck check, string? reason)
    {
        EnsureActive();
        var latest = kind == GateKind.Discovery ? LatestDiscoveryGateEvaluation : LatestShapeGateEvaluation;
        var finding = latest?.Results.FirstOrDefault(r => r.Check == check && !r.Passed)
            ?? throw new InvalidOperationException("Only a failed check in the latest gate evaluation can be dismissed.");

        if (_gateOverrides.OfType<DismissedGateFinding>().Any(d => d.Kind == kind && d.Finding == finding))
        {
            throw new InvalidOperationException("This gate finding has already been dismissed.");
        }

        var dismissal = new DismissedGateFinding(GateOverrideId.New(), kind, finding, reason);
        _gateOverrides.Add(dismissal);
        return dismissal.Id;
    }

    public InitiativeStructuredFields BuildStructuredFields()
    {
        var accepted = _responses.OfType<AcceptedResponse>()
            .Join(_questions, r => r.QuestionId, q => q.Id, (r, q) => (r.Text, q.Field))
            .ToLookup(x => x.Field, x => x.Text);

        return new InitiativeStructuredFields(
            OriginalChangeRequest,
            [.. accepted[InitiativeField.ProblemStatement]],
            [.. accepted[InitiativeField.AffectedUsers]],
            [.. accepted[InitiativeField.PainPoints]],
            [.. accepted[InitiativeField.Outcomes]],
            [.. accepted[InitiativeField.SuccessCriteria]],
            [.. accepted[InitiativeField.NonGoals]],
            [.. accepted[InitiativeField.Constraints]],
            [.. accepted[InitiativeField.Assumptions]],
            [.. accepted[InitiativeField.OpenQuestions]],
            [.. accepted[InitiativeField.Risks]],
            [.. _selectedInterventions]);
    }

    /// <summary>
    /// Finalizes the Initiative, snapshotting its structured fields as markdown. Neither gate ever
    /// blocks this: if a gate's latest evaluation has failing checks, the Initiative is finalized as
    /// <see cref="WithOpenGateFindings"/> and the disagreement is recorded as a
    /// <see cref="FinalizedAgainstGate"/> override carrying those findings.
    /// </summary>
    public InitiativeFinalization FinalizeInitiative(DateTimeOffset finalizedAt, string? reason)
    {
        if (Finalization is not null)
        {
            throw new InvalidOperationException("The Initiative is already finalized.");
        }

        var snapshot = BuildStructuredFields().ToMarkdown();
        var hasOpenFindings = false;

        foreach (var (kind, evaluation) in new[] { (GateKind.Discovery, LatestDiscoveryGateEvaluation), (GateKind.Shape, LatestShapeGateEvaluation) })
        {
            var failed = evaluation?.Results.Where(r => !r.Passed).ToList() ?? [];
            if (failed.Count == 0) continue;
            hasOpenFindings = true;
            _gateOverrides.Add(new FinalizedAgainstGate(GateOverrideId.New(), kind, failed, reason));
        }

        Finalization = hasOpenFindings ? new WithOpenGateFindings(snapshot, finalizedAt) : new Clean(snapshot, finalizedAt);
        return Finalization;
    }

    /// <summary>Reopens a finalized Initiative. Not irreversible in v1; gate overrides are retained.</summary>
    public void Reopen()
    {
        if (Finalization is null)
        {
            throw new InvalidOperationException("Only a finalized Initiative can be reopened.");
        }

        Finalization = null;
    }

    private void EnsureActive()
    {
        if (Finalization is not null)
        {
            throw new InvalidOperationException("The Initiative is finalized. Reopen it before making changes.");
        }
    }
}
