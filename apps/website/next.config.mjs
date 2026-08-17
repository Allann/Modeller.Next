import path from 'node:path';
import { fileURLToPath } from 'node:url';

const dirname = path.dirname(fileURLToPath(import.meta.url));

// Baseline production hardening (issue #74) — a considered starting point, not an exhaustively
// audited final policy; tighten further once real deployed traffic confirms exactly which asset
// origins this app needs. Beyond @vercel/analytics (script from and beacons to
// va.vercel-scripts.com / vitals.vercel-insights.com), the Initiative pages call the hosted
// Modeller.Api from the browser, so its origin has to be in connect-src too — see apiOrigin below.
//
// Next.js App Router streams RSC hydration payloads via inline <script>self.__next_f.push(...)
// tags, not src= URLs — 'unsafe-inline' is required in every environment, not just dev
// (confirmed the hard way: without it the page hydrates to a blank body and throws "Invariant:
// Expected a request ID to be defined... self.__next_r"). A nonce-based CSP is the real fix for
// this — it needs middleware wiring to generate a per-request nonce and thread it through Next's
// rendering — and is the natural next tightening step, not done in this baseline pass.
// 'unsafe-eval' is dev-only: Fast Refresh/HMR relies on eval() to load modules; production
// builds never need it.
const isDev = process.env.NODE_ENV !== 'production';

// The hosted Modeller.Api the Initiative pages talk to. Resolved once here and pushed into the
// client bundle via `env` below, so the CSP that has to permit the call and the code that makes it
// can never disagree. The production default is deliberate rather than dashboard-only: a build with
// NEXT_PUBLIC_MODELLER_API_URL unset used to silently ship the localhost dev fallback to
// modeller.website, where every Initiative failed with "Is the Modeller API running?". Setting the
// variable on the Vercel project still wins — that is how a Preview build points at a Preview API.
const apiOrigin =
  process.env.NEXT_PUBLIC_MODELLER_API_URL ||
  (isDev ? 'http://localhost:8080' : 'https://modeller-next.vercel.app');

// CSP scheme matching does not treat an http(s) source as covering the matching ws(s) one
// (CSP3 6.6.2.6 only relaxes in the other direction), so the SignalR hub behind the Initiative
// pages needs its own entry. Without it the WebSocket transport is refused and realtime quietly
// degrades to a slower fallback transport — the pages still work, which is exactly what makes it
// easy to miss.
const apiSocketOrigin = apiOrigin.replace(/^http/, 'ws');
const postHogOrigin = process.env.NEXT_PUBLIC_POSTHOG_HOST || 'https://us.i.posthog.com';

// /playground is proxied through to the apps/studio deployment (Next.js "Multi Zones" pattern,
// issue #74) rather than living at its own subdomain — visitors only ever see modeller.website.
// STUDIO_DEPLOYMENT_URL points at that deployment's own (non-public-facing) Vercel URL; the
// localhost fallback lets `npm run dev` here proxy to a locally running `apps/studio` playground
// build (NEXT_PUBLIC_STUDIO_BASE_PATH=/playground npm run dev, port 3101).
const studioUrl = process.env.STUDIO_DEPLOYMENT_URL || 'http://localhost:3101';

const SECURITY_HEADERS = [
  { key: 'X-Content-Type-Options', value: 'nosniff' },
  { key: 'Referrer-Policy', value: 'strict-origin-when-cross-origin' },
  { key: 'Permissions-Policy', value: 'camera=(), microphone=(), geolocation=()' },
  {
    key: 'Content-Security-Policy',
    value: [
      "default-src 'self'",
      `script-src 'self' 'unsafe-inline' https://va.vercel-scripts.com${isDev ? " 'unsafe-eval'" : ''}`,
      "style-src 'self' 'unsafe-inline'",
      "img-src 'self' data:",
      "font-src 'self' data:",
      `connect-src 'self' ${apiOrigin} ${apiSocketOrigin} ${postHogOrigin} https://va.vercel-scripts.com https://vitals.vercel-insights.com`,
      "frame-ancestors 'none'",
      "base-uri 'self'",
      "form-action 'self'",
    ].join('; '),
  },
];

/** @type {import('next').NextConfig} */
const nextConfig = {
  reactStrictMode: true,
  env: { NEXT_PUBLIC_MODELLER_API_URL: apiOrigin },
  // Scope Turbopack to this app — without this it infers a shared workspace
  // root with the sibling docs/studio apps (each has its own package-lock.json).
  turbopack: {
    root: dirname,
  },
  async rewrites() {
    return [
      { source: '/playground', destination: `${studioUrl}/playground` },
      { source: '/playground/:path*', destination: `${studioUrl}/playground/:path*` },
    ];
  },
  async headers() {
    // Excludes /playground: that path is proxied straight through to apps/studio's own
    // deployment (see rewrites() above), which sets its own, wider CSP for Monaco/onigasm.
    // Matching it here too would send a second, more restrictive CSP header alongside it and
    // break the editor the same way the original blank-page CSP regression did.
    return [{ source: '/((?!playground).*)', headers: SECURITY_HEADERS }];
  },
};

export default nextConfig;
