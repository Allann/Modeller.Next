// Creates a plain monaco-editor instance for one document and wires it to
// Modeller.LanguageServer over the server's single shared /lsp WebSocket
// connection (see src/lib/lsp/session.ts), using the hand-rolled LSP client
// in src/lib/lsp/ rather than monaco-languageclient.
import * as monaco from 'monaco-editor';
import './monaco-worker';
import { registerModellerLanguages, languageIdForPath } from './monaco-languages';
import { watchMonacoTheme } from './monaco-theme';
import { openDocument } from './lsp/session';

export interface StudioEditorSession {
  editor: monaco.editor.IStandaloneCodeEditor;
  dispose: () => void;
}

// React's dev-mode Strict Mode double-invokes effects (mount -> cleanup ->
// mount, synchronously before any of this function's awaits resolve), so
// `isCancelled` is checked immediately after every await, before touching the
// DOM/model/socket — the standard pattern for making an async effect body
// safe under that double-invocation. Without it, both invocations would call
// monaco.editor.create() on the same container before either had disposed,
// producing "Element already has context attribute" and "Model is disposed!".
export async function startLanguageClient(
  container: HTMLElement,
  path: string,
  content: string,
  isCancelled: () => boolean,
): Promise<StudioEditorSession | undefined> {
  await registerModellerLanguages(monaco);
  watchMonacoTheme(monaco);
  if (isCancelled()) return undefined;

  const languageId = languageIdForPath(path);
  const uri = toModelUri(path);
  const model = monaco.editor.createModel(content, languageId, monaco.Uri.parse(uri));
  // The Modeller language server is the source of truth for suggestions; Monaco's own
  // buffer-scanning word suggestions would otherwise duplicate the same keywords/symbols
  // (undeduped, since Monaco merges suggestions across providers without deduping).
  const editor = monaco.editor.create(container, { model, automaticLayout: true, wordBasedSuggestions: 'off' });

  const attached = await openDocument(monaco, languageId, uri, model);
  if (isCancelled()) {
    attached.dispose();
    editor.dispose();
    model.dispose();
    return undefined;
  }

  return {
    editor,
    dispose: () => {
      attached.dispose();
      editor.dispose();
      model.dispose();
    },
  };
}

function toModelUri(path: string): string {
  return `file:///workspace/${path}`;
}
