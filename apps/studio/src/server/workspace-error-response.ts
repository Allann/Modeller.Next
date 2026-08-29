import { NextResponse } from 'next/server';
import { WorkspaceNotFoundError, WorkspacePackageOpenError } from './workspace';

// Every route that calls loadWorkspace()/setWorkspaceRoot() needs the same translation from a
// thrown workspace error to a clean JSON response — without it, an uncaught exception from a route
// handler reaches the client as a body that isn't valid JSON at all, and every call site's
// `response.json()` fails with a confusing "Unexpected end of JSON input" instead of the actual
// problem. Most visible on first launch of the packaged app, which has no bundled default
// workspace (unlike dev, which falls back to samples/child-care) — loadWorkspace() throws
// WorkspaceNotFoundError until the user picks a folder via Open Folder.
export function workspaceErrorResponse(error: unknown): NextResponse | undefined {
  if (error instanceof WorkspaceNotFoundError) return NextResponse.json({ error: error.message }, { status: 404 });
  if (error instanceof WorkspacePackageOpenError) return NextResponse.json({ error: error.message }, { status: 400 });
  return undefined;
}
