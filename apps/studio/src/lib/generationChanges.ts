export interface GenerationChange {
  path: string;
  status: string;
}

// A written file (status 'create' or 'change'; 'unchanged'/'conflict'/'stale'/'remove' all leave
// the file as it already was) — used by LocalGenerationPreview.tsx to feed StatusBar.tsx's
// "N diffs" count. Kept in its own module, separate from LocalGenerationPreview.tsx, so it can be
// unit-tested under plain Node — that component transitively imports monaco-editor, which touches
// browser globals (AMD's `define`) at module load and crashes outside a real DOM.
export function countChangedArtifacts(changes: readonly GenerationChange[]): number {
  return changes.filter((change) => change.status === 'create' || change.status === 'change').length;
}
