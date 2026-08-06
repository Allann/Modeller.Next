'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { InitiativeApiError, initiativeApi } from '@/lib/initiativeApi';

export default function HomePage() {
  const router = useRouter();
  const [originalChangeRequest, setOriginalChangeRequest] = useState('');
  const [facilitatorName, setFacilitatorName] = useState('');
  const [domainExpertName, setDomainExpertName] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(event: React.FormEvent) {
    event.preventDefault();
    setSubmitting(true);
    setError(null);
    try {
      const session = await initiativeApi.create(originalChangeRequest, facilitatorName, domainExpertName);
      router.push(`/initiative/${session.id}`);
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
        <Link href="/playground">See the playground</Link>.
      </p>

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
        {error && <p className="form-error" role="alert">{error}</p>}
        <button className="primary-action" type="submit" disabled={submitting}>
          {submitting ? 'Starting…' : 'Start the Initiative'}
        </button>
        <p className="hero-note">No account required. You&rsquo;ll get a shareable link for the Domain Expert.</p>
      </form>
    </main>
  );
}
