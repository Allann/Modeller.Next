'use client';

import { useState } from 'react';
import { ALL_FIELDS, INITIATIVE_FIELD_LABELS, PHASE_OF_FIELD, type InitiativeField, type InitiativeSessionDto } from '@/lib/initiativeTypes';
import { initiativeApi } from '@/lib/initiativeApi';
import type { RunAction } from './types';

export function QuestionsSection({
  session,
  facilitatorId,
  run,
}: {
  session: InitiativeSessionDto;
  facilitatorId: string | undefined;
  run: RunAction;
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
                {INITIATIVE_FIELD_LABELS[f]} ({PHASE_OF_FIELD[f]})
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
              <span className="badge">{INITIATIVE_FIELD_LABELS[q.field]}</span> {q.text}
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
                <span className="badge">{INITIATIVE_FIELD_LABELS[q.field]}</span> {q.text}
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
