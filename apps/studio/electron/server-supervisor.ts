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
 * Spawns the packaged app's pre-bundled server (server-dist/server.js, built by
 * scripts/bundle-server.mjs from server.ts) as a genuine child Node process via
 * ELECTRON_RUN_AS_NODE — a plain compiled-JS entry point, not raw TypeScript run through a
 * runtime transpiler, so the packaged app doesn't need to ship its own source or a dev tool (tsx)
 * to interpret it. The child's process.argv shape matches a plain `node server-dist/server.js
 * <args>` invocation exactly — workspace-package.ts's resolveWorkspacePackageArgument()
 * (process.argv.slice(2)) needs no changes for the file-association open-with path to keep working.
 */
export function spawnServer(resourcesPath: string, forwardedArgs: readonly string[], port: number): ChildProcess {
  const serverEntry = path.join(resourcesPath, 'server-dist', 'server.js');
  return spawn(process.execPath, [serverEntry, ...forwardedArgs], {
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
  let lastError: unknown;
  for (;;) {
    const remaining = deadline - Date.now();
    if (remaining <= 0) {
      throw new Error(`Modeller Studio server did not become ready on port ${port}: ${lastError instanceof Error ? lastError.message : (lastError ?? 'timed out')}`);
    }
    try {
      // Bounded by whatever's left of the overall deadline, not a fresh per-attempt timeout — a
      // request that connects but never responds must not push the total wait past timeoutMs.
      const response = await fetch(`http://localhost:${port}/api/workspace`, { signal: AbortSignal.timeout(remaining) });
      // Not response.ok — /api/workspace legitimately answers with a clean 404 before any
      // workspace is open (see workspace-error-response.ts), and that still means the server is
      // up. Readiness only needs proof this is *our* server responding with its own JSON shape,
      // not that a workspace is loaded — guards against some unrelated process already bound to
      // this port answering with an HTTP response that isn't ours.
      const data: unknown = await response.json();
      if (data && typeof data === 'object' && ('sources' in data || 'error' in data)) return;
      lastError = new Error(`Unexpected response from port ${port}.`);
    } catch (error) {
      lastError = error;
    }
    await new Promise((resolve) => setTimeout(resolve, intervalMs));
  }
}

// Plain child.kill() doesn't reliably kill Windows descendants — the server process spawns its own
// `dotnet` Modeller.LanguageServer child (see server/lsp-process.ts) that would otherwise survive
// the window closing. This is Windows-only (matches apps/studio's Windows-only dist:windows target).
export function killServerTree(pid: number): void {
  if (process.platform === 'win32') spawnSync('taskkill', ['/pid', String(pid), '/t', '/f']);
  else process.kill(pid);
}
