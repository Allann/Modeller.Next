'use client';

import dynamic from 'next/dynamic';

// A detached Diagram tool window (see electron/panel-windows.ts) — the same self-contained
// DiagramPane the main WorkbenchShell docks, mounted alone with no ribbon/explorer/editor chrome.
const DiagramPane = dynamic(() => import('@/components/workbench/DiagramPane').then((mod) => mod.DiagramPane), { ssr: false });

export function DiagramPanelClient() {
  return (
    <main className="panel-window">
      <DiagramPane showDetach={false} />
    </main>
  );
}
