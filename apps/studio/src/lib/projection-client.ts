export const SUPPORTED_VIEWS = ['BehaviourMap', 'Lifecycle', 'CausalityAndEventFlow', 'ContextMap', 'Structural', 'RuleDecision'] as const;
export type ProjectionView = (typeof SUPPORTED_VIEWS)[number];

export interface ProjectionRoot {
  id: string;
  name: string;
  slug: string;
}

export interface ProjectionNode {
  id: string;
  role: string;
  label: string;
  semanticIds: string[];
}

export interface ProjectionEdge {
  id: string;
  role: string;
  label: string;
  sourceId: string;
  targetId: string;
  semanticIds: string[];
}

export interface ProjectionGraph {
  sourceRevision: number;
  kind: string;
  nodes: ProjectionNode[];
  edges: ProjectionEdge[];
}

export interface ProjectionDiagnostic {
  code: string;
  message: string;
}

export async function fetchProjectionRoots(view: ProjectionView): Promise<ProjectionRoot[]> {
  const response = await fetch(`/api/projection/roots?view=${view}`);
  const data = (await response.json()) as { roots?: ProjectionRoot[]; error?: string };
  if (!response.ok) throw new Error(data.error ?? 'Failed to load projection roots.');
  return data.roots ?? [];
}

export async function fetchProjectionGraph(
  view: ProjectionView,
  root: string,
): Promise<{ graph?: ProjectionGraph; diagnostics?: ProjectionDiagnostic[] }> {
  const response = await fetch(`/api/projection?view=${view}&root=${encodeURIComponent(root)}`);
  const data = (await response.json()) as { graph?: ProjectionGraph; diagnostics?: ProjectionDiagnostic[]; error?: string };
  if (!response.ok) throw new Error(data.error ?? 'Failed to load projection.');
  return data;
}
