'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { InitiativeApiError, initiativeApi } from '@/lib/initiativeApi';
import { capture } from '@/lib/productAnalytics';
import { saveAgentApiKey } from '@/lib/agentApiKey';
import { saveInitiativeCredentials } from '@/lib/initiativeCredentials';
import type { AgentAdvisorStatusResponse } from '@/lib/initiativeTypes';

export default function HomePage() {
  const router = useRouter();
  const [originalChangeRequest, setOriginalChangeRequest] = useState('');
  const [facilitatorName, setFacilitatorName] = useState('');
  const [domainExpertName, setDomainExpertName] = useState('');
  const [agentApiKey, setAgentApiKey] = useState('');
  const [agentStatus, setAgentStatus] = useState<AgentAdvisorStatusResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    void initiativeApi.getAgentStatus().then(setAgentStatus).catch(() => setAgentStatus(null));
  }, []);

  async function handleSubmit(event: React.FormEvent) {
    event.preventDefault();
    setSubmitting(true);
    setError(null);
    try {
      const { session, credentials } = await initiativeApi.create(originalChangeRequest, facilitatorName, domainExpertName);
      saveAgentApiKey(session.id, agentApiKey.trim());
      saveInitiativeCredentials(session.id, credentials.facilitator, credentials.domainExpert);
      capture('initiative_created');
      capture('meaningful_use_started');
      router.push(`/initiative/${session.id}?credential=${encodeURIComponent(credentials.facilitator)}`);
    } catch (err) {
      setError(err instanceof InitiativeApiError ? err.message : 'Could not start this Initiative. Is the Modeller API running?');
      setSubmitting(false);
    }
  }

  return (
    <main>
      <p className="eyebrow">Start with what needs to change</p>
      <h1>&ldquo;Build us a new system.&rdquo;</h1>
      <p>
        That is a proposed answer, not the problem. Capture the original request here, then work
        through Discover, Frame, and Shape with a Domain Expert before deciding whether the right
        response is a technology intervention at all. Explore a working example first?{' '}
        <Link href="/examples">See the examples</Link>.
      </p>

      <div className="initiative-start-layout">
        <form className="initiative-start-form" onSubmit={handleSubmit}>
        <label>
          Original change request
          <textarea
            required
            rows={3}
            placeholder="e.g. Build us a new approval system"
            value={originalChangeRequest}
            onChange={(event) => setOriginalChangeRequest(event.target.value)}
          />
        </label>
        <div className="initiative-start-form-row">
          <label>
            Facilitator name
            <input required value={facilitatorName} onChange={(event) => setFacilitatorName(event.target.value)} />
          </label>
          <label>
            Domain Expert name
            <input required value={domainExpertName} onChange={(event) => setDomainExpertName(event.target.value)} />
          </label>
        </div>
        {agentStatus?.available ? (
          <label>
            Vercel AI Gateway key {agentStatus.requiresApiKey ? '(optional — required for AI)' : '(optional)'}
            <input
              type="password"
              autoComplete="off"
              spellCheck={false}
              placeholder="Leave blank to facilitate manually"
              value={agentApiKey}
              onChange={(event) => setAgentApiKey(event.target.value)}
            />
            <span className="hero-note">
              Kept in this browser tab only. It is not saved with the Initiative.{' '}
              <a href="https://vercel.com/ai-gateway" target="_blank" rel="noreferrer">Get a key</a>.
            </span>
          </label>
        ) : null}
        {error && <p className="form-error" role="alert">{error}</p>}
        <button className="primary-action" type="submit" disabled={submitting}>
          {submitting ? 'Starting…' : 'Start the Initiative'}
        </button>
        <p className="hero-note">No account required. You&rsquo;ll get a shareable link for the Domain Expert.</p>
        </form>

        <section className="initiative-steps" aria-labelledby="initiative-steps-title">
          <h2 id="initiative-steps-title">How it works</h2>
          <ol>
            <li>Start the Initiative and send the private response link to the Domain Expert.</li>
            <li>Ask questions, then accept the answers that describe the problem and desired outcomes.</li>
            <li>Shape possible interventions, review the advisory gates, and finalize the Initiative.</li>
          </ol>
        </section>
      </div>
    </main>
  );
}
