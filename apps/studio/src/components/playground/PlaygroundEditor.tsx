'use client';

// A plain Monaco instance for the playground — no LSP: Modeller.Api exposes
// only a batch /v1/workspace/analyze endpoint (see
// docs/architecture/decisions/hosted-workspace-api.mdx), so there is no
// per-keystroke language server to bridge to. One model per document is kept
// alive for the whole session (not lazily per open tab, unlike local Studio's
// MonacoEditor) so diagnostics can be attached to any file regardless of
// which tab is currently active.
import { useEffect, useRef, useState } from 'react';
import * as monaco from 'monaco-editor';
import '@/lib/monaco-worker';
import { registerModellerLanguages, languageIdForPath } from '@/lib/monaco-languages';
import { watchMonacoTheme } from '@/lib/monaco-theme';
import type { ApiDiagnostic, WorkspaceDocumentDto } from '@/lib/playground/api-client';

function modelUri(path: string): string {
  return `file:///workspace/${path}`;
}

// Grouped by document path so callers don't have to; diagnostics without a
// source location (workspace-shape rejections, which the playground should
// never actually trigger since it always sends a well-formed request) are
// dropped here rather than attached to the wrong file.
export function applyDiagnosticMarkers(diagnostics: readonly ApiDiagnostic[]): void {
  const byPath = new Map<string, ApiDiagnostic[]>();
  for (const diagnostic of diagnostics) {
    if (!diagnostic.location) continue;
    const entries = byPath.get(diagnostic.location.document) ?? [];
    entries.push(diagnostic);
    byPath.set(diagnostic.location.document, entries);
  }

  for (const model of monaco.editor.getModels()) {
    const path = model.uri.path.replace(/^\/workspace\//, '');
    const entries = byPath.get(path) ?? [];
    monaco.editor.setModelMarkers(
      model,
      'modeller-api',
      entries.map((diagnostic) => {
        const location = diagnostic.location!;
        const startColumn = Math.max(location.column, 1);
        return {
          severity: monaco.MarkerSeverity.Error,
          message: diagnostic.message,
          code: diagnostic.code,
          startLineNumber: Math.max(location.line, 1),
          startColumn,
          endLineNumber: Math.max(location.line, 1),
          endColumn: startColumn + Math.max(location.length, 1),
        };
      }),
    );
  }
}

export function PlaygroundEditor({
  documents,
  activePath,
  onChange,
  navigationTarget,
  provideCompletions,
}: {
  documents: readonly WorkspaceDocumentDto[];
  activePath: string | undefined;
  onChange: (path: string, value: string) => void;
  navigationTarget?: { path: string; line: number; column: number; key: number };
  provideCompletions?: (path: string, line: number, prefix: string, signal: AbortSignal) => Promise<readonly { label: string; kind: string; detail: string }[]>;
}) {
  const containerRef = useRef<HTMLDivElement>(null);
  const editorRef = useRef<monaco.editor.IStandaloneCodeEditor | undefined>(undefined);
  const modelsRef = useRef<Map<string, monaco.editor.ITextModel>>(new Map());
  const onChangeRef = useRef(onChange);
  const provideCompletionsRef = useRef(provideCompletions);
  useEffect(() => {
    onChangeRef.current = onChange;
    provideCompletionsRef.current = provideCompletions;
  });
  const [ready, setReady] = useState(false);

  useEffect(() => {
    const container = containerRef.current;
    if (!container) return;
    const models = modelsRef.current;
    let cancelled = false;
    let completion: monaco.IDisposable | undefined;

    void registerModellerLanguages(monaco).then(() => {
      // See the isCancelled-after-every-await note in languageclient-setup.ts —
      // React Strict Mode double-invokes this effect before the await settles.
      if (cancelled) return;
      watchMonacoTheme(monaco);
      editorRef.current = monaco.editor.create(container, { automaticLayout: true, wordBasedSuggestions: 'off' });
      completion = monaco.languages.registerCompletionItemProvider('modeller-rml', {
        triggerCharacters: [' '],
        provideCompletionItems: async (model, position, _context, token) => {
          if (!provideCompletionsRef.current) return { suggestions: [] };
          const controller = new AbortController();
          token.onCancellationRequested(() => controller.abort());
          const word = model.getWordUntilPosition(position);
          const path = model.uri.path.replace(/^\/workspace\//, '');
          const items = await provideCompletionsRef.current(path, position.lineNumber, word.word, controller.signal);
          return { suggestions: items.map((item) => ({
            label: item.label, detail: item.detail, insertText: item.label,
            kind: item.kind === 'keyword' ? monaco.languages.CompletionItemKind.Keyword : monaco.languages.CompletionItemKind.Reference,
            range: { startLineNumber: position.lineNumber, endLineNumber: position.lineNumber, startColumn: word.startColumn, endColumn: word.endColumn },
          })) };
        },
      });
      setReady(true);
    });

    return () => {
      cancelled = true;
      completion?.dispose();
      editorRef.current?.dispose();
      editorRef.current = undefined;
      for (const model of models.values()) model.dispose();
      models.clear();
    };
  }, []);

  useEffect(() => {
    if (!ready) return;
    for (const document of documents) {
      if (modelsRef.current.has(document.path)) continue;
      const model = monaco.editor.createModel(document.content, languageIdForPath(document.path), monaco.Uri.parse(modelUri(document.path)));
      model.onDidChangeContent(() => onChangeRef.current(document.path, model.getValue()));
      modelsRef.current.set(document.path, model);
    }
    if (activePath) {
      const model = modelsRef.current.get(activePath);
      if (model && editorRef.current && editorRef.current.getModel() !== model) editorRef.current.setModel(model);
    }
  }, [ready, documents, activePath]);

  useEffect(() => {
    if (!ready || !navigationTarget || activePath !== navigationTarget.path) return;
    editorRef.current?.setPosition({ lineNumber: navigationTarget.line, column: navigationTarget.column });
    editorRef.current?.revealLineInCenter(navigationTarget.line);
    editorRef.current?.focus();
  }, [ready, activePath, navigationTarget]);

  return <div ref={containerRef} className="editor-container" />;
}
