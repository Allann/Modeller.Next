'use client';

import { useCallback, useEffect, useState } from 'react';
import {
  ReactFlow,
  ReactFlowProvider,
  applyEdgeChanges,
  applyNodeChanges,
  type Edge,
  type EdgeChange,
  type Node,
  type NodeChange,
} from '@xyflow/react';
import dagre from '@dagrejs/dagre';
import '@xyflow/react/dist/style.css';
import {
  fetchProjectionGraph,
  fetchProjectionRoots,
  SUPPORTED_VIEWS,
  type ProjectionDiagnostic,
  type ProjectionGraph,
  type ProjectionRoot,
  type ProjectionView,
} from '@/lib/projection-client';

const NODE_WIDTH = 180;
const NODE_HEIGHT = 44;

export function DiagramPane() {
  const [view, setView] = useState<ProjectionView>('Lifecycle');
  const [roots, setRoots] = useState<ProjectionRoot[]>([]);
  const [rootId, setRootId] = useState<string>('');
  const [graph, setGraph] = useState<ProjectionGraph | undefined>();
  const [diagnostics, setDiagnostics] = useState<ProjectionDiagnostic[]>([]);
  const [error, setError] = useState<string | undefined>();
  const [nodes, setNodes] = useState<Node[]>([]);
  const [edges, setEdges] = useState<Edge[]>([]);

  useEffect(() => {
    const laid = layoutGraph(graph);
    setNodes(laid.nodes);
    setEdges(laid.edges);
  }, [graph]);

  // Dragging a node is a layout-only change (repositioning, not editing the
  // model) — see the semantic/view/layout/session split already decided in
  // docs/architecture/decisions/diagram-projections-editing-semantics.mdx.
  // React Flow requires wiring onNodesChange/onEdgesChange yourself for a
  // controlled `nodes`/`edges` prop; without it, drags don't persist.
  const onNodesChange = useCallback((changes: NodeChange[]) => {
    setNodes((current) => applyNodeChanges(changes, current));
  }, []);
  const onEdgesChange = useCallback((changes: EdgeChange[]) => {
    setEdges((current) => applyEdgeChanges(changes, current));
  }, []);

  useEffect(() => {
    setRootId('');
    setGraph(undefined);
    void fetchProjectionRoots(view)
      .then(setRoots)
      .catch((fetchError: unknown) => setError(fetchError instanceof Error ? fetchError.message : 'Failed to load roots.'));
  }, [view]);

  useEffect(() => {
    if (!rootId) return;
    setError(undefined);
    setDiagnostics([]);
    void fetchProjectionGraph(view, rootId)
      .then((result) => {
        setGraph(result.graph);
        setDiagnostics(result.diagnostics ?? []);
      })
      .catch((fetchError: unknown) => setError(fetchError instanceof Error ? fetchError.message : 'Failed to load diagram.'));
  }, [view, rootId]);

  return (
    <div className="diagram-pane">
      <div className="diagram-toolbar">
        <select value={view} onChange={(event) => setView(event.target.value as ProjectionView)}>
          {SUPPORTED_VIEWS.map((kind) => (
            <option key={kind} value={kind}>
              {kind}
            </option>
          ))}
        </select>
        <select value={rootId} onChange={(event) => setRootId(event.target.value)}>
          <option value="">Select a root…</option>
          {roots.map((root) => (
            <option key={root.id} value={root.id}>
              {root.name}
            </option>
          ))}
        </select>
      </div>
      {error && <div className="diagram-error">{error}</div>}
      {diagnostics.map((diagnostic) => (
        <div key={diagnostic.code} className="diagram-error">
          {diagnostic.message}
        </div>
      ))}
      {!error && diagnostics.length === 0 && !graph && (
        <div className="diagram-placeholder">{rootId ? 'Loading…' : 'Pick a root to view its diagram.'}</div>
      )}
      {graph && (
        <div style={{ flex: 1, minHeight: 0 }}>
          <ReactFlowProvider>
            <ReactFlow
              nodes={nodes}
              edges={edges}
              onNodesChange={onNodesChange}
              onEdgesChange={onEdgesChange}
              fitView
              proOptions={{ hideAttribution: true }}
            />
          </ReactFlowProvider>
        </div>
      )}
    </div>
  );
}

function layoutGraph(graph: ProjectionGraph | undefined): { nodes: Node[]; edges: Edge[] } {
  if (!graph) return { nodes: [], edges: [] };

  const layout = new dagre.graphlib.Graph();
  layout.setDefaultEdgeLabel(() => ({}));
  layout.setGraph({ rankdir: 'LR' });
  for (const node of graph.nodes) layout.setNode(node.id, { width: NODE_WIDTH, height: NODE_HEIGHT });
  for (const edge of graph.edges) layout.setEdge(edge.sourceId, edge.targetId);
  dagre.layout(layout);

  const nodes: Node[] = graph.nodes.map((node) => {
    const position = layout.node(node.id);
    return {
      id: node.id,
      data: { label: node.label },
      position: { x: position.x - NODE_WIDTH / 2, y: position.y - NODE_HEIGHT / 2 },
      style: { width: NODE_WIDTH },
    };
  });

  const edges: Edge[] = graph.edges.map((edge) => ({
    id: edge.id,
    source: edge.sourceId,
    target: edge.targetId,
    label: edge.label || undefined,
  }));

  return { nodes, edges };
}
