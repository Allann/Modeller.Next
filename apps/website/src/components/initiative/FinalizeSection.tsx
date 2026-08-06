'use client';

import { useState } from 'react';
import type { InitiativeSessionDto } from '@/lib/initiativeTypes';
import { initiativeApi } from '@/lib/initiativeApi';
import type { RunAction } from './types';

export function FinalizeSection({ session, run }: { session: InitiativeSessionDto; run: RunAction }) {
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
