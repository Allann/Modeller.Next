export type DateRange = { from: string; to: string };
export type Dashboard = {
  headline: { visitors: number; meaningfulRate: number; return7: number; return30: number };
  funnel: Array<{ name: string; visitors: number }>;
  journeys: Array<{ source: string; initiatives: number }>;
  features: Array<{ event: string; uses: number }>;
  retention: Array<{ period: string; rate: number }>;
  quality: { lastEvent: string | null; unknownEvents: number; missingProperties: number; internalEvents: number };
};

const knownEvents = ['site_page_viewed','meaningful_use_started','outbound_link_followed','docs_article_viewed','docs_search_used','docs_call_to_action_selected','playground_opened','example_loaded','first_edit_made','analysis_completed','projection_viewed','workspace_downloaded','share_link_copied','initiative_created','initiative_viewed','question_proposed','question_sent','response_submitted','response_accepted','gate_evaluated','intervention_selected','initiative_finalized','initiative_reopened','initiative_phase_reached'];

async function query(sql: string): Promise<unknown[][]> {
  const host = (process.env.POSTHOG_HOST ?? 'https://us.posthog.com').replace(/\/$/, '');
  const project = process.env.POSTHOG_PROJECT_ID;
  const key = process.env.POSTHOG_PERSONAL_API_KEY;
  if (!project || !key) return [];
  const response = await fetch(`${host}/api/projects/${encodeURIComponent(project)}/query/`, { method: 'POST', headers: { Authorization: `Bearer ${key}`, 'Content-Type': 'application/json' }, body: JSON.stringify({ query: { kind: 'HogQLQuery', query: sql } }), cache: 'no-store' });
  if (!response.ok) throw new Error(`PostHog query failed (${response.status}).`);
  const body = await response.json() as { results?: unknown[][] };
  return body.results ?? [];
}

function scope(range: DateRange, includeInternal: boolean) {
  return `timestamp >= toDateTime('${range.from} 00:00:00') AND timestamp < toDateTime('${range.to} 23:59:59')${includeInternal ? '' : " AND coalesce(properties.internal, false) = false"}`;
}
const number = (value: unknown) => typeof value === 'number' ? value : Number(value ?? 0);

export async function getDashboard(range: DateRange, includeInternal: boolean): Promise<Dashboard> {
  const where = scope(range, includeInternal);
  const [headline, funnel, journeys, features, retention, quality] = await Promise.all([
    query(`SELECT count(), countIf(first_meaningful IS NOT NULL), countIf(dateDiff('day', first_meaningful, last_meaningful)>=7), countIf(dateDiff('day', first_meaningful, last_meaningful)>=30) FROM (SELECT distinct_id, minIf(toNullable(timestamp), event='meaningful_use_started') AS first_meaningful, maxIf(toNullable(timestamp), event='meaningful_use_started') AS last_meaningful FROM events WHERE ${where} GROUP BY distinct_id)`),
    query(`SELECT multiIf(event='initiative_phase_reached',concat('phase:',toString(properties.phase)),event), uniq(distinct_id) FROM events WHERE ${where} AND event IN ('site_page_viewed','initiative_created','initiative_phase_reached','initiative_finalized') GROUP BY 1 ORDER BY uniq(distinct_id) DESC`),
    query(`SELECT properties.site, uniq(distinct_id) FROM events WHERE ${where} AND event IN ('docs_article_viewed','docs_search_used','playground_opened','first_edit_made','analysis_completed','projection_viewed','workspace_downloaded') AND distinct_id IN (SELECT distinct_id FROM events WHERE ${where} AND event='initiative_created') GROUP BY properties.site`),
    query(`SELECT event, count() FROM events WHERE ${where} GROUP BY event ORDER BY count() DESC LIMIT 12`),
    query(`SELECT toString(toStartOfWeek(timestamp)), uniq(distinct_id) FROM events WHERE ${where} AND event='meaningful_use_started' GROUP BY toStartOfWeek(timestamp) ORDER BY toStartOfWeek(timestamp)`),
    query(`SELECT max(timestamp), countIf(event NOT IN (${knownEvents.map((event) => `'${event}'`).join(',')})), countIf(properties.site IS NULL OR properties.contract_version IS NULL), countIf(coalesce(properties.internal,false)=true) FROM events WHERE timestamp >= toDateTime('${range.from} 00:00:00') AND timestamp < toDateTime('${range.to} 23:59:59')`),
  ]);
  const h = headline[0] ?? [0,0,0,0]; const visitors = number(h[0]); const q = quality[0] ?? [null,0,0,0];
  return {
    headline: { visitors, meaningfulRate: visitors ? number(h[1]) / visitors : 0, return7: visitors ? number(h[2]) / visitors : 0, return30: visitors ? number(h[3]) / visitors : 0 },
    funnel: funnel.map((row) => ({ name: String(row[0]), visitors: number(row[1]) })),
    journeys: journeys.map((row) => ({ source: String(row[0] ?? 'unknown'), initiatives: number(row[1]) })),
    features: features.map((row) => ({ event: String(row[0]), uses: number(row[1]) })),
    retention: retention.map((row) => ({ period: String(row[0]), rate: visitors ? number(row[1]) / visitors : 0 })),
    quality: { lastEvent: q[0] ? String(q[0]) : null, unknownEvents: number(q[1]), missingProperties: number(q[2]), internalEvents: number(q[3]) },
  };
}
