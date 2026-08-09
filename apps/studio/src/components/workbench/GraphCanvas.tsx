'use client';

import { useCallback, useState } from 'react';
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

const NODE_WIDTH = 180;
const NODE_HEIGHT = 44;

// The dagre-laid-out React Flow renderer shared by local Studio's
// `DiagramPane` (fed from the CLI subprocess) and the playground's diagram
// pane (fed from Modeller.Api) — both already produce this same node/edge
// shape (`ApiProjectionGraph` and `ProjectionGraph` in projection-client.ts),
// so only the data-fetching around this component differs.
export interface GraphCanvasNode {
  id: string;
  label: string;
}

export interface GraphCanvasEdge {
  id: string;
  sourceId: string;
  targetId: string;
  label: string;
}

export interface GraphCanvasData {
  nodes: readonly GraphCanvasNode[];
  edges: readonly GraphCanvasEdge[];
}

export function GraphCanvas({ graph }: { graph: GraphCanvasData | undefined }) {
  // Layout is a pure derivation of `graph`, so it's recomputed during render on a `graph`
  // identity change (React's documented "adjusting state when a prop changes" pattern) rather
  // than via an effect — this is the case react-hooks/set-state-in-effect exists to steer away
  // from an unnecessary effect+extra-render round trip, unlike the fetch effects elsewhere in
  // this app where the setState is a side effect of async work, not a render-time derivation.
  const [prevGraph, setPrevGraph] = useState(graph);
  const [nodes, setNodes] = useState<Node[]>(() => layoutGraph(graph).nodes);
  const [edges, setEdges] = useState<Edge[]>(() => layoutGraph(graph).edges);

  if (graph !== prevGraph) {
    setPrevGraph(graph);
    const laid = layoutGraph(graph);
    setNodes(laid.nodes);
    setEdges(laid.edges);
  }

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

  if (!graph) return null;

  return (
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
  );
}

function layoutGraph(graph: GraphCanvasData | undefined): { nodes: Node[]; edges: Edge[] } {
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
