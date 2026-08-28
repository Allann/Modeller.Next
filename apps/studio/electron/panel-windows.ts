// Detached Diagram/Generation "tool windows" (see WorkbenchShell.tsx). Both panels already fetch
// their own data straight from the local HTTP API, so detaching one is just opening a second,
// independent BrowserWindow pointed at a dedicated route on the same server — no state to
// synchronize between the main window and the detached one.
//
// `parent: mainWindow` gives these MDI-child-like behavior (grouped with the main window rather
// than a fully independent top-level window, and — same as WinForms MDI children — Electron closes
// them automatically if the main window closes, e.g. before before-quit's own explicit
// closeAllPanelWindows() runs).
import { BrowserWindow, screen, type NativeImage } from 'electron';
import { isBoundsVisible, loadPanelWindowState, savePanelWindowBounds } from './panel-window-state';

export type PanelKind = 'diagram' | 'generation';

const PANEL_ROUTES: Record<PanelKind, string> = {
  diagram: 'panels/diagram',
  generation: 'panels/generation',
};
const PANEL_TITLES: Record<PanelKind, string> = {
  diagram: 'Diagram — Modeller Studio',
  generation: 'Generated Files — Modeller Studio',
};
const DEFAULT_SIZE = { width: 1000, height: 700 };
// How long to wait after the last move/resize before persisting — avoids a disk write per pixel
// while dragging. Deliberately not tied to the 'close' event: a renderer-triggered window.close()
// (see DiagramPane.tsx/LocalGenerationPreview.tsx's Reattach button) fires BrowserWindow's 'closed'
// event but not 'close', leaving no reliable moment to read final bounds before the window is
// destroyed — continuous debounced saving sidesteps that, and is more robust anyway (it also
// captures the last position across a force-quit/crash, not just a clean close).
const SAVE_DEBOUNCE_MS = 400;

const openPanels = new Map<PanelKind, BrowserWindow>();

interface StartupPlacement {
  bounds: { x?: number; y?: number; width: number; height: number };
  maximized: boolean;
}

function resolveStartupPlacement(kind: PanelKind): StartupPlacement {
  const saved = loadPanelWindowState()[kind];
  if (!saved) return { bounds: DEFAULT_SIZE, maximized: false };
  const displayWorkAreas = screen.getAllDisplays().map((display) => display.workArea);
  if (!isBoundsVisible(saved, displayWorkAreas)) return { bounds: DEFAULT_SIZE, maximized: false };
  const { maximized, ...bounds } = saved;
  return { bounds, maximized };
}

// Tracks each window's last *normal* (non-maximized) bounds separately from whether it's currently
// maximized — BrowserWindow.getBounds() while maximized isn't a meaningful "restore to" size — and
// persists them on a short debounce after every move/resize/maximize/unmaximize.
function trackAndPersistBounds(kind: PanelKind, panelWindow: BrowserWindow): void {
  let lastNormalBounds = panelWindow.getBounds();
  let saveTimer: ReturnType<typeof setTimeout> | undefined;

  const scheduleSave = () => {
    clearTimeout(saveTimer);
    saveTimer = setTimeout(() => {
      savePanelWindowBounds(kind, { ...lastNormalBounds, maximized: panelWindow.isMaximized() });
    }, SAVE_DEBOUNCE_MS);
  };
  const onResizeOrMove = () => {
    if (!panelWindow.isMaximized()) lastNormalBounds = panelWindow.getBounds();
    scheduleSave();
  };

  panelWindow.on('resize', onResizeOrMove);
  panelWindow.on('move', onResizeOrMove);
  panelWindow.on('maximize', scheduleSave);
  panelWindow.on('unmaximize', onResizeOrMove);
}

export function openPanelWindow(kind: PanelKind, port: number, parentWindow: BrowserWindow, icon: NativeImage | undefined, onClosed: () => void): void {
  const existing = openPanels.get(kind);
  if (existing && !existing.isDestroyed()) {
    if (existing.isMinimized()) existing.restore();
    existing.focus();
    return;
  }

  const placement = resolveStartupPlacement(kind);
  const panelWindow = new BrowserWindow({
    ...placement.bounds,
    parent: parentWindow,
    // A starting title — page-title-updated (fired once the route's own metadata title loads)
    // overwrites this to the same value in practice (see panels/diagram/page.tsx), kept here so the
    // window isn't briefly titled "Electron" during that first load.
    title: PANEL_TITLES[kind],
    icon,
    webPreferences: { nodeIntegration: false, contextIsolation: true },
  });
  if (placement.maximized) panelWindow.maximize();

  openPanels.set(kind, panelWindow);
  trackAndPersistBounds(kind, panelWindow);
  panelWindow.on('closed', () => {
    openPanels.delete(kind);
    onClosed();
  });
  void panelWindow.loadURL(`http://localhost:${port}/${PANEL_ROUTES[kind]}`);
}

export function detachState(): { diagram: boolean; generation: boolean } {
  return { diagram: openPanels.has('diagram'), generation: openPanels.has('generation') };
}

export function closeAllPanelWindows(): void {
  for (const panelWindow of openPanels.values()) if (!panelWindow.isDestroyed()) panelWindow.close();
}

// "Reset Panel Window Positions" (menu.ts): un-maximizes and resizes any currently open panel
// windows back to the default, in addition to clearPanelWindowState() clearing what's persisted for
// future opens — so a reset is immediately visible, not just on the next detach.
export function resetOpenPanelWindowBounds(): void {
  for (const panelWindow of openPanels.values()) {
    if (panelWindow.isDestroyed()) continue;
    if (panelWindow.isMaximized()) panelWindow.unmaximize();
    panelWindow.setBounds({ ...DEFAULT_SIZE, x: panelWindow.getBounds().x, y: panelWindow.getBounds().y });
  }
}
