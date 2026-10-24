import { stat } from 'node:fs/promises';
import { isKnownSource, resolveSourcePath, type Workspace } from './workspace';

export interface DocumentStatus {
  path: string;
  // null covers both "not a known source of this workspace" and "stat failed" (deleted, moved, or
  // briefly unreadable mid-write) — the client treats both the same way, see documentSync.ts.
  mtimeMs: number | null;
}

// The decision logic behind GET /api/document/status, isolated from the route handler so it's
// unit-testable against a real temp-directory workspace rather than needing NextRequest mocking —
// see tests/unit/document-status.test.ts.
export async function resolveDocumentStatus(workspace: Workspace, relativePath: string): Promise<DocumentStatus> {
  if (!isKnownSource(workspace, relativePath)) return { path: relativePath, mtimeMs: null };
  try {
    const stats = await stat(resolveSourcePath(workspace, relativePath));
    return { path: relativePath, mtimeMs: stats.mtimeMs };
  } catch {
    return { path: relativePath, mtimeMs: null };
  }
}
