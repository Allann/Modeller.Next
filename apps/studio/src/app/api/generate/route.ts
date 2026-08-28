import { NextResponse } from 'next/server';
import { loadWorkspace } from '@/server/workspace';
import { runCliGeneration } from '@/server/generation-process';
import { localOnlyRouteGuard } from '@/server/playground-guard';

export async function GET() {
  const guarded = localOnlyRouteGuard();
  if (guarded) return guarded;

  const workspace = await loadWorkspace();
  try {
    const result = await runCliGeneration(workspace.root);
    return NextResponse.json(result);
  } catch (error) {
    return NextResponse.json({ error: error instanceof Error ? error.message : 'Generation failed.' }, { status: 500 });
  }
}
