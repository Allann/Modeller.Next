import { NextRequest, NextResponse } from 'next/server';
import { loadWorkspace } from '@/server/workspace';
import { resolveDocumentStatus } from '@/server/document-status';
import { localOnlyRouteGuard } from '@/server/playground-guard';
import { workspaceErrorResponse } from '@/server/workspace-error-response';

// Cheap polling counterpart to GET /api/document: returns just each requested document's on-disk
// mtime, not its content, so WorkbenchShell can detect an external edit (another editor, git, a
// generator run) without re-reading and re-diffing full file content every poll tick.
export async function GET(request: NextRequest) {
  const guarded = localOnlyRouteGuard();
  if (guarded) return guarded;

  const paths = request.nextUrl.searchParams.getAll('path');
  if (paths.length === 0) return NextResponse.json({ error: 'Missing path.' }, { status: 400 });

  let workspace;
  try {
    workspace = await loadWorkspace();
  } catch (error) {
    const response = workspaceErrorResponse(error);
    if (response) return response;
    throw error;
  }

  const statuses = await Promise.all(paths.map((relativePath) => resolveDocumentStatus(workspace, relativePath)));
  return NextResponse.json({ statuses });
}
