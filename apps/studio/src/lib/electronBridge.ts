// Thin, optional wrapper around window.modeller (exposed by electron/preload.ts). Local Studio's
// UI also runs directly in a browser tab via `npm run dev`/`npm start` (no Electron) — every call
// site here must degrade to "this feature just isn't available" rather than throw, so the same
// WorkbenchShell works in both contexts.
export type PanelKind = 'diagram' | 'generation';
export interface PanelDetachState {
  diagram: boolean;
  generation: boolean;
}

interface ModellerBridge {
  onOpenFolder(callback: (path: string) => void): () => void;
  requestOpenFolder(): void;
  recordRecentWorkspace(root: string): void;
  detachPanel(kind: PanelKind): void;
  onPanelDetachState(callback: (state: PanelDetachState) => void): () => void;
  requestPanelDetachState(): void;
}

declare global {
  interface Window {
    modeller?: ModellerBridge;
  }
}

export function getElectronBridge(): ModellerBridge | undefined {
  return typeof window !== 'undefined' ? window.modeller : undefined;
}
