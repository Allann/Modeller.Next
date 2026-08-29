// Bundles server.ts and its first-party imports (src/server/**, src/lib/**) into one plain-JS
// file for the packaged Electron app, so the installer ships compiled output instead of raw
// TypeScript source plus a runtime transpiler (tsx) — the same reason electron:build compiles
// electron/*.ts ahead of time rather than shipping it for tsx to transpile on launch.
// `packages: 'external'` leaves every node_modules import (next, ws, monaco-editor, ...) exactly
// as node_modules already ships it — only first-party relative/`@/`-aliased imports get inlined.
import { build } from 'esbuild';

await build({
  entryPoints: ['server.ts'],
  bundle: true,
  platform: 'node',
  format: 'cjs',
  target: 'node22',
  packages: 'external',
  outfile: 'server-dist/server.js',
  logLevel: 'info',
});
