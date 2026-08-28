'use client';

import { useEffect, useState } from 'react';
import { useViewRootSelection } from '@/lib/useViewRootSelection';
import { DiagramView } from './DiagramView';
import {
  fetchProjectionGraph,
  fetchProjectionRoots,
  SUPPORTED_VIEWS,
  type ProjectionDiagnostic,
  type ProjectionGraph,
  type ProjectionRoot,
  type ProjectionView,
} from '@/lib/projection-client';
import { getElectronBridge } from '@/lib/electronBridge';

export function DiagramPane({ showDetach = true }: { showDetach?: boolean }) {
  const { view, setView, rootId, setRootId } = useViewRootSelection<ProjectionView>('Lifecycle');
  const [roots, setRoots] = useState<ProjectionRoot[]>([]);
  const [graph, setGraph] = useState<ProjectionGraph | undefined>();
  const [diagnostics, setDiagnostics] = useState<ProjectionDiagnostic[]>([]);
  const [error, setError] = useState<string | undefined>();

  useEffect(() => {
    // react-hooks/set-state-in-effect flags the resets below, but this effect's job is the
    // fetch-on-view-change side effect itself — the resets just clear stale root list/graph/
    // diagnostic state before that fetch starts, same fetch-on-mount shape as
    // useInitiativeSession.ts. `rootId` itself is reset atomically by useViewRootSelection's
    // setView, not here — see that hook for why.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setRoots([]);
    setGraph(undefined);
    setDiagnostics([]);
    setError(undefined);
    let cancelled = false;
    void fetchProjectionRoots(view)
      .then((result) => {
        if (!cancelled) setRoots(result);
      })
      .catch((fetchError: unknown) => {
        if (!cancelled) setError(fetchError instanceof Error ? fetchError.message : 'Failed to load roots.');
      });
    return () => {
      cancelled = true;
    };
  }, [view]);

  useEffect(() => {
    if (!rootId) {
      // eslint-disable-next-line react-hooks/set-state-in-effect -- see rationale above
      setGraph(undefined);
      setDiagnostics([]);
      setError(undefined);
      return;
    }
    setError(undefined);
    setDiagnostics([]);
    let cancelled = false;
    void fetchProjectionGraph(view, rootId)
      .then((result) => {
        if (cancelled) return;
        setGraph(result.graph);
        setDiagnostics(result.diagnostics ?? []);
      })
      .catch((fetchError: unknown) => {
        if (!cancelled) setError(fetchError instanceof Error ? fetchError.message : 'Failed to load diagram.');
      });
    return () => {
      cancelled = true;
    };
  }, [view, rootId]);

  const bridge = showDetach ? getElectronBridge() : undefined;

  return (
    <DiagramView
      view={view}
      onViewChange={(next) => setView(next as ProjectionView)}
      viewOptions={SUPPORTED_VIEWS}
      rootId={rootId}
      onRootChange={setRootId}
      rootOptions={roots}
      error={error}
      diagnostics={diagnostics}
      graph={graph}
      loading={!graph}
      extraToolbarContent={
        bridge ? (
          <button type="button" className="panel-detach-btn" onClick={() => bridge.detachPanel('diagram')} title="Open in a separate window">
            Detach
          </button>
        ) : !showDetach ? (
          // This pane is itself the content of a detached panel window (see panels/diagram/page.tsx)
          // — closing it re-docks the panel in the main window (see panel-windows.ts's 'closed' handler).
          <button type="button" className="panel-detach-btn" onClick={() => window.close()} title="Return to the main window">
            Reattach
          </button>
        ) : null
      }
    />
  );
}
