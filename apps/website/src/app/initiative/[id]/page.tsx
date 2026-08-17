'use client';

import { use, useState } from 'react';
import { buildStructuredFields } from '@/lib/initiativeTypes';
import { InitiativeApiError } from '@/lib/initiativeApi';
import { useInitiativeSession } from '@/lib/useInitiativeSession';
import { PhaseProgress } from '@/components/initiative/PhaseProgress';
import { QuestionsSection } from '@/components/initiative/QuestionsSection';
import { StructuredFieldsSection } from '@/components/initiative/StructuredFieldsSection';
import { GateSection } from '@/components/initiative/GateSection';
import { InterventionsSection } from '@/components/initiative/InterventionsSection';
import { FinalizeSection } from '@/components/initiative/FinalizeSection';
import { CopyLinkButton } from '@/components/initiative/CopyLinkButton';

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
          Domain Expert link: <a href={respondUrl}>{respondUrl}</a> <CopyLinkButton url={respondUrl} />
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
