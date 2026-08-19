import { AdminMenu } from '@/components/AdminMenu';
import { getRedisInventory } from '@/lib/redisInventory';

export const dynamic = 'force-dynamic';

function duration(seconds: number): string {
  if (seconds === -1) return 'No expiry';
  const days = Math.floor(seconds / 86_400);
  const hours = Math.floor((seconds % 86_400) / 3_600);
  const minutes = Math.floor((seconds % 3_600) / 60);
  return [days ? `${days}d` : '', hours ? `${hours}h` : '', minutes || (!days && !hours) ? `${minutes}m` : ''].filter(Boolean).join(' ');
}

export default async function RedisPage() {
  const inventory = await getRedisInventory();
  return <main>
    <header>
      <div><p className="eyebrow">Private storage inventory</p><h1>Redis initiatives</h1></div>
    </header>
    <AdminMenu />
    <p className="subtle">Operational summary of retained Initiative sessions.</p>
    <section className="cards inventory-cards">
      <article><span>Stored initiatives</span><strong>{inventory.items.length}</strong></article>
      <article><span>Active</span><strong>{inventory.items.filter((item) => item.storage === 'Active').length}</strong></article>
      <article><span>Archived</span><strong>{inventory.items.filter((item) => item.storage === 'Archive').length}</strong></article>
    </section>
    <section className="table-section">
      <div className="section-heading"><h2>Items</h2><span>Read at {new Date(inventory.generatedAt).toLocaleString('en-AU', { timeZone: 'Australia/Brisbane' })} AEST</span></div>
      {inventory.truncated ? <p className="warning">Only the first 1,000 matching keys are shown.</p> : null}
      <div className="table-scroll"><table className="inventory-table">
        <thead><tr><th>Initiative</th><th>Original change request</th><th>Storage</th><th>Phase</th><th>Participants</th><th>Questions</th><th>Responses</th><th>Interventions</th><th>TTL</th><th>Expires</th></tr></thead>
        <tbody>{inventory.items.length ? inventory.items.map((item) => <tr key={item.key}>
          <th><code>{item.initiativeId}</code></th><td className="change-request">{item.originalChangeRequest}</td><td>{item.storage}</td><td>{item.phase}</td><td>{item.participants}</td><td>{item.questions}</td><td>{item.responses}</td><td>{item.interventions}</td><td>{duration(item.ttlSeconds)}</td><td>{item.expiresAt ? new Date(item.expiresAt).toLocaleString('en-AU', { timeZone: 'Australia/Brisbane' }) : 'Never'}</td>
        </tr>) : <tr><td colSpan={10}>No initiative keys found.</td></tr>}</tbody>
      </table></div>
    </section>
  </main>;
}
