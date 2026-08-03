import { spawn } from 'node:child_process';
import { dotnetArgsFor, resolveDotnetTool } from './dotnet-tool';

const CLI = {
  envVar: 'MODELLER_CLI_PATH',
  bundledDllRelativePath: 'server-bin/Modeller.Cli.dll',
  projectRelativePath: 'src/Modeller.Cli/Modeller.Cli.csproj',
};

export interface ProjectionRoot {
  id: string;
  name: string;
  slug: string;
}

export interface ProjectionRootsResult {
  outputVersion: string;
  view: string;
  roots: ProjectionRoot[];
}

export interface ProjectionGraphResult {
  outputVersion: string;
  graph?: unknown;
  diagnostics?: { code: string; message: string }[];
}

/**
 * Shells out to `modeller project`, the same subprocess pattern already used
 * for `validate` (see src/server/workspace.ts's pre-publish gate note) and
 * `Modeller.LanguageServer` (lsp-process.ts). Runs with cwd set to the
 * workspace root itself so `--workspace .` avoids needing to compute a path
 * relative to wherever this Node process happens to be running from — the
 * CLI rejects rooted/absolute workspace paths (see CliApplication.IsWorkspaceRelative
 * and WorkspaceLoader.Unsafe).
 */
export function runCliProjection(
  workspaceRoot: string,
  view: string,
  root: string | undefined,
): Promise<ProjectionRootsResult | ProjectionGraphResult> {
  const location = resolveDotnetTool(CLI);
  const extraArgs = ['project', '--workspace', '.', '--view', view, '--format', 'json'];
  if (root) extraArgs.push('--root', root);
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
        reject(new Error(stderr.trim() || 'modeller project produced no output'));
        return;
      }
      try {
        resolve(JSON.parse(stdout));
      } catch {
        reject(new Error(`Failed to parse modeller project output: ${stdout}`));
      }
    });
  });
}
