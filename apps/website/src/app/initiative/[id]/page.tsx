'use client';

import { use, useEffect, useState } from 'react';
import { buildStructuredFields } from '@/lib/initiativeTypes';
import { InitiativeApiError, initiativeApi } from '@/lib/initiativeApi';
import { useInitiativeSession } from '@/lib/useInitiativeSession';
import { PhaseProgress, type CockpitStep } from '@/components/initiative/PhaseProgress';
import { QuestionsSection } from '@/components/initiative/QuestionsSection';
import { StructuredFieldsSection } from '@/components/initiative/StructuredFieldsSection';
import { GateSection } from '@/components/initiative/GateSection';
import { InterventionsSection } from '@/components/initiative/InterventionsSection';
import { FinalizeSection } from '@/components/initiative/FinalizeSection';
import { CopyLinkButton } from '@/components/initiative/CopyLinkButton';
import { ConnectionStatus } from '@/components/initiative/ConnectionStatus';
import { loadAgentApiKey } from '@/lib/agentApiKey';

export default function FacilitatorCockpitPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const { session, error, loading, connectionStatus, refetch } = useInitiativeSession(id);
  const [actionError, setActionError] = useState<string | null>(null);
  const [agentStatus, setAgentStatus] = useState<{ available: boolean; model: string | null; requiresApiKey: boolean; freeModel: string | null }>({ available: false, model: null, requiresApiKey: true, freeModel: null });
  const [agentApiKey, setAgentApiKey] = useState('');
  const [activeStep, setActiveStep] = useState<CockpitStep>('DiscoverFrame');

  useEffect(() => {
    const loadKeyTimer = window.setTimeout(() => setAgentApiKey(loadAgentApiKey(id)), 0);
    void initiativeApi.getAgentStatus().then(setAgentStatus).catch(() => setAgentStatus({ available: false, model: null, requiresApiKey: true, freeModel: null }));
    return () => window.clearTimeout(loadKeyTimer);
  }, [id]);

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
  const canUseAi = agentStatus.available && (!agentStatus.requiresApiKey || agentApiKey.length > 0);
  const displayedStep = session.finalization ? 'Finalize' : activeStep;

  return (
    <main className="cockpit">
      <p className="eyebrow">Facilitator cockpit <ConnectionStatus status={connectionStatus} /></p>
      <h1 className="initiative-session-title">{session.originalChangeRequest}</h1>
      {session.finalization && (
        <p className="badge badge-finalization">
          Finalized ({session.finalization.status === 'Clean' ? 'clean' : 'with open gate findings'})
        </p>
      )}
      {respondUrl && (
        <>
          <p className="hero-note">
            Domain Expert link: <a href={respondUrl}>{respondUrl}</a> <CopyLinkButton url={respondUrl} />
          </p>
          <p className="hero-note">
            New to Initiatives?{' '}
            <a href="https://modeller.wiki/docs/guides/building-variation-initiative" target="_blank" rel="noreferrer">
              Follow the worked building-variation example
            </a>
            .
          </p>
        </>
      )}
      {actionError && <p className="form-error" role="alert">{actionError}</p>}
      <PhaseProgress activeStep={displayedStep} onSelect={setActiveStep} />
      {displayedStep === 'DiscoverFrame' && (
        <div className="cockpit-step-panel">
          <div className="cockpit-input-column">
            <QuestionsSection session={session} facilitatorId={facilitator?.id} run={run} aiAvailable={canUseAi} agentApiKey={agentApiKey} />
            <GateSection kind="Discovery" session={session} run={run} aiAvailable={canUseAi} agentApiKey={agentApiKey} />
          </div>
          <aside className="cockpit-results-column" aria-label="Accepted Initiative record">
            <StructuredFieldsSection structuredFields={structuredFields} />
          </aside>
        </div>
      )}
      {displayedStep === 'Shape' && (
        <div className="cockpit-step-panel">
          <div className="cockpit-input-column">
            <InterventionsSection id={id} session={session} run={run} aiAvailable={canUseAi} agentApiKey={agentApiKey} />
            <GateSection kind="Shape" session={session} run={run} aiAvailable={canUseAi} agentApiKey={agentApiKey} />
          </div>
          <aside className="cockpit-results-column" aria-label="Accepted Initiative record">
            <StructuredFieldsSection structuredFields={structuredFields} />
          </aside>
        </div>
      )}
      {displayedStep === 'Finalize' && (
        <div className="cockpit-step-panel">
          <div className="cockpit-input-column">
            <FinalizeSection session={session} run={run} />
          </div>
          <aside className="cockpit-results-column" aria-label="Accepted Initiative record">
            <StructuredFieldsSection structuredFields={structuredFields} />
          </aside>
        </div>
      )}
    </main>
  );
}
