using System.Collections.Immutable;

namespace Modeller.Initiative;

/// <summary>
/// The single growing record an Initiative is, per issue #86's resolution: there is no separately-named
/// "Business Problem Brief" — Discover and Frame populate this record's structured fields directly, and
/// Shape adds selected interventions to the same record. Conceptually adapted from Business Statement's
/// <c>DiscoverySession</c> (M:\business-statement\src\BusinessStatement.Domain\DiscoverySession.cs),
/// scoped to v1's three roles and mechanics per issue #83's resolution — see issue #88's "explicitly not
/// built here" list for what was deliberately left out.
///
/// Unlike <c>DiscoverySession</c>, this is an immutable record per
/// docs/coding-standards/domain-modeling/immutable-domain-model.md: every mutator returns a new
/// <see cref="InitiativeSession"/> rather than changing this instance in place, so a caller (the
/// persistence/API layer) must capture and persist the returned value.
/// </summary>
public sealed record InitiativeSession
{
    public InitiativeId Id { get; private init; }
    public string OriginalChangeRequest { get; private init; } = null!;
    public ImmutableList<Participant> Participants { get; private init; } = [];
    public ImmutableList<PromptedQuestion> Questions { get; private init; } = [];
    public ImmutableList<DomainExpertResponse> Responses { get; private init; } = [];
    public ImmutableList<SelectedIntervention> SelectedInterventions { get; private init; } = [];
    public ImmutableList<GateOverride> GateOverrides { get; private init; } = [];
    public GateEvaluation? LatestDiscoveryGateEvaluation { get; private init; }
    public GateEvaluation? LatestShapeGateEvaluation { get; private init; }

    /// <summary>Null while the Initiative is active; set once finalized (reopen clears it — not irreversible in v1).</summary>
    public InitiativeFinalization? Finalization { get; private init; }

    /// <summary>The session-scoped identity AI-proposed Prompted Questions are attributed to. Looked up
    /// from this session's own participants rather than a shared static field, so a session restored in
    /// a different process still resolves to the Agent identity it was actually created with.</summary>
    public ParticipantId AgentParticipantId => Participants.Single(p => p.Role == ParticipantRole.Agent).Id;

    private InitiativeSession()
    {
    }

    public static InitiativeSession CreateNew(string originalChangeRequest) => new()
    {
        Id = InitiativeId.New(),
        OriginalChangeRequest = ValidChangeRequest(originalChangeRequest),
        Participants = [Participant.CreateNew("Agent", ParticipantRole.Agent)],
    };

    /// <summary>Rehydrates an Initiative from previously persisted state, re-validating the same
    /// participant-cardinality invariants <see cref="AddParticipant"/> enforces during live use — a
    /// corrupt or hand-edited persisted document cannot silently produce an invalid session.</summary>
    public static InitiativeSession CreateExisting(
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
        var participantList = participants.ToImmutableList();
        RequireValidParticipants(participantList);

        return new InitiativeSession
        {
            Id = id,
            OriginalChangeRequest = ValidChangeRequest(originalChangeRequest),
            Participants = participantList,
            Questions = questions.ToImmutableList(),
            Responses = responses.ToImmutableList(),
            SelectedInterventions = (selectedInterventions ?? []).ToImmutableList(),
            GateOverrides = (gateOverrides ?? []).ToImmutableList(),
            LatestDiscoveryGateEvaluation = latestDiscoveryGateEvaluation,
            LatestShapeGateEvaluation = latestShapeGateEvaluation,
            Finalization = finalization,
        };
    }

    public InitiativeSession AddParticipant(Participant participant)
    {
        EnsureActive();
        if (participant.Role == ParticipantRole.Agent)
        {
            throw new InvalidOperationException("The Agent participant is fixed at creation and cannot be added.");
        }

        if (Participants.Any(p => p.Role == participant.Role))
        {
            throw new InvalidOperationException($"A session can only have one {participant.Role} participant.");
        }

        return this with { Participants = Participants.Add(participant) };
    }

    public (InitiativeSession Session, QuestionId QuestionId) ProposeQuestion(
        string text, ParticipantId proposedBy, ParticipantRole authorRole, InitiativeField field)
    {
        EnsureActive();
        if (authorRole is not (ParticipantRole.Facilitator or ParticipantRole.Agent))
        {
            throw new InvalidOperationException("Only the Facilitator or the Agent Participant can propose a Prompted Question.");
        }

        var participant = Participants.SingleOrDefault(p => p.Id == proposedBy)
            ?? throw new InvalidOperationException("Unknown participant.");
        if (participant.Role != authorRole)
        {
            throw new InvalidOperationException("Question provenance must match the actual participant role.");
        }

        var question = new ProposedQuestion(QuestionId.New(), text, proposedBy, authorRole, field);
        return (this with { Questions = Questions.Add(question) }, question.Id);
    }

