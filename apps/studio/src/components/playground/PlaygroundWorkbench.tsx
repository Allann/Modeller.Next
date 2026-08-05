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
import { StatusBanner, type Notice } from './StatusBanner';
import { loadDraft, resetToExample, saveDraft, type PlaygroundDraft } from '@/lib/playground/session-store';
import {
  analyzeWorkspace,
  exportWorkspace,
  fetchSupportedViews,
  EPHEMERAL_IDENTITY,
  type ProjectionResponseDto,
  type RootSummaryDto,
} from '@/lib/playground/api-client';
import { decodeShareLink, encodeShareLink, type ShareDecodeResult } from '@/lib/playground/share-link';
import { buildWorkspaceZip, downloadWorkspaceZip } from '@/lib/playground/workspace-bundle';

const VIEW_KINDS = ['Lifecycle', 'RuleDecision'] as const;
type ViewKind = (typeof VIEW_KINDS)[number];
const ANALYZE_DEBOUNCE_MS = 500;

function shareDecodeErrorNotice(reason: Exclude<ShareDecodeResult, { ok: true }>['reason']): Notice {
  const text =
    reason === 'unsupported-version'
      ? "This share link was created by a newer version of the playground and can't be opened here."
      : reason === 'too-large'
        ? 'This share link is too large to open safely.'
        : "This share link couldn't be read — it may be corrupted or incomplete.";
  return { kind: 'error', text };
}

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
  const [status, setStatus] = useState<'idle' | 'analyzing' | 'error'>('idle');
  const [errorMessage, setErrorMessage] = useState<string | undefined>();
  const [uiNotice, setUiNotice] = useState<Notice | undefined>();
  const [shareUrl, setShareUrl] = useState<string | undefined>();
  const [downloading, setDownloading] = useState(false);
  const debounceRef = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);
  const requestIdRef = useRef(0);

  const loadSharedDraft = useCallback((shared: PlaygroundDraft) => {
    setDraft(shared);
    saveDraft(shared);
    setDraftRevision((revision) => revision + 1);
    setOpenPaths(shared.documents[0] ? [shared.documents[0].path] : []);
    setActivePath(shared.documents[0]?.path);
    setRootId('');
    setProjection(undefined);
  }, []);

  // A share link (issue #73) is consumed once, on first load, before anything else touches the
  // draft — it always wins over whatever loadDraft() already put in state above. The fragment is
  // never sent to any server; it's read straight out of the browser's own address bar.
  useEffect(() => {
    if (!window.location.hash) return;
    void decodeShareLink(window.location.hash).then((result) => {
      if (!result) return; // present but not a share fragment — leave the loaded draft alone
      if (!result.ok) {
        setUiNotice(shareDecodeErrorNotice(result.reason));
        return;
      }
      loadSharedDraft({ documents: result.documents, configuration: result.configuration, identity: EPHEMERAL_IDENTITY });
      window.history.replaceState(null, '', window.location.pathname + window.location.search);
    });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

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
      const response = await analyzeWorkspace(draft.documents, draft.identity, draft.configuration, projections);
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
  }, [draft.documents, draft.identity, draft.configuration, view, rootId]);

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
    setUiNotice(undefined);
    setShareUrl(undefined);
    loadSharedDraft(resetToExample());
  };

  const onShare = async () => {
    setUiNotice(undefined);
    const result = await encodeShareLink(draft.documents, draft.configuration);
    if (!result.ok) {
      setShareUrl(undefined);
      setUiNotice({ kind: 'error', text: 'This workspace is too large for a share link — use Download workspace instead.' });
      return;
    }
    setShareUrl(result.url);
  };

  const onDownload = async () => {
    setUiNotice(undefined);
    setDownloading(true);
    try {
      const response = await exportWorkspace(draft.documents, draft.identity, draft.configuration);
      if (!response.identity) {
        setUiNotice({
          kind: 'error',
          text: response.diagnostics[0]?.message ?? 'Could not export this workspace — fix the diagnostics above and try again.',
        });
        return;
      }
      const nextDraft: PlaygroundDraft = { documents: response.documents, configuration: draft.configuration, identity: response.identity };
      setDraft(nextDraft);
      saveDraft(nextDraft);
      setDraftRevision((revision) => revision + 1); // documents now carry embedded "# @id=" identities — remount the editor to show them
      downloadWorkspaceZip(buildWorkspaceZip(response.documents, response.identity, draft.configuration));
      setUiNotice({ kind: 'info', text: 'Workspace downloaded. Documents now carry durable identities (the "# @id=" comments) — repeat downloads reuse them.' });
    } catch (error) {
      setUiNotice({ kind: 'error', text: error instanceof Error ? error.message : 'Failed to download the workspace.' });
    } finally {
      setDownloading(false);
    }
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
  const notice: Notice | undefined =
    uiNotice ??
    (status === 'analyzing'
      ? { kind: 'analyzing', text: 'Analyzing…' }
      : status === 'error'
        ? {
            kind: 'error',
            text: `Couldn't reach the analysis service${errorMessage ? ` (${errorMessage})` : ''}. Your draft is unaffected — it will retry on your next edit.`,
          }
        : undefined);

  return (
    <div className="shell">
      <div className="ribbon">
        <div className="brand">
          <span className="mark">M</span> Modeller Playground
        </div>
        <div className="playground-actions">
          <button className="playground-share-btn" onClick={() => void onShare()}>
            Share
          </button>
          <button className="playground-download-btn" onClick={() => void onDownload()} disabled={downloading}>
            {downloading ? 'Downloading…' : 'Download workspace'}
          </button>
          <button className="playground-reset-btn" onClick={onReset}>
            Reset example
          </button>
        </div>
      </div>
      {shareUrl && (
        <div className="playground-share">
          <input readOnly value={shareUrl} aria-label="Share link" onFocus={(event) => event.currentTarget.select()} />
          <button className="playground-share-btn" onClick={() => void navigator.clipboard?.writeText(shareUrl).catch(() => undefined)}>
            Copy
          </button>
          <button className="playground-share-btn" aria-label="Dismiss share link" onClick={() => setShareUrl(undefined)}>
            ×
          </button>
        </div>
      )}
      <StatusBanner notice={notice} />
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
        This is a browser draft, not a durable local workspace — it lives only in this tab until you download it.{' '}
        <a href="https://modeller.website/privacy">Privacy</a>
      </p>
    </div>
  );
}
