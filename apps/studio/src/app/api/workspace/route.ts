import { NextRequest, NextResponse } from 'next/server';
import { loadWorkspace, setWorkspaceRoot, WorkspaceNotFoundError } from '@/server/workspace';
import { localOnlyRouteGuard } from '@/server/playground-guard';

export async function GET() {
  const guarded = localOnlyRouteGuard();
  if (guarded) return guarded;

  const workspace = await loadWorkspace();
  return NextResponse.json({ root: workspace.root, sources: workspace.sources });
}

// Switches the active workspace to a different local directory (issue: load samples/child-care's
// model files, and other workspace directories, without restarting the server). `path` names the
// new workspace's own root, so — unlike the document route's `path` query parameter — it is not
// checked against isWorkspaceRelative; see setWorkspaceRoot for why.
export async function POST(request: NextRequest) {
  const guarded = localOnlyRouteGuard();
  if (guarded) return guarded;

  const { path: requestedRoot } = (await request.json()) as { path?: string };
  if (!requestedRoot) return NextResponse.json({ error: 'Missing path.' }, { status: 400 });

  try {
    const workspace = await setWorkspaceRoot(requestedRoot);
    return NextResponse.json({ root: workspace.root, sources: workspace.sources });
  } catch (error) {
    if (error instanceof WorkspaceNotFoundError) {
      return NextResponse.json({ error: error.message }, { status: 404 });
    }
    throw error;
  }
}
