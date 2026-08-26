'use client';

// The generation preview panel (issue #135): a read-only view of what `POST /v1/workspace/generate`
// would write, if it wrote anything — it never does (see PlaygroundWorkbench's read-only note).
// Reuses the same Monaco setup calls as PlaygroundEditor (worker registration, theme, language
// registration) rather than duplicating them, but drives Monaco's *diff* editor instead of a plain
// one, since nothing else in this app uses createDiffEditor yet.
import { useEffect, useRef, useState } from 'react';
import * as monaco from 'monaco-editor';
import '@/lib/monaco-worker';
import { registerModellerLanguages, languageIdForPath } from '@/lib/monaco-languages';
import { watchMonacoTheme } from '@/lib/monaco-theme';
import type { ApiDiagnostic, GeneratedArtifactDto } from '@/lib/playground/api-client';

export interface GenerationPreviewProps {
  artifacts: readonly GeneratedArtifactDto[];
  previousContentByPath: ReadonlyMap<string, string>;
  status: 'idle' | 'generating' | 'error';
  diagnostics: readonly ApiDiagnostic[];
  errorMessage?: string;
}

function diffModelUri(side: 'original' | 'modified', path: string, sequence: number): monaco.Uri {
  return monaco.Uri.parse(`generation-preview:///${side}/${sequence}/${encodeURIComponent(path)}`);
}

export function GenerationPreview({ artifacts, previousContentByPath, status, diagnostics, errorMessage }: GenerationPreviewProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const diffEditorRef = useRef<monaco.editor.IStandaloneDiffEditor | undefined>(undefined);
  const originalModelRef = useRef<monaco.editor.ITextModel | undefined>(undefined);
  const modifiedModelRef = useRef<monaco.editor.ITextModel | undefined>(undefined);
  const sequenceRef = useRef(0);
  const [ready, setReady] = useState(false);
  const [selectedPath, setSelectedPath] = useState<string | undefined>(artifacts[0]?.path);

  useEffect(() => {
    const container = containerRef.current;
    if (!container) return;
    let cancelled = false;

    void registerModellerLanguages(monaco).then(() => {
      // See PlaygroundEditor's isCancelled-after-every-await note: React Strict Mode
      // double-invokes this effect before the await settles.
      if (cancelled) return;
      watchMonacoTheme(monaco);
      diffEditorRef.current = monaco.editor.createDiffEditor(container, {
        automaticLayout: true,
        readOnly: true,
        renderSideBySide: true,
        originalEditable: false,
      });
      setReady(true);
    });

    return () => {
      cancelled = true;
      diffEditorRef.current?.dispose();
      diffEditorRef.current = undefined;
      originalModelRef.current?.dispose();
      originalModelRef.current = undefined;
      modifiedModelRef.current?.dispose();
      modifiedModelRef.current = undefined;
    };
  }, []);

  // The selected path must always name a currently-available artifact. Rather than syncing that
  // back into state with an effect (which would cascade an extra render), it's derived directly:
  // `selectedPath` state only ever holds an explicit user choice, and falls back to the first
  // artifact whenever that choice isn't (or is no longer) in the list — including the very first
  // render, before any explicit choice has been made.
  const effectiveSelectedPath = selectedPath && artifacts.some((artifact) => artifact.path === selectedPath) ? selectedPath : artifacts[0]?.path;

  useEffect(() => {
    if (!ready || !diffEditorRef.current) return;
    // The diff editor must be pointed at the new (or null) model *before* the old ones are
    // disposed — disposing first leaves it holding disposed models until setModel runs, which
    // Monaco logs as "TextModel got disposed before DiffEditorWidget model got reset".
    const previousOriginal = originalModelRef.current;
    const previousModified = modifiedModelRef.current;
    const artifact = artifacts.find((candidate) => candidate.path === effectiveSelectedPath);
    if (!artifact) {
      diffEditorRef.current.setModel(null);
      originalModelRef.current = undefined;
      modifiedModelRef.current = undefined;
    } else {
      // On the very first render of a path there is no previous version yet — an empty original
      // reads the whole content as an add, which is the clearest rendering of "nothing existed
      // before this".
      const previousContent = previousContentByPath.get(artifact.path) ?? '';
      const language = languageIdForPath(artifact.path);
      const sequence = ++sequenceRef.current;
      const original = monaco.editor.createModel(previousContent, language, diffModelUri('original', artifact.path, sequence));
      const modified = monaco.editor.createModel(artifact.content, language, diffModelUri('modified', artifact.path, sequence));
      originalModelRef.current = original;
      modifiedModelRef.current = modified;
      diffEditorRef.current.setModel({ original, modified });
    }
    previousOriginal?.dispose();
    previousModified?.dispose();
  }, [ready, effectiveSelectedPath, artifacts, previousContentByPath]);

  const selectedArtifact = artifacts.find((artifact) => artifact.path === effectiveSelectedPath);

  return (
    <div className="generation-preview-pane">
      <div className="generation-preview-toolbar diagram-toolbar">
        <select
          aria-label="Generated file"
          value={effectiveSelectedPath ?? ''}
          onChange={(event) => setSelectedPath(event.target.value || undefined)}
          disabled={artifacts.length === 0}
        >
          {artifacts.length === 0 && <option value="">No generated files yet</option>}
          {artifacts.map((artifact) => (
            <option key={artifact.path} value={artifact.path}>
              {artifact.path}
            </option>
          ))}
        </select>
      </div>
      {selectedArtifact && (
        <div className="generation-preview-meta">
          <span>{selectedArtifact.owner || 'workspace'}</span>
          <span>{selectedArtifact.packId} / {selectedArtifact.templateId}</span>
        </div>
      )}
      {status === 'error' && (
        <div className="diagram-error">{errorMessage ?? 'Failed to generate a preview.'}</div>
      )}
      {diagnostics.map((diagnostic, index) => (
        <div key={`${diagnostic.code}-${index}`} className="diagram-error">
          {diagnostic.message}
        </div>
      ))}
      {artifacts.length === 0 && status !== 'error' && diagnostics.length === 0 && (
        <div className="diagram-placeholder">{status === 'generating' ? 'Generating…' : 'No generated files yet.'}</div>
      )}
      <div ref={containerRef} className="generation-preview-editor" />
    </div>
  );
}
