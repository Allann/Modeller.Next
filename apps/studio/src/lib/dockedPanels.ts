import type { PanelDetachState } from './electronBridge';

export type DockedRightPanelKey = 'diagram' | 'generation' | 'tabs';

/**
 * Which of Diagram/Generation are docked in the right-hand section of WorkbenchShell's layout,
 * and in what arrangement — isolated from the JSX that renders each so this can be unit-tested
 * without a DOM. A detached panel's slot is omitted entirely (not a placeholder), so the caller's
 * remaining panels reclaim its space.
 *
 * Wide layout keeps Diagram and Generation as separate docked panels. Narrow layout shares one
 * panel between them via a tab switcher ('tabs') — unless exactly one is docked, in which case
 * there is nothing to switch between, so that one renders directly instead of behind a tab bar.
 */
export function computeDockedRightPanelKeys(isWideForGeneration: boolean, detachedPanels: PanelDetachState): DockedRightPanelKey[] {
  if (isWideForGeneration) {
    const keys: DockedRightPanelKey[] = [];
    if (!detachedPanels.diagram) keys.push('diagram');
    if (!detachedPanels.generation) keys.push('generation');
    return keys;
  }
  if (!detachedPanels.diagram && !detachedPanels.generation) return ['tabs'];
  if (!detachedPanels.diagram) return ['diagram'];
  if (!detachedPanels.generation) return ['generation'];
  return [];
}
