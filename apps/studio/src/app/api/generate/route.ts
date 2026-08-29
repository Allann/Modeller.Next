import { NextResponse } from 'next/server';
import { loadWorkspace } from '@/server/workspace';
import { runCliGenerationApply, runCliGenerationPreview } from '@/server/generation-process';
import { readBeforeContent } from '@/server/generation-diff';
import { buildGenerateResponseBody } from '@/server/generation-response';
import { localOnlyRouteGuard } from '@/server/playground-guard';
import { workspaceErrorResponse } from '@/server/workspace-error-response';

// Generate now writes for real (local Studio already writes your edited documents to disk
// unprompted — this matches that trust model, unlike the sandboxed playground). Runs a preview
// first, purely to learn the artifact list and snapshot each one's current on-disk content *before*
// anything is overwritten, then applies the real write. The response carries both halves so the
// client can diff against the actual previous file content — correct even on the very first click
// after restarting Studio, not just session-to-session. See generation-response.ts for the
// conflict-handling logic.
export async function GET() {
  const guarded = localOnlyRouteGuard();
  if (guarded) return guarded;

  let workspace;
  try {
    workspace = await loadWorkspace();
  } catch (error) {
    const response = workspaceErrorResponse(error);
    if (response) return response;
    throw error;
  }
  try {
    const preview = await runCliGenerationPreview(workspace.root);
    if (preview.diagnostics.length > 0) return NextResponse.json(preview);

    const before = await readBeforeContent(workspace.root, workspace.logicalOutputRoot, preview.artifacts.map((artifact) => artifact.path));
    const applied = await runCliGenerationApply(workspace.root);
    return NextResponse.json(buildGenerateResponseBody(applied, before));
  } catch (error) {
    return NextResponse.json({ error: error instanceof Error ? error.message : 'Generation failed.' }, { status: 500 });
  }
}
