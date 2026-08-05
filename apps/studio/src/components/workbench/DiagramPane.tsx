'use client';

import { useEffect, useState } from 'react';
import { GraphCanvas } from './GraphCanvas';
import {
  fetchProjectionGraph,
  fetchProjectionRoots,
  SUPPORTED_VIEWS,
  type ProjectionDiagnostic,
  type ProjectionGraph,
  type ProjectionRoot,
  type ProjectionView,
} from '@/lib/projection-client';

export function DiagramPane() {
  const [view, setView] = useState<ProjectionView>('Lifecycle');
  const [roots, setRoots] = useState<ProjectionRoot[]>([]);
  const [rootId, setRootId] = useState<string>('');
  const [graph, setGraph] = useState<ProjectionGraph | undefined>();
  const [diagnostics, setDiagnostics] = useState<ProjectionDiagnostic[]>([]);
  const [error, setError] = useState<string | undefined>();

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
      <GraphCanvas graph={graph} />
    </div>
  );
}
