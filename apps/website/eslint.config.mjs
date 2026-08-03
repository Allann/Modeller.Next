// eslint-config-next 16.x ships a flat-config-native default export; see
// apps/studio/eslint.config.mjs for why the legacy FlatCompat names are
// avoided here.
import nextConfig from 'eslint-config-next';

const config = [
  ...nextConfig,
  {
    ignores: ['.next/**'],
  },
];

export default config;
