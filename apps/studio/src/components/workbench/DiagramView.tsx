'use client';

import { GraphCanvas, type GraphCanvasData } from './GraphCanvas';

export interface DiagramRootOption {
  id: string;
  name: string;
}

export interface DiagramDiagnostic {
  code: string;
  message: string;
}

// The rendering half of the diagram pane, shared by local Studio's DiagramPane (fed from the CLI
// subprocess) and the playground's diagram pane (fed from Modeller.Api) — only how each caller
// fetches roots/graphs differs; the toolbar, error/diagnostic display, and placeholder rules are
// one implementation so a UI fix here can't land in one copy and miss the other.
export function DiagramView({
  view,
  onViewChange,
  viewOptions,
  rootId,
  onRootChange,
  rootOptions,
  error,
  diagnostics,
  graph,
  loading,
  extraToolbarContent,
}: {
  view: string;
  onViewChange: (view: string) => void;
  viewOptions: readonly string[];
  rootId: string;
  onRootChange: (rootId: string) => void;
  rootOptions: readonly DiagramRootOption[];
  error?: string;
  diagnostics: readonly DiagramDiagnostic[];
  graph: GraphCanvasData | undefined;
  loading: boolean;
  // Local Studio's Detach button (see DiagramPane.tsx) — the playground and detached panel windows
  // pass nothing, so this stays a plain diagram toolbar there.
  extraToolbarContent?: React.ReactNode;
}) {
  return (
    <div className="diagram-pane">
      <div className="diagram-toolbar">
        <select value={view} onChange={(event) => onViewChange(event.target.value)}>
          {viewOptions.map((kind) => (
            <option key={kind} value={kind}>
              {kind}
            </option>
          ))}
        </select>
        <select value={rootId} onChange={(event) => onRootChange(event.target.value)}>
          <option value="">Select a root…</option>
          {rootOptions.map((root) => (
            <option key={root.id} value={root.id}>
              {root.name}
            </option>
          ))}
        </select>
        {extraToolbarContent}
      </div>
      {error && <div className="diagram-error">{error}</div>}
      {diagnostics.map((diagnostic) => (
        <div key={diagnostic.code} className="diagram-error">
          {diagnostic.message}
        </div>
      ))}
      {!error && diagnostics.length === 0 && !graph && (
        <div className="diagram-placeholder">{!rootId ? 'Pick a root to view its diagram.' : loading ? 'Loading…' : null}</div>
      )}
      <GraphCanvas graph={graph} />
    </div>
  );
}
