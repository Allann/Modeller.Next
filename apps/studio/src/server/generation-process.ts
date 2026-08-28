import { spawn } from 'node:child_process';
import { dotnetArgsFor, resolveDotnetTool } from './dotnet-tool';
import { CLI } from './projection-process';
import type { ApiDiagnostic, GeneratedArtifactDto } from '@/lib/generation-types';

export interface GenerationResult {
  outputVersion: string;
  artifacts: GeneratedArtifactDto[];
  diagnostics: ApiDiagnostic[];
}

/**
 * Shells out to `modeller generate --dry-run`, the same subprocess pattern already used for
 * `modeller project` (see runCliProjection). `--dry-run` never writes to disk — this is a preview,
 * matching the playground's own generation-preview guarantee (see PlaygroundWorkbench's read-only
 * note) even though local Studio, unlike the playground, could otherwise write for real.
 */
export function runCliGeneration(workspaceRoot: string): Promise<GenerationResult> {
  const location = resolveDotnetTool(CLI);
  const args = dotnetArgsFor(location, ['generate', '--workspace', '.', '--dry-run', '--format', 'json']);

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
