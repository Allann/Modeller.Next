'use client';

import { use, useState } from 'react';
import {
  ALL_FIELDS,
  ALL_INTERVENTION_TYPES,
  CHECKS_BY_GATE,
  PHASE_OF_FIELD,
  buildStructuredFields,
  type GateCheckResultDto,
  type GateKind,
  type InitiativeField,
  type InterventionType,
} from '@/lib/initiativeTypes';
import { InitiativeApiError, initiativeApi } from '@/lib/initiativeApi';
import { useInitiativeSession } from '@/lib/useInitiativeSession';

export default function FacilitatorCockpitPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const { session, error, loading, refetch } = useInitiativeSession(id);
  const [actionError, setActionError] = useState<string | null>(null);

  async function run(action: () => Promise<unknown>) {
    setActionError(null);
    try {
      await action();
      await refetch();
    } catch (err) {
      setActionError(err instanceof InitiativeApiError ? `${err.body.code}: ${err.message}` : 'That action failed.');
    }
  }

  if (loading) return <main><p>Loading…</p></main>;
  if (error || !session) return <main><p className="form-error" role="alert">{error ?? 'Initiative not found.'}</p></main>;

  const facilitator = session.participants.find((p) => p.role === 'Facilitator');
  const structuredFields = buildStructuredFields(session);
  const respondUrl = typeof window !== 'undefined' ? `${window.location.origin}/initiative/${id}/respond` : '';

  return (
    <main className="cockpit">
      <p className="eyebrow">Facilitator cockpit</p>
      <h1>{session.originalChangeRequest}</h1>
      {session.finalization && (
        <p className="badge badge-finalization">
          Finalized ({session.finalization.status === 'Clean' ? 'clean' : 'with open gate findings'})
        </p>
      )}
      {respondUrl && (
        <p className="hero-note">
          Domain Expert link: <code>{respondUrl}</code>
        </p>
      )}
      {actionError && <p className="form-error" role="alert">{actionError}</p>}

      <PhaseProgress session={session} />

      <QuestionsSection session={session} facilitatorId={facilitator?.id} run={run} />

      <StructuredFieldsSection structuredFields={structuredFields} />

      <GateSection kind="Discovery" session={session} run={run} />

      <InterventionsSection id={id} session={session} run={run} />

      <GateSection kind="Shape" session={session} run={run} />

      <FinalizeSection session={session} run={run} />
    </main>
  );
}

function PhaseProgress({ session }: { session: import('@/lib/initiativeTypes').InitiativeSessionDto }) {
  const hasShapeActivity = session.selectedInterventions.length > 0 || session.latestShapeGateEvaluation !== null;
  const phase = session.finalization ? 'Design' : hasShapeActivity ? 'Shape' : session.latestDiscoveryGateEvaluation ? 'Frame' : 'Discover';
  return (
    <section aria-label="Phase progress">
      <div className="phase-progress">
        {(['Discover', 'Frame', 'Shape', 'Design'] as const).map((step) => (
          <span key={step} className={step === phase ? 'phase-step phase-step-active' : 'phase-step'}>
            {step}
          </span>
        ))}
      </div>
    </section>
  );
}

function QuestionsSection({
  session,
  facilitatorId,
  run,
}: {
  session: import('@/lib/initiativeTypes').InitiativeSessionDto;
  facilitatorId: string | undefined;
  run: (action: () => Promise<unknown>) => Promise<void>;
}) {
  const [field, setField] = useState<InitiativeField>('ProblemStatement');
  const [text, setText] = useState('');

  const proposed = session.questions.filter((q) => q.status === 'Proposed');
  const sent = session.questions.filter((q) => q.status === 'Sent');
  const pendingResponses = session.responses.filter((r) => r.status === 'Pending');

  return (
    <section aria-label="Questions">
      <h2>Discover &amp; Frame</h2>
      {facilitatorId && (
        <form
          className="inline-form"
          onSubmit={(event) => {
            event.preventDefault();
            void run(() => initiativeApi.proposeQuestion(session.id, facilitatorId, 'Facilitator', field, text || null)).then(() => setText(''));
          }}
        >
          <select value={field} onChange={(event) => setField(event.target.value as InitiativeField)}>
            {ALL_FIELDS.map((f) => (
              <option key={f} value={f}>
                {f} ({PHASE_OF_FIELD[f]})
              </option>
            ))}
          </select>
          <input placeholder="Question text (leave blank to ask AI)" value={text} onChange={(event) => setText(event.target.value)} />
          <button className="secondary-action" type="submit">
            Propose
          </button>
        </form>
      )}

      {proposed.length > 0 && (
        <ul className="item-list">
          {proposed.map((q) => (
            <li key={q.id}>
              <span className="badge">{q.field}</span> {q.text}
              <button className="link-action" onClick={() => void run(() => initiativeApi.sendQuestion(session.id, q.id))}>
                Send to Domain Expert
              </button>
              <button className="link-action" onClick={() => void run(() => initiativeApi.rejectQuestion(session.id, q.id))}>
                Reject
              </button>
            </li>
          ))}
        </ul>
      )}

      {sent.length > 0 && (
        <div>
          <p className="panel-kicker">Awaiting the Domain Expert</p>
          <ul className="item-list">
            {sent.map((q) => (
              <li key={q.id}>
                <span className="badge">{q.field}</span> {q.text}
              </li>
            ))}
          </ul>
        </div>
      )}

      {pendingResponses.length > 0 && (
        <div>
          <p className="panel-kicker">Responses to accept</p>
          <ul className="item-list">
            {pendingResponses.map((r) => (
              <li key={r.id}>
                {r.text}
                <button className="link-action" onClick={() => void run(() => initiativeApi.acceptResponse(session.id, r.id))}>
                  Accept
                </button>
              </li>
            ))}
          </ul>
        </div>
      )}
    </section>
  );
}

