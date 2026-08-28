export type FolderSlotMode = 'button' | 'input' | 'text';

// Which control the status bar's folder slot renders (see StatusBar.tsx) — isolated so this
// decision is unit-tested rather than only exercised live through the rendered component.
export function resolveFolderSlotMode(canSwitchWorkspace: boolean, hasElectronBridge: boolean): FolderSlotMode {
  if (!canSwitchWorkspace) return 'text';
  return hasElectronBridge ? 'button' : 'input';
}

// Mirrors VS Code's status bar convention ("1 error", "2 errors").
export function pluralize(count: number, singular: string, plural: string = `${singular}s`): string {
  return count === 1 ? singular : plural;
}
