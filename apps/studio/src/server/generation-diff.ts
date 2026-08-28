import { readFile } from 'node:fs/promises';
import path from 'node:path';
import { isWorkspaceRelative } from './path-safety';

// Snapshots each artifact's current on-disk content, taken before a generate-apply run overwrites
// it — the real "before" half of the diff view (see LocalGenerationPreview.tsx), not a session's
// previous in-memory render. A path outside the output root (shouldn't happen — the CLI already
// validates logical paths, see WorkspaceLoader.Unsafe) or a file that doesn't exist yet is treated
// as empty, matching GenerationPreview.tsx's existing "no previous version yet" rendering.
export async function readBeforeContent(
  workspaceRoot: string,
  logicalOutputRoot: string,
  artifactPaths: readonly string[],
): Promise<Record<string, string>> {
  const before: Record<string, string> = {};
  for (const artifactPath of artifactPaths) {
    if (!isWorkspaceRelative(artifactPath)) continue;
    const absolutePath = path.join(workspaceRoot, logicalOutputRoot, artifactPath);
    try {
      before[artifactPath] = await readFile(absolutePath, 'utf-8');
    } catch {
      before[artifactPath] = '';
    }
  }
  return before;
}
