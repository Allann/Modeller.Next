import { NextResponse } from 'next/server';
import { localOnlyRouteGuard } from '@/server/playground-guard';
import { buildPublishedSnapshot } from '@/server/published-snapshot';
import { workspaceErrorResponse } from '@/server/workspace-error-response';

export async function GET() {
  const guarded = localOnlyRouteGuard();
  if (guarded) return guarded;

  try {
    return NextResponse.json(await buildPublishedSnapshot());
  } catch (error) {
    const response = workspaceErrorResponse(error);
    if (response) return response;
    return NextResponse.json({ error: error instanceof Error ? error.message : 'Could not build the published snapshot.' }, { status: 500 });
  }
}
