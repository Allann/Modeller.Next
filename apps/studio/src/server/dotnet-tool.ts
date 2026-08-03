// Generalized version of the binary-resolution pattern originally written just
// for Modeller.LanguageServer (see lsp-process.ts) — a Node/TS parallel of
// editors/vscode-modeller/src/extension.ts's resolveServer(), adapted for any
// .NET tool in this repo: env var override -> bundled dll -> `dotnet run
// --project` fallback. Not imported from the VS Code extension itself — that
// package depends on the `vscode` API, unavailable outside the extension host.
//
// Paths are resolved from process.cwd(), not __dirname: this module is used
// both by server.ts (run directly via tsx, where __dirname is the real source
// location) and by Next.js API routes (bundled by Turbopack into .next/, where
// __dirname points somewhere else entirely at runtime). process.cwd() is
// always apps/studio regardless of how a given module got loaded, since
// that's the directory npm run dev/start is invoked from.
import { existsSync } from 'node:fs';
import path from 'node:path';

const STUDIO_ROOT = process.cwd();
const REPO_ROOT = path.resolve(STUDIO_ROOT, '..', '..');

export interface DotnetToolConfig {
  /** Env var checked first for an explicit override path to a built dll. */
  envVar: string;
  /** Bundled dll path, relative to apps/studio (e.g. `server-bin/Modeller.Cli.dll`). */
  bundledDllRelativePath: string;
  /** csproj path, relative to the repo root. */
  projectRelativePath: string;
}

export type DotnetToolLocation = { kind: 'dll'; path: string } | { kind: 'project'; path: string };

export function resolveDotnetTool(config: DotnetToolConfig): DotnetToolLocation {
  const configured = process.env[config.envVar];
  if (configured && existsSync(configured)) return { kind: 'dll', path: configured };
  const bundledDll = path.resolve(STUDIO_ROOT, config.bundledDllRelativePath);
  if (existsSync(bundledDll)) return { kind: 'dll', path: bundledDll };
  return { kind: 'project', path: path.join(REPO_ROOT, config.projectRelativePath) };
}

/** Builds the `dotnet` process args for a resolved tool location, appending any extra args after `--` when running via `dotnet run`. */
export function dotnetArgsFor(location: DotnetToolLocation, extraArgs: string[] = []): string[] {
  if (location.kind === 'dll') return [location.path, ...extraArgs];
  const runArgs = ['run', '--project', location.path, '--no-launch-profile'];
  return extraArgs.length > 0 ? [...runArgs, '--', ...extraArgs] : runArgs;
}
