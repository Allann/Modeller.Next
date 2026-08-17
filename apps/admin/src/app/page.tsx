import { signOut } from '@/auth';
import { getDashboard, type DateRange } from '@/lib/dashboard';

function iso(date: Date) { return date.toISOString().slice(0, 10); }
function rangeFor(days: number): DateRange { const to = new Date(); const from = new Date(to); from.setUTCDate(from.getUTCDate() - days + 1); return { from: iso(from), to: iso(to) }; }
function safeDate(value: string | undefined, fallback: string) { return value && /^\d{4}-\d{2}-\d{2}$/.test(value) ? value : fallback; }
const percent = (value: number) => new Intl.NumberFormat('en-AU', { style: 'percent', maximumFractionDigits: 1 }).format(value);

export default async function AdminPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const params = await searchParams;
  const preset = Number(params.days ?? 30); const base = rangeFor([7, 30, 90].includes(preset) ? preset : 30);
  const range = { from: safeDate(typeof params.from === 'string' ? params.from : undefined, base.from), to: safeDate(typeof params.to === 'string' ? params.to : undefined, base.to) };
  const includeInternal = params.internal === '1';
  const dashboard = await getDashboard(range, includeInternal);
  return <main>
    <header><div><p className="eyebrow">Private product analytics</p><h1>Modeller engagement</h1></div><form action={async () => { 'use server'; await signOut({ redirectTo: '/sign-in' }); }}><button>Sign out</button></form></header>
    <nav>{[7,30,90].map((days) => <a key={days} href={`/?days=${days}${includeInternal ? '&internal=1' : ''}`}>{days} days</a>)}</nav>
    <form className="filters"><label>From <input name="from" type="date" defaultValue={range.from}/></label><label>To <input name="to" type="date" defaultValue={range.to}/></label><label><input name="internal" type="checkbox" value="1" defaultChecked={includeInternal}/> Include internal use</label><button>Apply</button></form>
    <section className="cards"><article><span>Visitors</span><strong>{dashboard.headline.visitors}</strong></article><article><span>Meaningful use</span><strong>{percent(dashboard.headline.meaningfulRate)}</strong></article><article><span>7-day return</span><strong>{percent(dashboard.headline.return7)}</strong></article><article><span>30-day return</span><strong>{percent(dashboard.headline.return30)}</strong></article></section>
    <section className="grid"><Panel title="Initiative funnel" rows={dashboard.funnel.map((x) => [x.name, x.visitors])}/><Panel title="Cross-site journey" rows={dashboard.journeys.map((x) => [x.source, x.initiatives])}/><Panel title="Feature engagement" rows={dashboard.features.map((x) => [x.event, x.uses])}/><Panel title="Retention cohorts" rows={dashboard.retention.map((x) => [x.period, percent(x.rate)])}/></section>
    <section><h2>Data quality</h2><dl><dt>Last event</dt><dd>{dashboard.quality.lastEvent ?? 'No events'}</dd><dt>Unknown events</dt><dd>{dashboard.quality.unknownEvents}</dd><dt>Missing required properties</dt><dd>{dashboard.quality.missingProperties}</dd><dt>Internal events</dt><dd>{dashboard.quality.internalEvents}</dd></dl></section>
  </main>;
}
function Panel({ title, rows }: { title: string; rows: Array<[string, string | number]> }) { return <section><h2>{title}</h2><table><tbody>{rows.length ? rows.map(([name,value]) => <tr key={name}><th>{name}</th><td>{value}</td></tr>) : <tr><td>No data</td></tr>}</tbody></table></section>; }
