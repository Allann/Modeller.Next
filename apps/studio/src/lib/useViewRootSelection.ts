import { useCallback, useState } from 'react';

// Shared by DiagramPane (local Studio) and PlaygroundWorkbench: both drive a "view kind" + "root
// within that view" pair, and both need the root cleared the instant the view changes — a root id
// from the previous view is never valid for the new one. Doing the reset here, in the same setter
// call as the view change, means there's no render in between where `view` has updated but the
// stale `rootId` hasn't — the exact gap that let a stale root slip through as a real fetch in
// Studio's diagram pane. Don't move this reset into a `useEffect` keyed on `view`; that reopens
// the gap this hook exists to close.
export function useViewRootSelection<TView extends string>(initialView: TView) {
  const [view, setViewState] = useState<TView>(initialView);
  const [rootId, setRootId] = useState('');

  const setView = useCallback((next: TView) => {
    setViewState(next);
    setRootId('');
  }, []);

  return { view, setView, rootId, setRootId } as const;
}
