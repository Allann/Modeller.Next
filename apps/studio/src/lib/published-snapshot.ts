export const PUBLISHED_SNAPSHOT_VERSION = 1;

export interface PublishedGraph {
  nodes: { id: string; label: string }[];
  edges: { id: string; sourceId: string; targetId: string; label: string }[];
}

export interface PublishedDiagram {
  view: string;
  root: { id: string; name: string; slug: string };
  graph?: PublishedGraph;
  diagnostics: { code: string; message: string }[];
}

export interface PublishedSnapshot {
  version: typeof PUBLISHED_SNAPSHOT_VERSION;
  publishedAt: string;
  diagrams: PublishedDiagram[];
}

export function isPublishSlug(value: string): boolean {
  return /^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(value) && value.length <= 80;
}

export function publishedSnapshotKey(slug: string): string {
  return `modeller:published:${slug}`;
}
