// Creates a plain monaco-editor instance for one document and wires it to
// Modeller.LanguageServer over the server's /lsp WebSocket bridge, using the
// hand-rolled LSP client in src/lib/lsp/ rather than monaco-languageclient —
// see the KNOWN PHASE-1 SIMPLIFICATION note below for the one real limitation
// of that choice.
//
// KNOWN PHASE-1 SIMPLIFICATION: each open document gets its own editor/model/
// LSP connection/LanguageServer process (one per mounted MonacoEditor), rather
// than one connection per browser session with multiple documents
// multiplexed onto it via didOpen. That's a deviation from decision #52's
// "one process per session" intent — correct but wasteful for multi-tab use.
// Follow-up: keep one LspConnection alive per session and attach additional
// models to it instead of opening a new WebSocket per tab.
import * as monaco from 'monaco-editor';
import { registerModellerLanguages, languageIdForPath } from './monaco-languages';
import { watchMonacoTheme } from './monaco-theme';
import { LspConnection } from './lsp/protocol';
import { attachLanguageServer } from './lsp/monaco-bridge';

export interface StudioEditorSession {
  editor: monaco.editor.IStandaloneCodeEditor;
  dispose: () => void;
}

// Runs Monaco's editor services (tokenization, diff, etc.) on their own
// worker thread instead of the main thread. The previous attempt at this used
// the specifier 'monaco-editor/esm/vs/editor/editor.worker.js', which looked
// right (it's the real on-disk path) but doesn't match monaco-editor's own
// package.json "exports" map (`"./*.js": "./esm/vs/*.js"` — the esm/vs prefix
// is added BY the mapping, not part of the specifier), so it 404s under any
// exports-map-respecting resolver, Turbopack included. Dropping the redundant
// prefix resolves correctly. This app only uses the base editor services plus
// custom TextMate-backed languages (see monaco-languages.ts), not any of
// Monaco's built-in per-language workers (TS/JSON/CSS/HTML), so a single
// worker for every label is sufficient — no per-label dispatch needed.
self.MonacoEnvironment = {
  getWorker() {
    return new Worker(new URL('monaco-editor/editor/editor.worker.js', import.meta.url), { type: 'module' });
  },
};

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

  const connection = new LspConnection(lspSocketUrl());
  const attached = await attachLanguageServer({ monaco, connection, languageId, uri, model });
  if (isCancelled()) {
    attached.dispose();
    connection.close();
    editor.dispose();
    model.dispose();
    return undefined;
  }

  return {
    editor,
    dispose: () => {
      attached.dispose();
      connection.close();
      editor.dispose();
      model.dispose();
    },
  };
}

function toModelUri(path: string): string {
  return `file:///workspace/${path}`;
}

function lspSocketUrl(): string {
  const { protocol, host } = window.location;
  const wsProtocol = protocol === 'https:' ? 'wss:' : 'ws:';
  return `${wsProtocol}//${host}/lsp`;
}