function StructuredFieldsSection({ structuredFields }: { structuredFields: Record<InitiativeField, string[]> }) {
  const populated = ALL_FIELDS.filter((f) => structuredFields[f].length > 0);
  if (populated.length === 0) return null;

  return (
    <section aria-label="Structured fields">
      <h2>Structured record</h2>
      {populated.map((f) => (
        <div key={f}>
          <p className="panel-kicker">{f}</p>
          <ul className="item-list">
            {structuredFields[f].map((text, index) => (
              <li key={index}>{text}</li>
            ))}
          </ul>
        </div>
      ))}
    </section>
  );
}

function GateSection({
  kind,
  session,
  run,
}: {
  kind: GateKind;
  session: import('@/lib/initiativeTypes').InitiativeSessionDto;
  run: (action: () => Promise<unknown>) => Promise<void>;
}) {
  const evaluation = kind === 'Discovery' ? session.latestDiscoveryGateEvaluation : session.latestShapeGateEvaluation;
  const [manualResults, setManualResults] = useState<Record<string, { passed: boolean; reason: string }>>({});

  const checks = CHECKS_BY_GATE[kind];

  function submitManual() {
    const results: GateCheckResultDto[] = checks.map((check) => ({
      check,
      passed: manualResults[check]?.passed ?? false,
      reason: manualResults[check]?.reason || 'Not stated.',
    }));
    void run(() => initiativeApi.recordGateEvaluation(session.id, kind, results));
  }

  return (
    <section aria-label={`${kind} Gate`}>
      <h2>{kind} Gate</h2>
      <p className="hero-note">Advisory only — never blocks proceeding.</p>
      <div className="inline-form">
        <button
          className="secondary-action"
          onClick={() => void run(() => initiativeApi.recordGateEvaluation(session.id, kind, null))}
        >
          Ask AI to evaluate
        </button>
      </div>

      {evaluation ? (
        <ul className="item-list">
          {evaluation.results.map((result) => (
            <li key={result.check}>
              <span className={result.passed ? 'badge badge-pass' : 'badge badge-fail'}>{result.passed ? 'Pass' : 'Flagged'}</span>{' '}
              {result.check} — {result.reason}
              {!result.passed && (
                <button
                  className="link-action"
                  onClick={() => void run(() => initiativeApi.dismissGateFinding(session.id, kind, result.check, 'Accepted for now.'))}
                >
                  Dismiss
                </button>
              )}
            </li>
          ))}
        </ul>
      ) : (
        <details>
          <summary>No AI configured? Evaluate manually</summary>
          <div className="manual-gate-form">
            {checks.map((check) => (
              <label key={check} className="manual-gate-row">
                <input
                  type="checkbox"
                  checked={manualResults[check]?.passed ?? false}
                  onChange={(event) =>
                    setManualResults((prev) => ({ ...prev, [check]: { ...prev[check], passed: event.target.checked, reason: prev[check]?.reason ?? '' } }))
                  }
                />
                {check}
                <input
                  placeholder="reason"
                  value={manualResults[check]?.reason ?? ''}
                  onChange={(event) =>
                    setManualResults((prev) => ({ ...prev, [check]: { ...prev[check], passed: prev[check]?.passed ?? false, reason: event.target.value } }))
                  }
                />
              </label>
            ))}
            <button className="secondary-action" onClick={submitManual}>
              Record manual evaluation
            </button>
          </div>
        </details>
      )}
    </section>
  );
}

