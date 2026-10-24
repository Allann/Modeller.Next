import { SUPPORTED_VIEWS } from '@/lib/projection-client';
import { PUBLISHED_SNAPSHOT_VERSION, type PublishedSnapshot } from '@/lib/published-snapshot';
import { runCliProjection, type ProjectionGraphResult, type ProjectionRootsResult } from './projection-process';
import { loadWorkspace } from './workspace';

export async function buildPublishedSnapshot(): Promise<PublishedSnapshot> {
  const workspace = await loadWorkspace();
  const diagrams = [];

  for (const view of SUPPORTED_VIEWS) {
    const roots = await runCliProjection(workspace.root, view, undefined) as ProjectionRootsResult;
    for (const root of roots.roots) {
      const result = await runCliProjection(workspace.root, view, root.id) as ProjectionGraphResult;
      diagrams.push({ view, root, graph: result.graph as PublishedSnapshot['diagrams'][number]['graph'], diagnostics: result.diagnostics ?? [] });
    }
  }

  return { version: PUBLISHED_SNAPSHOT_VERSION, publishedAt: new Date().toISOString(), diagrams };
}
