// Prerequisite check ahead of packaging the Electron shell (electron-builder.json) — the actual
// staging/installer generation is electron-builder's job now, not this script's. Previously this
// script also hand-copied build output into packaging/dist/windows and wrote a .cmd/.vbs/.iss
// browser-launch installer by hand; that installer ran the server with a hidden window (no way to
// quit but Task Manager) and is replaced by a real Electron BrowserWindow (see electron/main.ts).
import { existsSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const studioRoot = path.resolve(here, '..');

for (const requiredPath of [
  '.next',
  'node_modules',
  'server-dist/server.js',
  'server-bin/Modeller.Cli.dll',
  'server-bin/Modeller.LanguageServer.dll',
]) {
  if (!existsSync(path.join(studioRoot, requiredPath))) {
    throw new Error(`Missing ${requiredPath}. Run npm run build, npm run server:build, and npm run server:bundle before packaging Studio.`);
  }
}

console.log('Prerequisites present — proceeding to electron-builder.');
