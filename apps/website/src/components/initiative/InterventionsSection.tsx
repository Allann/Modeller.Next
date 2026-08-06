'use client';

import { useState } from 'react';
import { ALL_INTERVENTION_TYPES, type InitiativeSessionDto, type InterventionType } from '@/lib/initiativeTypes';
import { InitiativeApiError, initiativeApi } from '@/lib/initiativeApi';
import type { RunAction } from './types';

export function InterventionsSection({ id, session, run }: { id: string; session: InitiativeSessionDto; run: RunAction }) {
  const [type, setType] = useState<InterventionType>('Process');
  const [description, setDescription] = useState('');
  const [rationale, setRationale] = useState('');
  const [continuesToDesignWorkspace, setContinuesToDesignWorkspace] = useState(false);
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
                  setContinuesToDesignWorkspace(false);
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
          void run(() =>
            initiativeApi.selectIntervention(id, type, description, rationale, type === 'Technology' && continuesToDesignWorkspace),
          ).then(() => {
            setDescription('');
            setRationale('');
            setContinuesToDesignWorkspace(false);
          });
        }}
      >
        <select
          value={type}
          onChange={(event) => {
            setType(event.target.value as InterventionType);
            setContinuesToDesignWorkspace(false);
          }}
        >
          {ALL_INTERVENTION_TYPES.map((t) => (
            <option key={t} value={t}>
              {t}
            </option>
          ))}
        </select>
        <input required placeholder="Description" value={description} onChange={(event) => setDescription(event.target.value)} />
        <input required placeholder="Rationale" value={rationale} onChange={(event) => setRationale(event.target.value)} />
        {type === 'Technology' && (
          <label className="manual-gate-row">
            <input
              type="checkbox"
              checked={continuesToDesignWorkspace}
              onChange={(event) => setContinuesToDesignWorkspace(event.target.checked)}
            />
            Continue into System Design
          </label>
        )}
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
              ) : intervention.continuesToDesignWorkspace ? (
                <>
                  <span className="badge"> queued for System Design</span>
                  <LinkDesignWorkspaceForm id={id} interventionId={intervention.id} run={run} />
                </>
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

function LinkDesignWorkspaceForm({ id, interventionId, run }: { id: string; interventionId: string; run: RunAction }) {
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
