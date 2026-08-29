import { NextRequest, NextResponse } from 'next/server';
import { loadWorkspace } from '@/server/workspace';
import { runCliProjection } from '@/server/projection-process';
import { localOnlyRouteGuard } from '@/server/playground-guard';
import { workspaceErrorResponse } from '@/server/workspace-error-response';

export async function GET(request: NextRequest) {
  const guarded = localOnlyRouteGuard();
  if (guarded) return guarded;

  const view = request.nextUrl.searchParams.get('view');
  const root = request.nextUrl.searchParams.get('root');
  if (!view || !root) return NextResponse.json({ error: 'Missing view or root.' }, { status: 400 });

  let workspace;
  try {
    workspace = await loadWorkspace();
  } catch (error) {
    const response = workspaceErrorResponse(error);
    if (response) return response;
    throw error;
  }
  try {
    const result = await runCliProjection(workspace.root, view, root);
    return NextResponse.json(result);
  } catch (error) {
    return NextResponse.json({ error: error instanceof Error ? error.message : 'Projection failed.' }, { status: 500 });
  }
}
