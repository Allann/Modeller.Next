import type { ProjectionGraph } from '@/lib/examples';

const NODE_HEIGHT = 44;
const NODE_MIN_WIDTH = 120;
const NODE_PADDING_X = 16;
const COLUMN_GAP = 96;
const ROW_GAP = 20;
const MARGIN = 24;
const CHAR_WIDTH = 7.5;

// Assigns each node a column via longest-path-from-source layering, then
// stacks nodes within a column top to bottom. Good enough for the small
// (single-digit node count) graphs Modeller currently projects.
function layoutColumns(graph: ProjectionGraph): string[][] {
  const incoming = new Map<string, string[]>();
  for (const node of graph.nodes) incoming.set(node.id, []);
  for (const edge of graph.edges) incoming.get(edge.targetId)?.push(edge.sourceId);

  const layer = new Map<string, number>();
  function layerOf(nodeId: string, seen: Set<string>): number {
    if (layer.has(nodeId)) return layer.get(nodeId)!;
    if (seen.has(nodeId)) return 0; // cycle guard; not expected for current view kinds
    seen.add(nodeId);
    const predecessors = incoming.get(nodeId) ?? [];
    const value = predecessors.length === 0 ? 0 : Math.max(...predecessors.map((id) => layerOf(id, seen))) + 1;
    layer.set(nodeId, value);
    return value;
  }
  for (const node of graph.nodes) layerOf(node.id, new Set());

  const columnCount = Math.max(0, ...Array.from(layer.values())) + 1;
  const columns: string[][] = Array.from({ length: columnCount }, () => []);
  for (const node of graph.nodes) columns[layer.get(node.id) ?? 0].push(node.id);
  return columns;
}

export function GraphDiagram({ graph }: { graph: ProjectionGraph }) {
  if (graph.nodes.length === 0) return null;

  const columns = layoutColumns(graph);
  const columnWidths = columns.map((columnNodeIds) =>
    Math.max(
      NODE_MIN_WIDTH,
      ...columnNodeIds.map((id) => {
        const label = graph.nodes.find((node) => node.id === id)?.label ?? '';
        return label.length * CHAR_WIDTH + NODE_PADDING_X * 2;
      }),
    ),
  );

  const positions = new Map<string, { x: number; y: number; width: number }>();
  let x = MARGIN;
  columns.forEach((columnNodeIds, columnIndex) => {
    const width = columnWidths[columnIndex];
    const columnHeight = columnNodeIds.length * NODE_HEIGHT + (columnNodeIds.length - 1) * ROW_GAP;
    let y = MARGIN;
    columnNodeIds.forEach((id) => {
      positions.set(id, { x, y, width });
      y += NODE_HEIGHT + ROW_GAP;
    });
    x += width + COLUMN_GAP;
    void columnHeight;
  });
  x -= COLUMN_GAP;

  const viewWidth = x + MARGIN;
  const viewHeight = Math.max(...Array.from(positions.values()).map((p) => p.y)) + NODE_HEIGHT + MARGIN;

  return (
    <svg
      viewBox={`0 0 ${viewWidth} ${viewHeight}`}
      role="img"
      aria-label="Model diagram"
      style={{ width: '100%', height: 'auto', maxWidth: `${viewWidth}px` }}
    >
      <defs>
        <marker id="arrow" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse">
          <path d="M0,0 L10,5 L0,10 z" className="graph-arrow" />
        </marker>
      </defs>
      {graph.edges.map((edge) => {
        const source = positions.get(edge.sourceId);
        const target = positions.get(edge.targetId);
        if (!source || !target) return null;
        const x1 = source.x + source.width;
        const y1 = source.y + NODE_HEIGHT / 2;
        const x2 = target.x;
        const y2 = target.y + NODE_HEIGHT / 2;
        const midX = (x1 + x2) / 2;
        return (
          <g key={edge.id}>
            <path
              d={`M${x1},${y1} C${midX},${y1} ${midX},${y2} ${x2},${y2}`}
              className="graph-edge"
              markerEnd="url(#arrow)"
            />
            {edge.label && (
              <text x={midX} y={(y1 + y2) / 2 - 6} textAnchor="middle" className="graph-edge-label">
                {edge.label}
              </text>
            )}
          </g>
        );
      })}
      {graph.nodes.map((node) => {
        const position = positions.get(node.id);
        if (!position) return null;
        return (
          <g key={node.id}>
            <rect
              x={position.x}
              y={position.y}
              width={position.width}
              height={NODE_HEIGHT}
              rx={8}
              className={`graph-node graph-node-${node.role}`}
            />
            <text x={position.x + position.width / 2} y={position.y + NODE_HEIGHT / 2 + 5} textAnchor="middle" className="graph-node-label">
              {node.label}
            </text>
          </g>
        );
      })}
    </svg>
  );
}
