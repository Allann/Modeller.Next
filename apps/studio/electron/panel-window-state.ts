// Remembers each detached panel window's last normal (non-maximized) position/size and whether it
// was maximized, across both re-detaching within a session and restarting the app entirely — until
// the user either moves/resizes it again (overwriting the saved state) or resets it (see menu.ts's
// "Reset Panel Window Positions", which clears this file).
import { app } from 'electron';
import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import type { PanelKind } from './panel-windows';

export interface PanelWindowBounds {
  x: number;
  y: number;
  width: number;
  height: number;
  maximized: boolean;
}

type PanelWindowState = Partial<Record<PanelKind, PanelWindowBounds>>;

function statePath(): string {
  return path.join(app.getPath('userData'), 'panel-window-state.json');
}

export function loadPanelWindowState(): PanelWindowState {
  try {
    if (!existsSync(statePath())) return {};
    return JSON.parse(readFileSync(statePath(), 'utf-8')) as PanelWindowState;
  } catch {
    return {};
  }
}

export function savePanelWindowBounds(kind: PanelKind, bounds: PanelWindowBounds): void {
  const state = loadPanelWindowState();
  state[kind] = bounds;
  writeFileSync(statePath(), JSON.stringify(state, null, 2));
}

export function clearPanelWindowState(): void {
  writeFileSync(statePath(), JSON.stringify({}, null, 2));
}

/**
 * A saved position from a monitor that's no longer connected (unplugged, or a different machine's
 * saved state on a synced profile) would otherwise open an unreachable, invisible window. True if
 * `bounds`' center point falls within any of `displayWorkAreas`. Pure and Electron-independent so
 * it's unit-testable with plain data rather than requiring a real `screen` module.
 */
export function isBoundsVisible(
  bounds: Pick<PanelWindowBounds, 'x' | 'y' | 'width' | 'height'>,
  displayWorkAreas: readonly { x: number; y: number; width: number; height: number }[],
): boolean {
  const centerX = bounds.x + bounds.width / 2;
  const centerY = bounds.y + bounds.height / 2;
  return displayWorkAreas.some(
    (area) => centerX >= area.x && centerX < area.x + area.width && centerY >= area.y && centerY < area.y + area.height,
  );
}
