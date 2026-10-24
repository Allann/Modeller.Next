import { spawn } from 'node:child_process';
import { dotnetArgsFor, resolveDotnetTool } from './dotnet-tool';
import { CLI } from './projection-process';
import type { ApiDiagnostic, GeneratedArtifactDto } from '@/lib/generation-types';

export interface GenerationChange {
  path: string;
  status: 'create' | 'change' | 'unchanged' | 'conflict' | 'stale' | 'remove';
  artifactId: string;
}

export interface GenerationResult {
  outputVersion: string;
  changes: GenerationChange[];
  artifacts: GeneratedArtifactDto[];
  diagnostics: ApiDiagnostic[];
}

// `modeller generate --format json`'s early-failure path (WorkspaceLoader.Failure in the CLI —
// a bad config.json, an unreadable source, a missing identity-registry entry, anything caught
// before rendering even starts) prints only `{ outputVersion, diagnostics }`, with no `changes` or
// `artifacts` key at all — unlike a normal run, which always includes both (even as `[]`). Without
// this, a real diagnostic from that path silently vanished: the /api/generate route only forwards
// diagnostics when `preview.diagnostics.length > 0`, but the *client* only knows how to notice a
// missing `artifacts` array as "the request failed" and had nothing named `error` to show instead
// — so the actual diagnostic was dropped and the UI just said "Failed to generate." Normalizing the
// two possibly-missing arrays here, right at the CLI JSON boundary, keeps GenerationResult honest
// everywhere else: every other consumer (the route, GenerationPreview) can keep assuming both are
// always real arrays.
export function normalizeGenerationResult(parsed: Partial<GenerationResult>): GenerationResult {
  return {
    outputVersion: parsed.outputVersion ?? '1.0',
    changes: parsed.changes ?? [],
    artifacts: parsed.artifacts ?? [],
    diagnostics: parsed.diagnostics ?? [],
  };
}

/**
 * Shells out to `modeller generate`, the same subprocess pattern already used for `modeller
 * project` (see runCliProjection). `dryRun: true` never writes to disk — used first to learn which
 * artifacts a generate would touch (see the /api/generate route's before/after snapshot), before
 * `dryRun: false` actually applies it.
 */
function runCli(workspaceRoot: string, dryRun: boolean): Promise<GenerationResult> {
  const location = resolveDotnetTool(CLI);
  const extraArgs = ['generate', '--workspace', '.', '--format', 'json'];
  if (dryRun) extraArgs.push('--dry-run');
  const args = dotnetArgsFor(location, extraArgs);

  return new Promise((resolve, reject) => {
    const child = spawn('dotnet', args, { cwd: workspaceRoot, stdio: ['ignore', 'pipe', 'pipe'] });
    let stdout = '';
    let stderr = '';
    child.stdout.on('data', (chunk: Buffer) => { stdout += chunk.toString('utf-8'); });
    child.stderr.on('data', (chunk: Buffer) => { stderr += chunk.toString('utf-8'); });
    child.on('error', reject);
    child.on('close', () => {
      if (!stdout.trim()) {
        reject(new Error(stderr.trim() || 'modeller generate produced no output'));
        return;
      }
      try {
        resolve(normalizeGenerationResult(JSON.parse(stdout) as Partial<GenerationResult>));
      } catch {
        reject(new Error(`Failed to parse modeller generate output: ${stdout}`));
      }
    });
  });
}

export function runCliGenerationPreview(workspaceRoot: string): Promise<GenerationResult> {
  return runCli(workspaceRoot, true);
}

export function runCliGenerationApply(workspaceRoot: string): Promise<GenerationResult> {
  return runCli(workspaceRoot, false);
}
