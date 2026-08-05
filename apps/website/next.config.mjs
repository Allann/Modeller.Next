import path from 'node:path';
import { fileURLToPath } from 'node:url';

const dirname = path.dirname(fileURLToPath(import.meta.url));

// Baseline production hardening (issue #74) — a considered starting point, not an exhaustively
// audited final policy; tighten further once real deployed traffic confirms exactly which asset
// origins this app needs. This app is otherwise static/build-time-generated (no request-time API
// calls) — the only cross-origin allowances are for @vercel/analytics, which loads its script from
// and beacons to va.vercel-scripts.com / vitals.vercel-insights.com.
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
      "connect-src 'self' https://va.vercel-scripts.com https://vitals.vercel-insights.com",
      "frame-ancestors 'none'",
      "base-uri 'self'",
      "form-action 'self'",
    ].join('; '),
  },
];

/** @type {import('next').NextConfig} */
const nextConfig = {
  reactStrictMode: true,
  // Scope Turbopack to this app — without this it infers a shared workspace
  // root with the sibling docs/studio apps (each has its own package-lock.json).
  turbopack: {
    root: dirname,
  },
  async headers() {
    return [{ source: '/:path*', headers: SECURITY_HEADERS }];
  },
};

export default nextConfig;
