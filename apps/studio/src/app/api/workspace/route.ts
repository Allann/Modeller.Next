import { NextResponse } from 'next/server';
import { loadWorkspace } from '@/server/workspace';

// No client-supplied path parameter here, so no workspace-boundary check is
// needed on this route — if a path parameter is ever added, it MUST be
// validated with isWorkspaceRelative (see src/server/path-safety.ts), same as
// the document route.
export async function GET() {
  const workspace = await loadWorkspace();
  return NextResponse.json({ sources: workspace.sources });
}
