// Main process talking to the local server it supervises — plain Node fetch, no CORS concern
// (that's a browser-only mechanism) and no need to round-trip through the renderer/IPC just to
// read workspace info the server already has.
export interface WorkspaceInfo {
  root: string;
  logicalOutputRoot: string;
}

export async function fetchWorkspaceInfo(port: number): Promise<WorkspaceInfo | undefined> {
  try {
    const response = await fetch(`http://localhost:${port}/api/workspace`);
    if (!response.ok) return undefined;
    const data = (await response.json()) as { root?: string; logicalOutputRoot?: string };
    if (!data.root) return undefined;
    return { root: data.root, logicalOutputRoot: data.logicalOutputRoot ?? 'generated' };
  } catch {
    return undefined;
  }
}
