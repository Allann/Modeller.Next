'use client';

import { use, useEffect, useState } from 'react';
import { useSearchParams } from 'next/navigation';
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
import { loadDomainExpertCredential, loadFacilitatorCredential, saveInitiativeCredentials } from '@/lib/initiativeCredentials';

export default function FacilitatorCockpitPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const searchParams = useSearchParams();
  // The Facilitator's sharable link carries its own credential as a query parameter (issue #146) —
  // that link, not this browser's storage, is the actual bearer of authority. sessionStorage is
  // only consulted as a same-tab fallback (e.g. a reload after the query string was trimmed).
  const [credential, setCredential] = useState('');
  const [domainExpertCredential, setDomainExpertCredential] = useState('');

  const credentialFromQuery = searchParams.get('credential');
  useEffect(() => {
    // Deferred via setTimeout, matching loadAgentApiKey's own use of this pattern just below —
    // sessionStorage is only available client-side, so reading it inside the effect body directly
    // (rather than a scheduled callback) would call setState synchronously within the effect.
    const loadTimer = window.setTimeout(() => {
      const resolved = credentialFromQuery || loadFacilitatorCredential(id);
      setCredential(resolved);
      const domainExpert = loadDomainExpertCredential(id);
      setDomainExpertCredential(domainExpert);
      if (credentialFromQuery && domainExpert) saveInitiativeCredentials(id, credentialFromQuery, domainExpert);
    }, 0);
    return () => window.clearTimeout(loadTimer);
  }, [id, credentialFromQuery]);

  const { session, error, loading, connectionStatus, refetch } = useInitiativeSession(id, credential);
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

  if (!credential) return <main><p className="form-error" role="alert">Missing session credential — use the Facilitator link this Initiative gave you.</p></main>;
  if (loading) return <main><p>Loading…</p></main>;
  if (error || !session) return <main><p className="form-error" role="alert">{error ?? 'Initiative not found.'}</p></main>;

  const facilitator = session.participants.find((p) => p.role === 'Facilitator');
  const structuredFields = buildStructuredFields(session);
  const respondUrl = typeof window !== 'undefined' && domainExpertCredential
    ? `${window.location.origin}/initiative/${id}/respond?credential=${encodeURIComponent(domainExpertCredential)}`
    : '';
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
      {respondUrl ? (
        <p className="hero-note">
          Domain Expert link: <a href={respondUrl}>{respondUrl}</a> <CopyLinkButton url={respondUrl} />
        </p>
      ) : (
        <p className="hero-note">
          The Domain Expert link is only available in the browser tab where you started this
          Initiative. Return to that tab to copy it.
        </p>
      )}
      <p className="hero-note">
        New to Initiatives?{' '}
        <a href="https://modeller.wiki/docs/guides/building-variation-initiative" target="_blank" rel="noreferrer">
          Follow the worked building-variation example
        </a>
        .
      </p>
      {actionError && <p className="form-error" role="alert">{actionError}</p>}
      <PhaseProgress activeStep={displayedStep} onSelect={setActiveStep} />
      {displayedStep === 'DiscoverFrame' && (
        <div className="cockpit-step-panel">
          <div className="cockpit-input-column">
            <QuestionsSection session={session} facilitatorId={facilitator?.id} credential={credential} run={run} aiAvailable={canUseAi} agentApiKey={agentApiKey} />
            <GateSection kind="Discovery" session={session} credential={credential} run={run} aiAvailable={canUseAi} agentApiKey={agentApiKey} />
          </div>
          <aside className="cockpit-results-column" aria-label="Accepted Initiative record">
            <StructuredFieldsSection structuredFields={structuredFields} />
          </aside>
        </div>
      )}
      {displayedStep === 'Shape' && (
        <div className="cockpit-step-panel">
          <div className="cockpit-input-column">
            <InterventionsSection id={id} credential={credential} session={session} run={run} aiAvailable={canUseAi} agentApiKey={agentApiKey} />
            <GateSection kind="Shape" session={session} credential={credential} run={run} aiAvailable={canUseAi} agentApiKey={agentApiKey} />
          </div>
          <aside className="cockpit-results-column" aria-label="Accepted Initiative record">
            <StructuredFieldsSection structuredFields={structuredFields} />
          </aside>
        </div>
      )}
      {displayedStep === 'Finalize' && (
        <div className="cockpit-step-panel">
          <div className="cockpit-input-column">
            <FinalizeSection session={session} credential={credential} run={run} />
          </div>
          <aside className="cockpit-results-column" aria-label="Accepted Initiative record">
            <StructuredFieldsSection structuredFields={structuredFields} />
          </aside>
        </div>
      )}
    </main>
  );
}