    public InitiativeSession SendQuestion(QuestionId questionId)
    {
        EnsureActive();
        var index = Questions.FindIndex(q => q.Id == questionId);
        if (index < 0 || Questions[index] is not ProposedQuestion proposed)
        {
            throw new InvalidOperationException("Only a proposed question can be sent to the Domain Expert.");
        }

        return this with { Questions = Questions.SetItem(index, proposed.SendToDomainExpert()) };
    }

    public InitiativeSession EditProposedQuestion(QuestionId questionId, string newText)
    {
        EnsureActive();
        var index = Questions.FindIndex(q => q.Id == questionId);
        if (index < 0 || Questions[index] is not ProposedQuestion proposed)
        {
            throw new InvalidOperationException("Only a proposed question can be edited.");
        }

        return this with { Questions = Questions.SetItem(index, proposed.WithText(newText)) };
    }

    public InitiativeSession RejectProposedQuestion(QuestionId questionId)
    {
        EnsureActive();
        var index = Questions.FindIndex(q => q.Id == questionId);
        if (index < 0 || Questions[index] is not ProposedQuestion proposed)
        {
            throw new InvalidOperationException("Only a proposed question can be rejected.");
        }

        return this with { Questions = Questions.SetItem(index, proposed.Reject()) };
    }

    public (InitiativeSession Session, ResponseId ResponseId) SubmitResponse(QuestionId questionId, string text)
    {
        EnsureActive();
        if (Questions.SingleOrDefault(q => q.Id == questionId) is not SentQuestion)
        {
            throw new InvalidOperationException("A response can only be submitted for a question sent to the Domain Expert.");
        }

        var response = new PendingResponse(ResponseId.New(), questionId, text);
        return (this with { Responses = Responses.Add(response) }, response.Id);
    }

    public InitiativeSession AcceptResponse(ResponseId responseId)
    {
        EnsureActive();
        var index = Responses.FindIndex(r => r.Id == responseId);
        if (index < 0 || Responses[index] is not PendingResponse pending)
        {
            throw new InvalidOperationException("Only a pending response can be accepted.");
        }

        return this with { Responses = Responses.SetItem(index, pending.Accept()) };
    }

    public (InitiativeSession Session, InterventionId InterventionId) SelectIntervention(
        InterventionType type, string description, string rationale)
    {
        EnsureActive();
        var intervention = SelectedIntervention.CreateNew(type, description, rationale);
        return (this with { SelectedInterventions = SelectedInterventions.Add(intervention) }, intervention.Id);
    }

    public InitiativeSession WithdrawIntervention(InterventionId interventionId)
    {
        EnsureActive();
        var index = SelectedInterventions.FindIndex(i => i.Id == interventionId);
        if (index < 0)
        {
            throw new InvalidOperationException("Unknown intervention.");
        }

        return this with { SelectedInterventions = SelectedInterventions.RemoveAt(index) };
    }

    /// <summary>Records a cross-reference link into a design workspace. Never scaffolds model content (issue #83).</summary>
    public InitiativeSession LinkDesignWorkspace(InterventionId interventionId, string reference)
    {
        EnsureActive();
        var index = SelectedInterventions.FindIndex(i => i.Id == interventionId);
        if (index < 0)
        {
            throw new InvalidOperationException("Unknown intervention.");
        }

        return this with { SelectedInterventions = SelectedInterventions.SetItem(index, SelectedInterventions[index].WithDesignWorkspaceReference(reference)) };
    }

    /// <summary>
    /// Records a gate evaluation. If a recommendation is provided, it is proposed as a new Prompted
    /// Question attributed to the Agent Participant — the gate stays strictly advisory either way.
    /// </summary>
    public InitiativeSession RecordGateEvaluation(
        GateEvaluation evaluation, string? recommendedQuestionText = null, InitiativeField? recommendedQuestionField = null)
    {
        EnsureActive();
        var session = this;
        QuestionId? recommendedQuestionId = null;
        if (!string.IsNullOrWhiteSpace(recommendedQuestionText) && recommendedQuestionField is not null)
        {
            (session, var questionId) = session.ProposeQuestion(recommendedQuestionText, AgentParticipantId, ParticipantRole.Agent, recommendedQuestionField.Value);
            recommendedQuestionId = questionId;
        }

        var recorded = evaluation with { RecommendedQuestionId = recommendedQuestionId };
        return recorded.Kind switch
        {
            GateKind.Discovery => session with { LatestDiscoveryGateEvaluation = recorded },
            GateKind.Shape => session with { LatestShapeGateEvaluation = recorded },
            _ => throw new ArgumentOutOfRangeException(nameof(evaluation)),
        };
    }

