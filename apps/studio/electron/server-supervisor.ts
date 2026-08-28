// Electron-independent helpers for electron/main.ts, kept in their own module (no `import
// 'electron'`) so they can be unit-tested under plain Node — requiring the real `electron` package
// outside an actual Electron process returns a path string, not the {app, BrowserWindow, ...} API,
// so main.ts itself can't be imported directly from a Node test.
import { spawn, spawnSync, type ChildProcess } from 'node:child_process';
import path from 'node:path';

export function resolveForwardedArgs(argv: readonly string[], defaultApp: boolean): string[] {
  // Dev (`electron .`): argv is [electronExe, mainJsPath, ...userArgs] — same shape tsx/dist/cli.mjs
  // itself would see. Packaged: argv is [appExe, ...userArgs] — no separate "script" entry, since
  // the packaged binary *is* the entry point. `process.defaultApp` is Electron's own signal for
  // which shape applies (true only when running unpackaged via `electron .`/`electron <path>`).
  return defaultApp ? argv.slice(2) : argv.slice(1);
}

/**
 * Spawns the exact same server entry point the Windows installer already runs
 * (node_modules/tsx/dist/cli.mjs server.ts), as a genuine child Node process via
 * ELECTRON_RUN_AS_NODE, so the child's process.argv shape matches a plain `node server.ts <args>`
 * invocation exactly — workspace-package.ts's resolveWorkspacePackageArgument()
 * (process.argv.slice(2)) needs no changes for the file-association open-with path to keep working.
 */
export function spawnServer(resourcesPath: string, forwardedArgs: readonly string[], port: number): ChildProcess {
  const tsxCli = path.join(resourcesPath, 'node_modules', 'tsx', 'dist', 'cli.mjs');
  const serverEntry = path.join(resourcesPath, 'server.ts');
  return spawn(process.execPath, [tsxCli, serverEntry, ...forwardedArgs], {
    cwd: resourcesPath,
    // NODE_ENV=production matches the previous .cmd installer's own `set "NODE_ENV=production"` —
    // the Electron shell always runs against a built .next output (npm run build), never Next's
    // dev server, so server.ts's own `dev = process.env.NODE_ENV !== 'production'` must resolve
    // false here regardless of what NODE_ENV happens to be set to in Electron's own environment.
    env: { ...process.env, ELECTRON_RUN_AS_NODE: '1', PORT: String(port), NODE_ENV: 'production' },
    stdio: ['ignore', 'pipe', 'pipe'],
  });
}

export async function waitForServerReady(port: number, timeoutMs = 20_000, intervalMs = 200): Promise<void> {
  const deadline = Date.now() + timeoutMs;
  for (;;) {
    try {
      await fetch(`http://localhost:${port}/api/workspace`);
      return;
    } catch (error) {
      if (Date.now() >= deadline) throw new Error(`Modeller Studio server did not become ready on port ${port}: ${error instanceof Error ? error.message : error}`);
      await new Promise((resolve) => setTimeout(resolve, intervalMs));
    }
  }
}

// Plain child.kill() doesn't reliably kill Windows descendants — the server process spawns its own
// `dotnet` Modeller.LanguageServer child (see server/lsp-process.ts) that would otherwise survive
// the window closing. This is Windows-only (matches apps/studio's Windows-only dist:windows target).
export function killServerTree(pid: number): void {
  if (process.platform === 'win32') spawnSync('taskkill', ['/pid', String(pid), '/t', '/f']);
  else process.kill(pid);
}
