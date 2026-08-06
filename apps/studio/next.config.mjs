// Baseline production hardening (issue #74) — a considered starting point, not an exhaustively
// audited final policy. Wider than apps/website's because this app actually needs it: Monaco's
// editor worker runs from a blob: URL, onigasm's TextMate grammar engine needs 'wasm-unsafe-eval',
// and the playground calls the hosted Modeller.Api origin directly from the browser (local mode's
// own /lsp WebSocket is same-origin, already covered by 'self').
const apiOrigin = process.env.NEXT_PUBLIC_MODELLER_API_URL || "'self'";
// The deployed playground lives at modeller.website/playground, not its own subdomain (issue
// #74) — this app still builds and deploys as its own Vercel project, but modeller.website's
// rewrites() proxies that path to it (Next.js "Multi Zones" pattern), so it needs to know its own
// mount point to generate correct asset/route URLs. Set only via the deployed project's env vars;
// local dev and Playwright leave it unset so the app still serves from '/'.
const basePath = process.env.NEXT_PUBLIC_STUDIO_BASE_PATH || undefined;
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
      `script-src 'self' 'unsafe-inline' 'wasm-unsafe-eval' https://va.vercel-scripts.com${isDev ? " 'unsafe-eval'" : ''}`,
      "style-src 'self' 'unsafe-inline'",
      "img-src 'self' data:",
      "font-src 'self' data:",
      // 'self' for the bundled Monaco editor worker (src/lib/monaco-worker.ts), which Turbopack
      // serves as a same-origin chunk; blob: for the wrapper worker Monaco creates around it.
      "worker-src 'self' blob:",
      `connect-src 'self' ${apiOrigin} https://va.vercel-scripts.com https://vitals.vercel-insights.com`,
      "frame-ancestors 'none'",
      "base-uri 'self'",
      "form-action 'self'",
    ].join('; '),
  },
];

/** @type {import('next').NextConfig} */
const nextConfig = {
  reactStrictMode: true,
  basePath,
  // Locally, the playground and local-mode Playwright projects
  // (playwright.config.ts) both run `npm run dev` from this same directory
  // concurrently — sharing the default `.next` build/cache dir corrupts
  // Turbopack's persistent cache under concurrent writers, so the playground
  // dev server gets its own. Skip this on Vercel (VERCEL is always set during
  // its builds): NEXT_PUBLIC_MODELLER_STUDIO_MODE=playground is also required
  // there for the deployed app, but Vercel's build wrapper expects output at
  // the standard `.next`, not `.next-playground`.
  distDir:
    process.env.NEXT_PUBLIC_MODELLER_STUDIO_MODE === 'playground' && !process.env.VERCEL
      ? '.next-playground'
      : '.next',
  async headers() {
    return [{ source: '/:path*', headers: SECURITY_HEADERS }];
  },
};

export default nextConfig;
