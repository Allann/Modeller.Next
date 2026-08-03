// Monaco's theme is global (monaco.editor.setTheme affects every editor
// instance at once, not per-instance), so this follows the system
// prefers-color-scheme the same way the rest of the app's CSS does — see
// src/app/globals.css and the DiagramPane/select theming — rather than each
// MonacoEditor instance trying to own its own theme.
import type * as Monaco from 'monaco-editor';

let watching = false;

function preferredTheme(): 'vs-dark' | 'vs' {
  return typeof window !== 'undefined' && window.matchMedia('(prefers-color-scheme: dark)').matches ? 'vs-dark' : 'vs';
}

export function watchMonacoTheme(monaco: typeof Monaco): void {
  monaco.editor.setTheme(preferredTheme());
  if (watching) return;
  watching = true;
  window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', () => {
    monaco.editor.setTheme(preferredTheme());
  });
}
