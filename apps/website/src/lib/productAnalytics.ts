'use client';

const EVENT_NAMES = new Set([
  'site_page_viewed', 'meaningful_use_started', 'outbound_link_followed',
  'initiative_created', 'initiative_viewed',
]);
const PROPERTY_NAMES = new Set(['route', 'action', 'site', 'environment', 'release', 'internal', 'contract_version', 'distinct_id', '$process_person_profile']);
const COOKIE_NAME = 'modeller_analytics_id';

function cookie(name: string): string | undefined {
  return document.cookie.split('; ').find((item) => item.startsWith(`${name}=`))?.split('=')[1];
}

export function analyticsId(): string {
  const existing = cookie(COOKIE_NAME);
  if (existing) return existing;
  const value = crypto.randomUUID();
  document.cookie = `${COOKIE_NAME}=${value}; Max-Age=31536000; Path=/; Domain=.modeller.website; SameSite=Lax; Secure`;
  return value;
}

export function isInternalVisitor(): boolean {
  return cookie('modeller_internal') === '1';
}

export function capture(event: string, properties: Record<string, string | number | boolean> = {}): void {
  const key = process.env.NEXT_PUBLIC_POSTHOG_KEY;
  if (!key || !EVENT_NAMES.has(event) || Object.keys(properties).some((name) => !PROPERTY_NAMES.has(name))) return;
  const host = (process.env.NEXT_PUBLIC_POSTHOG_HOST ?? 'https://us.i.posthog.com').replace(/\/$/, '');
  const body = JSON.stringify({
    api_key: key,
    event,
    properties: {
      ...properties,
      distinct_id: analyticsId(), site: 'initiative',
      environment: process.env.NEXT_PUBLIC_VERCEL_ENV ?? 'local',
      release: process.env.NEXT_PUBLIC_VERCEL_GIT_COMMIT_SHA ?? 'local',
      internal: isInternalVisitor(), contract_version: 1, $process_person_profile: false,
    },
  });
  void fetch(`${host}/capture/`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body, keepalive: true }).catch(() => undefined);
}

export function normalizedRoute(pathname: string): string {
  if (/^\/initiative\/[^/]+\/respond$/.test(pathname)) return '/initiative/:id/respond';
  if (/^\/initiative\/[^/]+$/.test(pathname)) return '/initiative/:id';
  if (/^\/examples\/[^/]+$/.test(pathname)) return '/examples/:slug';
  return pathname;
}
