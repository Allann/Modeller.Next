'use client';

import dynamic from 'next/dynamic';

// A detached Generation tool window (see electron/panel-windows.ts) — the same self-contained
// LocalGenerationPreview the main WorkbenchShell docks, mounted alone with no ribbon/explorer/editor
// chrome. Monaco (via GenerationPreview) touches `window` at module scope, so this stays client-only.
const LocalGenerationPreview = dynamic(
  () => import('@/components/workbench/LocalGenerationPreview').then((mod) => mod.LocalGenerationPreview),
  { ssr: false },
);

export function GenerationPanelClient() {
  return (
    <main className="panel-window">
      <LocalGenerationPreview showDetach={false} />
    </main>
  );
}