function InterventionsSection({
  id,
  session,
  run,
}: {
  id: string;
  session: import('@/lib/initiativeTypes').InitiativeSessionDto;
  run: (action: () => Promise<unknown>) => Promise<void>;
}) {
  const [type, setType] = useState<InterventionType>('Process');
  const [description, setDescription] = useState('');
  const [rationale, setRationale] = useState('');
  const [suggestions, setSuggestions] = useState<{ type: InterventionType; description: string; rationale: string }[] | null>(null);
  const [suggestionsError, setSuggestionsError] = useState<string | null>(null);

  async function askForSuggestions() {
    setSuggestionsError(null);
    try {
      const response = await initiativeApi.getInterventionSuggestions(id);
      setSuggestions(response.suggestions);
    } catch (err) {
      setSuggestionsError(err instanceof InitiativeApiError ? err.message : 'No suggestions available.');
    }
  }

  return (
    <section aria-label="Shape: interventions">
      <h2>Shape: interventions</h2>
      <div className="inline-form">
        <button className="secondary-action" onClick={() => void askForSuggestions()}>
          Ask AI for candidate interventions
        </button>
      </div>
      {suggestionsError && <p className="hero-note">{suggestionsError}</p>}
      {suggestions && (
        <ul className="item-list">
          {suggestions.map((s, index) => (
            <li key={index}>
              <span className="badge">{s.type}</span> {s.description} — {s.rationale}
              <button
                className="link-action"
                onClick={() => {
                  setType(s.type);
                  setDescription(s.description);
                  setRationale(s.rationale);
                }}
              >
                Use this
              </button>
            </li>
          ))}
        </ul>
      )}

      <form
        className="inline-form"
        onSubmit={(event) => {
          event.preventDefault();
          void run(() => initiativeApi.selectIntervention(id, type, description, rationale)).then(() => {
            setDescription('');
            setRationale('');
          });
        }}
      >
        <select value={type} onChange={(event) => setType(event.target.value as InterventionType)}>
          {ALL_INTERVENTION_TYPES.map((t) => (
            <option key={t} value={t}>
              {t}
            </option>
          ))}
        </select>
        <input required placeholder="Description" value={description} onChange={(event) => setDescription(event.target.value)} />
        <input required placeholder="Rationale" value={rationale} onChange={(event) => setRationale(event.target.value)} />
        <button className="secondary-action" type="submit">
          Select
        </button>
      </form>

      {session.selectedInterventions.length > 0 && (
        <ul className="item-list">
          {session.selectedInterventions.map((intervention) => (
            <li key={intervention.id}>
              <span className="badge">{intervention.type}</span> {intervention.description} — {intervention.rationale}
              {intervention.designWorkspaceReference ? (
                <span className="badge badge-pass"> linked: {intervention.designWorkspaceReference}</span>
              ) : intervention.type === 'Technology' ? (
                <LinkDesignWorkspaceForm id={id} interventionId={intervention.id} run={run} />
              ) : null}
              <button className="link-action" onClick={() => void run(() => initiativeApi.withdrawIntervention(id, intervention.id))}>
                Withdraw
              </button>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

function LinkDesignWorkspaceForm({
  id,
  interventionId,
  run,
}: {
  id: string;
  interventionId: string;
  run: (action: () => Promise<unknown>) => Promise<void>;
}) {
  const [reference, setReference] = useState('');
  return (
    <form
      className="inline-form"
      onSubmit={(event) => {
        event.preventDefault();
        void run(() => initiativeApi.linkDesignWorkspace(id, interventionId, reference));
      }}
    >
      <input required placeholder="System Design reference" value={reference} onChange={(event) => setReference(event.target.value)} />
      <button className="secondary-action" type="submit">
        Link
      </button>
    </form>
  );
}

function FinalizeSection({
  session,
  run,
}: {
  session: import('@/lib/initiativeTypes').InitiativeSessionDto;
  run: (action: () => Promise<unknown>) => Promise<void>;
}) {
  const [reason, setReason] = useState('');

  if (session.finalization) {
    return (
      <section aria-label="Finalization">
        <h2>Finalized</h2>
        <pre>{session.finalization.markdownSnapshot}</pre>
        <button className="secondary-action" onClick={() => void run(() => initiativeApi.reopen(session.id))}>
          Reopen
        </button>
      </section>
    );
  }

  return (
    <section aria-label="Finalize">
      <h2>Finalize</h2>
      <form
        className="inline-form"
        onSubmit={(event) => {
          event.preventDefault();
          void run(() => initiativeApi.finalize(session.id, reason || null));
        }}
      >
        <input placeholder="Reason (optional, e.g. if a gate finding is still open)" value={reason} onChange={(event) => setReason(event.target.value)} />
        <button className="primary-action" type="submit">
          Finalize this Initiative
        </button>
      </form>
    </section>
  );
}
