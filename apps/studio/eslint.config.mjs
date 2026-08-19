// eslint-config-next 16.x ships a flat-config-native default export (see
// node_modules/eslint-config-next/dist/index.js) rather than the legacy
// eslintrc-style shareable names ('next/core-web-vitals', 'next/typescript').
// Routing those legacy names through @eslint/eslintrc's FlatCompat crashes on
// this eslint/eslint-config-next combination — FlatCompat's config validator
// hits a schema error and its own error-formatter then throws
// "Converting circular structure to JSON" trying to report it (a circular
// plugin object reference). Importing the flat config directly sidesteps
// FlatCompat entirely.
import nextConfig from 'eslint-config-next';

const config = [
  ...nextConfig,
  {
    ignores: ['server-bin/**', '.next/**', '.next-playground/**', '.next-playground-local/**', 'tests/**'],
  },
];

export default config;