    /// <summary>
    /// Records the Facilitator's dismissal of a flagged gap as a <see cref="DismissedGateFinding"/>
    /// override. The dismissal is retained, never deleted, and never changes the evaluation itself —
    /// both gates stay strictly advisory. A dismissed finding is excluded from
    /// <see cref="FinalizeInitiative"/>'s "still open" check — see that method's own remarks.
    /// </summary>
    public (InitiativeSession Session, GateOverrideId GateOverrideId) DismissGateFinding(GateKind kind, GateCheck check, string? reason)
    {
        EnsureActive();
        var latest = kind == GateKind.Discovery ? LatestDiscoveryGateEvaluation : LatestShapeGateEvaluation;
        var finding = latest?.Results.FirstOrDefault(r => r.Check == check && !r.Passed)
            ?? throw new InvalidOperationException("Only a failed check in the latest gate evaluation can be dismissed.");

        if (GateOverrides.OfType<DismissedGateFinding>().Any(d => d.Kind == kind && d.Finding == finding))
        {
            throw new InvalidOperationException("This gate finding has already been dismissed.");
        }

        var dismissal = new DismissedGateFinding(GateOverrideId.New(), kind, finding, reason);
        return (this with { GateOverrides = GateOverrides.Add(dismissal) }, dismissal.Id);
    }

    public InitiativeStructuredFields BuildStructuredFields()
    {
        var accepted = Responses.OfType<AcceptedResponse>()
            .Join(Questions, r => r.QuestionId, q => q.Id, (r, q) => (r.Text, q.Field))
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
            [.. SelectedInterventions]);
    }

    /// <summary>
    /// Finalizes the Initiative, snapshotting its structured fields as markdown. Neither gate ever
    /// blocks this: if a gate's latest evaluation has failing checks that have <em>not</em> already
    /// been dismissed via <see cref="DismissGateFinding"/>, the Initiative is finalized as
    /// <see cref="WithOpenGateFindings"/> and the disagreement is recorded as a
    /// <see cref="FinalizedAgainstGate"/> override carrying only those still-open findings — a
    /// dismissed finding was already the Facilitator's recorded judgment call and must not also
    /// produce a second, contradictory override at finalize time.
    /// </summary>
    public InitiativeSession FinalizeInitiative(DateTimeOffset finalizedAt, string? reason)
    {
        if (Finalization is not null)
        {
            throw new InvalidOperationException("The Initiative is already finalized.");
        }

        var snapshot = BuildStructuredFields().ToMarkdown();
        var overrides = GateOverrides;
        var hasOpenFindings = false;

        foreach (var (kind, evaluation) in new[] { (GateKind.Discovery, LatestDiscoveryGateEvaluation), (GateKind.Shape, LatestShapeGateEvaluation) })
        {
            var dismissed = overrides.OfType<DismissedGateFinding>()
                .Where(d => d.Kind == kind)
                .Select(d => d.Finding)
                .ToHashSet();
            var stillOpen = evaluation?.Results.Where(r => !r.Passed && !dismissed.Contains(r)).ToList() ?? [];
            if (stillOpen.Count == 0) continue;
            hasOpenFindings = true;
            overrides = overrides.Add(new FinalizedAgainstGate(GateOverrideId.New(), kind, stillOpen, reason));
        }

        InitiativeFinalization finalization = hasOpenFindings ? new WithOpenGateFindings(snapshot, finalizedAt) : new Clean(snapshot, finalizedAt);
        return this with { GateOverrides = overrides, Finalization = finalization };
    }

    /// <summary>Reopens a finalized Initiative. Not irreversible in v1; gate overrides are retained.</summary>
    public InitiativeSession Reopen()
    {
        if (Finalization is null)
        {
            throw new InvalidOperationException("Only a finalized Initiative can be reopened.");
        }

        return this with { Finalization = null };
    }

    private void EnsureActive()
    {
        if (Finalization is not null)
        {
            throw new InvalidOperationException("The Initiative is finalized. Reopen it before making changes.");
        }
    }

    private static string ValidChangeRequest(string value) => string.IsNullOrWhiteSpace(value)
        ? throw new ArgumentException("The original change request is required.", nameof(value))
        : value.Trim();

    private static void RequireValidParticipants(ImmutableList<Participant> participants)
    {
        if (participants.Count(p => p.Role == ParticipantRole.Agent) != 1)
        {
            throw new ArgumentException("An Initiative must have exactly one Agent participant.", nameof(participants));
        }

        if (participants.Count(p => p.Role == ParticipantRole.Facilitator) > 1)
        {
            throw new ArgumentException("An Initiative can have at most one Facilitator.", nameof(participants));
        }

        if (participants.Count(p => p.Role == ParticipantRole.DomainExpert) > 1)
        {
            throw new ArgumentException("An Initiative can have at most one Domain Expert.", nameof(participants));
        }
    }
}
