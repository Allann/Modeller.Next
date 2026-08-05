// apps/studio has no Tailwind/PostCSS needs of its own (plain CSS only) — this file's only job is
// to exist, so Next's PostCSS config lookup stops here instead of walking up into the sibling docs
// site's postcss.config.mjs at the repo root, which names a plugin (@tailwindcss/postcss) that
// isn't installed in this app's own node_modules. Confirmed on Vercel (not reproducible locally):
// that walk-up happened there and broke the build with "Cannot find module '@tailwindcss/postcss'".
/** @type {import('postcss-load-config').Config} */
const config = {
  plugins: {},
};

export default config;
