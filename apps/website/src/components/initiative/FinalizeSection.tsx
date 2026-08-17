'use client';

import { useState } from 'react';
import type { InitiativeSessionDto } from '@/lib/initiativeTypes';
import { initiativeApi } from '@/lib/initiativeApi';
import type { RunAction } from './types';
import { InitiativeDocument } from './InitiativeDocument';

export function FinalizeSection({ session, run }: { session: InitiativeSessionDto; run: RunAction }) {
  const [reason, setReason] = useState('');

  if (session.finalization) {
    const archiveExpiresAt = new Date(
      new Date(session.finalization.finalizedAt).getTime() + 7 * 24 * 60 * 60 * 1000,
    );

    return (
      <section aria-label="Finalization">
        <h2>Archived</h2>
        <p>
          This Initiative is available until {archiveExpiresAt.toLocaleString()}. Use the{' '}
          <a href={`/initiative/${session.id}`}>Facilitator link</a> to return here and reopen it during this period.
        </p>
        <InitiativeDocument initiativeId={session.id} markdown={session.finalization.markdownSnapshot} />
        <button className="secondary-action" onClick={() => void run(() => initiativeApi.reopen(session.id))}>
          Reopen archived Initiative
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
