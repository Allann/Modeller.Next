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
}: {
  documents: readonly WorkspaceDocumentDto[];
  activePath: string | undefined;
  onChange: (path: string, value: string) => void;
}) {
  const containerRef = useRef<HTMLDivElement>(null);
  const editorRef = useRef<monaco.editor.IStandaloneCodeEditor | undefined>(undefined);
  const modelsRef = useRef<Map<string, monaco.editor.ITextModel>>(new Map());
  const onChangeRef = useRef(onChange);
  useEffect(() => {
    onChangeRef.current = onChange;
  });
  const [ready, setReady] = useState(false);

  useEffect(() => {
    const container = containerRef.current;
    if (!container) return;
    let cancelled = false;

    void registerModellerLanguages(monaco).then(() => {
      // See the isCancelled-after-every-await note in languageclient-setup.ts —
      // React Strict Mode double-invokes this effect before the await settles.
      if (cancelled) return;
      watchMonacoTheme(monaco);
      editorRef.current = monaco.editor.create(container, { automaticLayout: true, wordBasedSuggestions: 'off' });
      setReady(true);
    });

    return () => {
      cancelled = true;
      editorRef.current?.dispose();
      editorRef.current = undefined;
      for (const model of modelsRef.current.values()) model.dispose();
      modelsRef.current.clear();
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

  return <div ref={containerRef} className="editor-container" />;
}
