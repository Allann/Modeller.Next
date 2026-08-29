// Remembers the most recently opened local workspace folders, for the File > Open Recent menu
// (menu.ts) — mirrors VS Code's own recently-opened list. Most recent first; re-opening an
// already-listed folder moves it back to the top rather than duplicating it. Recorded from the
// renderer only after a workspace actually loads successfully (see WorkbenchShell.tsx's
// onLoadWorkspace), not merely picked in the folder dialog, so a mistaken or invalid pick never
// clutters the list.
import { app } from 'electron';
import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';

const MAX_RECENT = 10;

function statePath(): string {
  return path.join(app.getPath('userData'), 'recent-workspaces.json');
}

export function loadRecentWorkspaces(): string[] {
  try {
    if (!existsSync(statePath())) return [];
    const parsed = JSON.parse(readFileSync(statePath(), 'utf-8')) as unknown;
    return Array.isArray(parsed) ? parsed.filter((entry): entry is string => typeof entry === 'string') : [];
  } catch {
    return [];
  }
}

export function addRecentWorkspace(root: string): void {
  const deduped = loadRecentWorkspaces().filter((entry) => entry !== root);
  writeFileSync(statePath(), JSON.stringify([root, ...deduped].slice(0, MAX_RECENT), null, 2));
}

export function clearRecentWorkspaces(): void {
  writeFileSync(statePath(), JSON.stringify([], null, 2));
}
