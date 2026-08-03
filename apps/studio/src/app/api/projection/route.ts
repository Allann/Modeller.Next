import { NextRequest, NextResponse } from 'next/server';
import { loadWorkspace } from '@/server/workspace';
import { runCliProjection } from '@/server/projection-process';

export async function GET(request: NextRequest) {
  const view = request.nextUrl.searchParams.get('view');
  const root = request.nextUrl.searchParams.get('root');
  if (!view || !root) return NextResponse.json({ error: 'Missing view or root.' }, { status: 400 });

  const workspace = await loadWorkspace();
  try {
    const result = await runCliProjection(workspace.root, view, root);
    return NextResponse.json(result);
  } catch (error) {
    return NextResponse.json({ error: error instanceof Error ? error.message : 'Projection failed.' }, { status: 500 });
  }
}
