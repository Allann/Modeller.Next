'use client';

import { use, useEffect, useState } from 'react';
import { buildStructuredFields } from '@/lib/initiativeTypes';
import { InitiativeApiError, initiativeApi } from '@/lib/initiativeApi';
import { useInitiativeSession } from '@/lib/useInitiativeSession';
import { PhaseProgress } from '@/components/initiative/PhaseProgress';
import { QuestionsSection } from '@/components/initiative/QuestionsSection';
import { StructuredFieldsSection } from '@/components/initiative/StructuredFieldsSection';
import { GateSection } from '@/components/initiative/GateSection';
import { InterventionsSection } from '@/components/initiative/InterventionsSection';
import { FinalizeSection } from '@/components/initiative/FinalizeSection';
import { CopyLinkButton } from '@/components/initiative/CopyLinkButton';
import { ConnectionStatus } from '@/components/initiative/ConnectionStatus';

export default function FacilitatorCockpitPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const { session, error, loading, connectionStatus, refetch } = useInitiativeSession(id);
  const [actionError, setActionError] = useState<string | null>(null);
  const [agentStatus, setAgentStatus] = useState<{ available: boolean; model: string | null; requiresApiKey: boolean; freeModel: string | null }>({ available: false, model: null, requiresApiKey: true, freeModel: null });
  const [agentApiKey, setAgentApiKey] = useState('');

  useEffect(() => {
    void initiativeApi.getAgentStatus().then(setAgentStatus).catch(() => setAgentStatus({ available: false, model: null, requiresApiKey: true, freeModel: null }));
  }, []);

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
  const aiRequiresKey = agentStatus.available && agentStatus.requiresApiKey && agentApiKey.length === 0;

  return (
    <main className="cockpit">
      <p className="eyebrow">Facilitator cockpit <ConnectionStatus status={connectionStatus} /></p>
      <h1>{session.originalChangeRequest}</h1>
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
      {agentStatus.available ? (
        <div className="inline-form" role="status">
          <label>
            Your Vercel AI Gateway key{agentStatus.requiresApiKey ? '' : ' (optional)'}
            <input
              type="password"
              autoComplete="off"
              spellCheck={false}
              value={agentApiKey}
              onChange={(event) => setAgentApiKey(event.target.value)}
              placeholder="AI Gateway API key"
            />
          </label>
          <span className="hero-note">
            {agentStatus.freeModel
              ? `Without a key, AI uses the free ${agentStatus.freeModel} model. With your key, it uses ${agentStatus.model}.`
              : `A key is required and uses ${agentStatus.model}.`}
            {' '}The key is used for this page only and is not saved.
            {' '}<a href="https://vercel.com/ai-gateway" target="_blank" rel="noreferrer">Get a Vercel AI Gateway key</a>.
          </span>
        </div>
      ) : (
        <p className="hero-note" role="status">Agent Advisor unavailable — manual facilitation remains available.</p>
      )}

      <PhaseProgress session={session} />
      <QuestionsSection session={session} facilitatorId={facilitator?.id} run={run} aiAvailable={canUseAi} agentApiKey={agentApiKey} />
      <StructuredFieldsSection structuredFields={structuredFields} />
      <GateSection kind="Discovery" session={session} run={run} aiAvailable={canUseAi} aiRequiresKey={aiRequiresKey} agentApiKey={agentApiKey} />
      <InterventionsSection id={id} session={session} run={run} aiAvailable={canUseAi} aiRequiresKey={aiRequiresKey} agentApiKey={agentApiKey} />
      <GateSection kind="Shape" session={session} run={run} aiAvailable={canUseAi} aiRequiresKey={aiRequiresKey} agentApiKey={agentApiKey} />
      <FinalizeSection session={session} run={run} />
    </main>
  );
}
