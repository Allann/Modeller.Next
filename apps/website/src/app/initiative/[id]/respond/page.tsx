'use client';

import { use, useState } from 'react';
import { useSearchParams } from 'next/navigation';
import { InitiativeApiError, initiativeApi } from '@/lib/initiativeApi';
import { useInitiativeSession } from '@/lib/useInitiativeSession';
import { ConnectionStatus } from '@/components/initiative/ConnectionStatus';
import { InitiativeDocument } from '@/components/initiative/InitiativeDocument';
import { buildStructuredFields, INITIATIVE_FIELD_LABELS } from '@/lib/initiativeTypes';

/** The Domain Expert's focused view — deliberately not the whole cockpit surface (issue #91):
 * just the current sent question and a place to answer it. Enforced on the wire, not just in what
 * this page renders — this page's own URL carries the Domain Expert's role-scoped credential
 * (issue #146) as a query parameter, and the server derives the role-scoped projection
 * (Modeller.Api.Initiative.InitiativeSessionMapper.ToDomainExpertDto) from that credential, never
 * from a role name this page could claim — hiding proposed-but-unsent questions, gate
 * evaluations/overrides, and Shape's intervention curation regardless of what the page asks for. */
export default function DomainExpertRespondPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const credential = useSearchParams().get('credential') ?? '';
  const { session, error, loading, connectionStatus, refetch } = useInitiativeSession(id, credential);
  const [text, setText] = useState('');
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  if (!credential) return <main><p className="form-error" role="alert">Missing session credential — use the link the Facilitator shared with you.</p></main>;
  if (loading) return <main><p>Loading…</p></main>;
  if (error || !session) return <main><p className="form-error" role="alert">{error ?? 'Initiative not found.'}</p></main>;

  if (session.finalization) {
    return (
      <main>
        <p className="eyebrow">Domain Expert <ConnectionStatus status={connectionStatus} /></p>
        <h1>This Initiative is finalized</h1>
        <p>Thank you. The final Initiative document is available below.</p>
        <InitiativeDocument initiativeId={session.id} markdown={session.finalization.markdownSnapshot} />
      </main>
    );
  }

  const answeredQuestionIds = new Set(session.responses.map((r) => r.questionId));
  const currentQuestion = session.questions.find((q) => q.status === 'Sent' && !answeredQuestionIds.has(q.id));
  const acceptedFields = buildStructuredFields(session);
  const acceptedEntries = Object.entries(acceptedFields).filter(([, values]) => values.length > 0);

  async function handleSubmit(event: React.FormEvent) {
    event.preventDefault();
    if (!currentQuestion) return;
    setSubmitting(true);
    setSubmitError(null);
    try {
      await initiativeApi.submitResponse(id, credential, currentQuestion.id, text);
      setText('');
      await refetch();
    } catch (err) {
      setSubmitError(err instanceof InitiativeApiError ? err.message : 'Could not submit your response.');
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <main>
      <p className="eyebrow">Domain Expert <ConnectionStatus status={connectionStatus} /></p>
      <h1 className="initiative-session-title">{session.originalChangeRequest}</h1>

      <div className="domain-expert-workspace">
        <div>
          {currentQuestion ? (
            <form className="respond-form" onSubmit={handleSubmit}>
          <p className="panel-kicker">Current question</p>
          <p className="respond-question">{currentQuestion.text}</p>
          <textarea
            required
            rows={5}
            placeholder="Your answer…"
            value={text}
            onChange={(event) => setText(event.target.value)}
          />
          {submitError && <p className="form-error" role="alert">{submitError}</p>}
          <button className="primary-action" type="submit" disabled={submitting}>
            {submitting ? 'Submitting…' : 'Submit response'}
          </button>
            </form>
          ) : (
            <p>Waiting for the next question from the Facilitator…</p>
          )}
        </div>

        <section className="accepted-summary" aria-label="Accepted answers">
        <h2>Accepted so far</h2>
        {acceptedEntries.length > 0 ? (
          <ul className="item-list">
            {acceptedEntries.flatMap(([field, values]) =>
              values.map((value, index) => (
                <li key={`${field}-${index}`}>
                  <span className="badge">{INITIATIVE_FIELD_LABELS[field as keyof typeof INITIATIVE_FIELD_LABELS]}</span>
                  {value}
                </li>
              )),
            )}
          </ul>
        ) : (
          <p className="hero-note">Nothing has been accepted yet. Accepted answers will appear here as the Initiative develops.</p>
        )}
        </section>
      </div>
    </main>
  );
}
