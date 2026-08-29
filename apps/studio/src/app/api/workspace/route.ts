import { NextRequest, NextResponse } from 'next/server';
import { loadWorkspace, setWorkspaceRoot } from '@/server/workspace';
import { localOnlyRouteGuard } from '@/server/playground-guard';
import { workspaceErrorResponse } from '@/server/workspace-error-response';

export async function GET() {
  const guarded = localOnlyRouteGuard();
  if (guarded) return guarded;

  try {
    const workspace = await loadWorkspace();
    return NextResponse.json({
      root: workspace.root,
      sources: workspace.sources,
      logicalOutputRoot: workspace.logicalOutputRoot,
      openedFromPackage: workspace.openedFromPackage ?? false,
    });
  } catch (error) {
    const response = workspaceErrorResponse(error);
    if (response) return response;
    throw error;
  }
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
    return NextResponse.json({ root: workspace.root, sources: workspace.sources, openedFromPackage: workspace.openedFromPackage ?? false });
  } catch (error) {
    const response = workspaceErrorResponse(error);
    if (response) return response;
    throw error;
  }
}
