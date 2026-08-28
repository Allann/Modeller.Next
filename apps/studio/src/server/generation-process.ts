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
        resolve(JSON.parse(stdout));
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
