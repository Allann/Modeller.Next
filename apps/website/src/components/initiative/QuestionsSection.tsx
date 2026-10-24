'use client';

import { useState } from 'react';
import { ALL_FIELDS, INITIATIVE_FIELD_LABELS, PHASE_OF_FIELD, type InitiativeField, type InitiativeSessionDto } from '@/lib/initiativeTypes';
import { initiativeApi } from '@/lib/initiativeApi';
import { INITIATIVE_FIELD_GUIDANCE } from '@/lib/initiativeFieldGuidance';
import type { RunAction } from './types';

export function QuestionsSection({
  session,
  facilitatorId,
  credential,
  run,
  aiAvailable,
  agentApiKey,
}: {
  session: InitiativeSessionDto;
  facilitatorId: string | undefined;
  credential: string;
  run: RunAction;
  aiAvailable: boolean;
  agentApiKey: string;
}) {
  const [field, setField] = useState<InitiativeField>('ProblemStatement');
  const [text, setText] = useState('');

  const proposed = session.questions.filter((q) => q.status === 'Proposed');
  const respondedQuestionIds = new Set(session.responses.map((response) => response.questionId));
  const sent = session.questions.filter((q) => q.status === 'Sent' && !respondedQuestionIds.has(q.id));
  const pendingResponses = session.responses.filter((r) => r.status === 'Pending');
  const guidance = INITIATIVE_FIELD_GUIDANCE[field];

  return (
    <section aria-label="Questions">
      <h2>Discover &amp; Frame</h2>
      {facilitatorId && (
        <>
        <form
          className="inline-form"
          onSubmit={(event) => {
            event.preventDefault();
            void run(() => initiativeApi.proposeQuestion(session.id, credential, field, text || null, text ? undefined : agentApiKey)).then(() => setText(''));
          }}
        >
          <select
            aria-label="Field to ask about"
            aria-describedby="field-guidance"
            value={field}
            onChange={(event) => setField(event.target.value as InitiativeField)}
          >
            {ALL_FIELDS.map((f) => (
              <option key={f} value={f}>
                {INITIATIVE_FIELD_LABELS[f]} ({PHASE_OF_FIELD[f]})
              </option>
            ))}
          </select>
          <input
            placeholder={aiAvailable ? 'Question text (leave blank to ask AI)' : 'Question text'}
            required={!aiAvailable}
            value={text}
            onChange={(event) => setText(event.target.value)}
          />
          <button className="secondary-action" type="submit">
            Propose
          </button>
        </form>
        <aside id="field-guidance" className="field-guidance" aria-live="polite">
          <p className="field-guidance-title">
            {INITIATIVE_FIELD_LABELS[field]} <span className="field-guidance-phase">{PHASE_OF_FIELD[field]}</span>
          </p>
          <p>{guidance.captures}</p>
          <p className="field-guidance-example">
            Example question: <q>{guidance.exampleQuestion}</q>{' '}
            <button className="link-action" type="button" onClick={() => setText(guidance.exampleQuestion)}>
              Use this question
            </button>
          </p>
        </aside>
        </>
      )}

      {proposed.length > 0 && (
        <ul className="item-list">
          {proposed.map((q) => (
            <li key={q.id}>
              <span className="badge">{INITIATIVE_FIELD_LABELS[q.field]}</span> {q.text}
              <button className="link-action" onClick={() => void run(() => initiativeApi.sendQuestion(session.id, credential, q.id))}>
                Send to Domain Expert
              </button>
              <button className="link-action" onClick={() => void run(() => initiativeApi.rejectQuestion(session.id, credential, q.id))}>
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
                <button className="link-action" onClick={() => void run(() => initiativeApi.acceptResponse(session.id, credential, r.id))}>
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
