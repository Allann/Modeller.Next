'use client';

import { use, useState } from 'react';
import { InitiativeApiError, initiativeApi } from '@/lib/initiativeApi';
import { useInitiativeSession } from '@/lib/useInitiativeSession';

/** The Domain Expert's focused view — deliberately not the whole cockpit surface (issue #91):
 * just the current sent question and a place to answer it. The API returns the full session to
 * everyone (v1 dropped role-scoped visibility, issue #88), so the restriction is enforced here,
 * in what this page chooses to render, not on the wire. */
export default function DomainExpertRespondPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const { session, error, loading, refetch } = useInitiativeSession(id);
  const [text, setText] = useState('');
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  if (loading) return <main><p>Loading…</p></main>;
  if (error || !session) return <main><p className="form-error" role="alert">{error ?? 'Initiative not found.'}</p></main>;

  if (session.finalization) {
    return (
      <main>
        <p className="eyebrow">Domain Expert</p>
        <h1>This Initiative is finalized</h1>
        <p>Thank you — there&rsquo;s nothing further needed from you right now.</p>
      </main>
    );
  }

  const answeredQuestionIds = new Set(session.responses.map((r) => r.questionId));
  const currentQuestion = session.questions.find((q) => q.status === 'Sent' && !answeredQuestionIds.has(q.id));

  async function handleSubmit(event: React.FormEvent) {
    event.preventDefault();
    if (!currentQuestion) return;
    setSubmitting(true);
    setSubmitError(null);
    try {
      await initiativeApi.submitResponse(id, currentQuestion.id, text);
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
      <p className="eyebrow">Domain Expert</p>
      <h1>{session.originalChangeRequest}</h1>

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
    </main>
  );
}
