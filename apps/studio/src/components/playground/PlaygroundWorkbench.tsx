'use client';

import { useCallback, useEffect, useRef, useState } from 'react';
import { Group, Panel, Separator } from 'react-resizable-panels';
import { buildTree } from '@/lib/tree';
import { Explorer } from '@/components/workbench/Explorer';
import { EditorTabs } from '@/components/workbench/EditorTabs';
import { ProblemsPanel } from '@/components/workbench/ProblemsPanel';
import { GraphCanvas } from '@/components/workbench/GraphCanvas';
import '@/components/workbench/workbench.css';
import './playground.css';
import { PlaygroundEditor, applyDiagnosticMarkers } from './PlaygroundEditor';
import { StatusBanner, type AnalysisStatus } from './StatusBanner';
import { loadDraft, resetToExample, saveDraft, type PlaygroundDraft } from '@/lib/playground/session-store';
import { analyzeWorkspace, fetchSupportedViews, type ProjectionResponseDto, type RootSummaryDto } from '@/lib/playground/api-client';

const VIEW_KINDS = ['Lifecycle', 'RuleDecision'] as const;
type ViewKind = (typeof VIEW_KINDS)[number];
const ANALYZE_DEBOUNCE_MS = 500;

export function PlaygroundWorkbench() {
  const [draft, setDraft] = useState<PlaygroundDraft>(() => loadDraft());
  const [draftRevision, setDraftRevision] = useState(0);
  const [activePath, setActivePath] = useState<string | undefined>(() => draft.documents[0]?.path);
  const [openPaths, setOpenPaths] = useState<string[]>(() => (draft.documents[0] ? [draft.documents[0].path] : []));
  const [view, setView] = useState<ViewKind>('Lifecycle');
  const [rootId, setRootId] = useState('');
  const [roots, setRoots] = useState<RootSummaryDto[]>([]);
  const [projection, setProjection] = useState<ProjectionResponseDto | undefined>();
  const [supportedViews, setSupportedViews] = useState<string[]>([]);
  const [status, setStatus] = useState<AnalysisStatus>('idle');
  const [errorMessage, setErrorMessage] = useState<string | undefined>();
  const debounceRef = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);
  const requestIdRef = useRef(0);

  useEffect(() => {
    void fetchSupportedViews()
      .then(setSupportedViews)
      .catch(() => undefined); // best-effort: the view/root selectors just fall back to every locally-known kind
  }, []);

  const runAnalysis = useCallback(async () => {
    const requestId = ++requestIdRef.current;
    setStatus('analyzing');
    const projections = rootId ? [{ id: 'active', kind: view, roots: [rootId] }] : [];
    try {
      const response = await analyzeWorkspace(draft.documents, draft.configuration, projections);
      if (requestId !== requestIdRef.current) return; // superseded by a newer edit
      setRoots(response.roots);
      setProjection(response.projections[0]);
      applyDiagnosticMarkers(response.diagnostics);
      setStatus('idle');
      setErrorMessage(undefined);
    } catch (error) {
      if (requestId !== requestIdRef.current) return;
      setStatus('error');
      setErrorMessage(error instanceof Error ? error.message : 'Failed to analyze the workspace.');
    }
  }, [draft.documents, draft.configuration, view, rootId]);

  useEffect(() => {
    clearTimeout(debounceRef.current);
    debounceRef.current = setTimeout(() => void runAnalysis(), ANALYZE_DEBOUNCE_MS);
    return () => clearTimeout(debounceRef.current);
  }, [runAnalysis]);

  const onDocumentChange = (path: string, value: string) => {
    setDraft((previous) => {
      const next: PlaygroundDraft = {
        ...previous,
        documents: previous.documents.map((document) => (document.path === path ? { ...document, content: value } : document)),
      };
      saveDraft(next);
      return next;
    });
  };

  const onReset = () => {
    const fresh = resetToExample();
    setDraft(fresh);
    setDraftRevision((revision) => revision + 1);
    setOpenPaths(fresh.documents[0] ? [fresh.documents[0].path] : []);
    setActivePath(fresh.documents[0]?.path);
    setRootId('');
    setProjection(undefined);
  };

  const openDocument = (path: string) => {
    setActivePath(path);
    setOpenPaths((previous) => (previous.includes(path) ? previous : [...previous, path]));
  };

  const closeDocument = (path: string) => {
    const remaining = openPaths.filter((openPath) => openPath !== path);
    setOpenPaths(remaining);
    if (activePath === path) setActivePath(remaining.at(-1));
  };

  const navigateToProblem = (path: string) => {
    if (draft.documents.some((document) => document.path === path)) openDocument(path);
  };

  const tree = buildTree(draft.documents.map((document) => document.path));
  const availableViews = VIEW_KINDS.filter((kind) => supportedViews.length === 0 || supportedViews.includes(kind));

  return (
    <div className="shell">
      <div className="ribbon">
        <div className="brand">
          <span className="mark">M</span> Modeller Playground
        </div>
        <button className="playground-reset-btn" onClick={onReset}>
          Reset example
        </button>
      </div>
      <StatusBanner status={status} errorMessage={errorMessage} />
      <Group orientation="horizontal" className="panel-group">
        <Panel defaultSize="20" minSize="12">
          <div className="explorer">
            <Explorer nodes={tree} activePath={activePath} onOpenDocument={openDocument} />
          </div>
        </Panel>
        <Separator className="resize-handle" />
        <Panel defaultSize="55" minSize="25">
          <div className="center">
            <EditorTabs openPaths={openPaths} activePath={activePath} onSelect={setActivePath} onClose={closeDocument} />
            <PlaygroundEditor key={draftRevision} documents={draft.documents} activePath={activePath} onChange={onDocumentChange} />
            <ProblemsPanel onNavigate={navigateToProblem} />
          </div>
        </Panel>
        <Separator className="resize-handle" />
        <Panel defaultSize="25" minSize="15">
          <div className="diagram-pane">
            <div className="diagram-toolbar">
              <select
                value={view}
                onChange={(event) => {
                  setView(event.target.value as ViewKind);
                  setRootId('');
                }}
              >
                {availableViews.map((kind) => (
                  <option key={kind} value={kind}>
                    {kind}
                  </option>
                ))}
              </select>
              <select value={rootId} onChange={(event) => setRootId(event.target.value)}>
                <option value="">Select a root…</option>
                {roots
                  .filter((root) => root.kind === view)
                  .map((root) => (
                    <option key={root.id} value={root.id}>
                      {root.name}
                    </option>
                  ))}
              </select>
            </div>
            {projection && !projection.succeeded && projection.diagnostics.map((diagnostic) => (
              <div key={diagnostic.code} className="diagram-error">
                {diagnostic.message}
              </div>
            ))}
            {!rootId && <div className="diagram-placeholder">Pick a root to view its diagram.</div>}
            {rootId && !projection?.graph && <div className="diagram-placeholder">Loading…</div>}
            <GraphCanvas graph={projection?.graph} />
          </div>
        </Panel>
      </Group>
      <p className="playground-explainer">
        This is a browser draft, not a durable local workspace — it lives only in this tab until you download it.
      </p>
    </div>
  );
}
