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
  completeWorkspace,
  exportWorkspace,
  fetchSupportedViews,
  EPHEMERAL_IDENTITY,
  type ProjectionResponseDto,
  type RootSummaryDto,
  type SemanticOutlineItemDto,
  type SemanticCountDto,
} from '@/lib/playground/api-client';
import { decodeShareLink, encodeShareLink, type ShareDecodeResult } from '@/lib/playground/share-link';
import { buildWorkspaceZip, downloadWorkspaceZip } from '@/lib/playground/workspace-bundle';
import { capture } from '@/lib/productAnalytics';

const VIEW_KINDS = ['BehaviourMap', 'Lifecycle', 'CausalityAndEventFlow', 'ContextMap', 'Structural', 'RuleDecision'] as const;
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

function kindLabel(kind: string): string {
  return kind.replace(/([a-z])([A-Z])/g, '$1 $2').toLowerCase();
}

function countLabel(item: SemanticCountDto): string {
  const kind = kindLabel(item.kind);
  const noun = item.count === 1 ? kind : kind.endsWith('y') ? `${kind.slice(0, -1)}ies` : `${kind}s`;
  return `${item.count} ${noun}`;
}

const MODEL_GROUPS: ReadonlyArray<{ label: string; kinds: readonly string[] }> = [
  { label: 'Entities', kinds: ['Entity'] },
  { label: 'Enumerations', kinds: ['Enumeration'] },
  { label: 'Facts', kinds: ['Fact'] },
  { label: 'Rules', kinds: ['Rule'] },
  { label: 'Decisions', kinds: ['Decision'] },
  { label: 'Behaviours', kinds: ['Behaviour'] },
];

export function PlaygroundWorkbench() {
  const [draft, setDraft] = useState<PlaygroundDraft>(() => loadDraft());
  const [draftRevision, setDraftRevision] = useState(0);
  const [activePath, setActivePath] = useState<string | undefined>(() => draft.documents[0]?.path);
  const [openPaths, setOpenPaths] = useState<string[]>(() => (draft.documents[0] ? [draft.documents[0].path] : []));
  const [view, setView] = useState<ViewKind>('Lifecycle');
  const [rootId, setRootId] = useState('');
  const [roots, setRoots] = useState<RootSummaryDto[]>([]);
  const [outline, setOutline] = useState<SemanticOutlineItemDto[]>([]);
  const [summary, setSummary] = useState<SemanticCountDto[]>([]);
  const [navigationTarget, setNavigationTarget] = useState<{ path: string; line: number; column: number; key: number }>();
  const [projection, setProjection] = useState<ProjectionResponseDto | undefined>();
  const [supportedViews, setSupportedViews] = useState<string[]>([]);
  const [status, setStatus] = useState<'idle' | 'analyzing' | 'error'>('idle');
  const [diagnosticCount, setDiagnosticCount] = useState(0);
  const [errorMessage, setErrorMessage] = useState<string | undefined>();
  const [uiNotice, setUiNotice] = useState<Notice | undefined>();
  const [shareUrl, setShareUrl] = useState<string | undefined>();
  const [downloading, setDownloading] = useState(false);
  const debounceRef = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);
  const requestIdRef = useRef(0);
  const firstEditCapturedRef = useRef(false);
  const navigationKeyRef = useRef(0);

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
      setOutline(response.outline ?? []);
      setSummary(response.summary ?? []);
      if (!rootId) setRootId(response.roots.find((root) => root.kind === view)?.id ?? '');
      if (draft.identity.kind === 'ephemeral' && response.identity) {
        setDraft((previous) => {
          const next = { ...previous, identity: response.identity! };
          saveDraft(next);
          return next;
        });
      }
      setProjection(response.projections[0]);
      applyDiagnosticMarkers(response.diagnostics);
      setDiagnosticCount(response.diagnostics.length);
      setStatus('idle');
      setErrorMessage(undefined);
      capture('analysis_completed', { outcome: 'succeeded' });
    } catch (error) {
      if (requestId !== requestIdRef.current) return;
      setStatus('error');
      setErrorMessage(error instanceof Error ? error.message : 'Failed to analyze the workspace.');
      capture('analysis_completed', { outcome: 'failed' });
    }
  }, [draft.documents, draft.identity, draft.configuration, view, rootId]);

  const provideCompletions = useCallback((path: string, line: number, column: number, signal: AbortSignal) =>
    completeWorkspace(draft.documents, draft.identity, draft.configuration, path, line, column, signal),
  [draft.documents, draft.identity, draft.configuration]);

  useEffect(() => {
    clearTimeout(debounceRef.current);
    debounceRef.current = setTimeout(() => void runAnalysis(), ANALYZE_DEBOUNCE_MS);
    return () => clearTimeout(debounceRef.current);
  }, [runAnalysis]);

  const onDocumentChange = (path: string, value: string) => {
    if (!firstEditCapturedRef.current) {
      firstEditCapturedRef.current = true;
      capture('first_edit_made');
      capture('meaningful_use_started');
    }
    setDraft((previous) => {
      const next: PlaygroundDraft = {
        ...previous,
        identity: EPHEMERAL_IDENTITY,
        documents: previous.documents.map((document) => (document.path === path ? { ...document, content: value } : document)),
      };
      saveDraft(next);
      return next;
    });
    setRootId('');
    setProjection(undefined);
  };

  const onReset = () => {
    capture('example_loaded');
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
      capture('workspace_downloaded');
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

  const navigateToConcept = (item: SemanticOutlineItemDto) => {
    openDocument(item.location.document);
    navigationKeyRef.current += 1;
    setNavigationTarget({ path: item.location.document, line: item.location.line, column: item.location.column, key: navigationKeyRef.current });
  };

  const tree = buildTree(draft.documents.map((document) => document.path));
  const availableViews = VIEW_KINDS.filter((kind) => supportedViews.length === 0 || supportedViews.includes(kind));
  const analysisStatus: Notice =
    status === 'analyzing'
      ? { kind: 'analyzing', text: 'Analysing…' }
      : status === 'error'
        ? { kind: 'error', text: 'Analysis failed' }
        : diagnosticCount > 0
          ? { kind: 'error', text: `${diagnosticCount} ${diagnosticCount === 1 ? 'problem' : 'problems'}` }
          : { kind: 'info', text: 'Ready' };
  const messageNotice: Notice | undefined =
    uiNotice ??
    (status === 'error'
      ? {
          kind: 'error',
          text: `Couldn't reach the analysis service${errorMessage ? ` (${errorMessage})` : ''}. Your draft is unaffected — it will retry on your next edit.`,
        }
      : undefined);
  const renderConcept = (item: SemanticOutlineItemDto, depth = 0): React.ReactNode => (
    <div key={item.id} className="model-outline-group">
      <button style={{ paddingLeft: `${depth}rem` }} onClick={() => navigateToConcept(item)}>{kindLabel(item.kind)} {item.name}</button>
      {outline.filter((child) => child.ownerId === item.id).map((child) => renderConcept(child, depth + 1))}
    </div>
  );

  return (
    <div className="shell playground-shell">
      <div className="ribbon">
        <div className="brand">
          <span className="mark">M</span> Modeller Playground
        </div>
        <div className="playground-actions">
          <a className="playground-docs-link" href="https://modeller.wiki/docs/reference/readable-modelling-language" target="_blank" rel="noreferrer">
            RML syntax reference
          </a>
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
      <div className="playground-share-slot">
        {shareUrl && (
          <div className="playground-share">
            <input readOnly value={shareUrl} aria-label="Share link" onFocus={(event) => event.currentTarget.select()} />
            <button className="playground-share-btn" onClick={() => { capture('share_link_copied'); void navigator.clipboard?.writeText(shareUrl).catch(() => undefined); }}>
              Copy
            </button>
            <button className="playground-share-btn" aria-label="Dismiss share link" onClick={() => setShareUrl(undefined)}>
              ×
            </button>
          </div>
        )}
      </div>
      <Group orientation="horizontal" className="panel-group">
        <Panel defaultSize="20" minSize="12">
          <div className="explorer">
            <section className="file-explorer" aria-label="Files">
              <h2>Files</h2>
              <Explorer nodes={tree} activePath={activePath} onOpenDocument={openDocument} />
            </section>
            <div className="model-outline" aria-label="Model explorer">
              <h2>Model</h2>
              {status === 'idle' && diagnosticCount === 0 && summary.length > 0 && (
                <p className="model-summary" aria-label={`Valid model: ${summary.map(countLabel).join(', ')}`}>
                  <span>Valid</span>
                  {summary.map((item) => <span key={item.kind}>{countLabel(item)}</span>)}
                </p>
              )}
              {MODEL_GROUPS.map((group) => {
                const items = outline.filter((item) => !item.ownerId && group.kinds.includes(item.kind));
                return items.length > 0 ? (
                  <section key={group.label} className="model-kind-group">
                    <h3>{group.label}</h3>
                    {items.map((item) => renderConcept(item))}
                  </section>
                ) : null;
              })}
            </div>
          </div>
        </Panel>
        <Separator className="resize-handle" />
        <Panel defaultSize="55" minSize="25">
          <div className="center">
            <EditorTabs openPaths={openPaths} activePath={activePath} onSelect={setActivePath} onClose={closeDocument} />
            <PlaygroundEditor key={draftRevision} documents={draft.documents} activePath={activePath} onChange={onDocumentChange} navigationTarget={navigationTarget} provideCompletions={provideCompletions} />
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
                  capture('projection_viewed', { view: event.target.value });
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
            {rootId && (!projection || projection.succeeded) && !projection?.graph && <div className="diagram-placeholder">Loading…</div>}
            <GraphCanvas graph={projection?.graph} />
          </div>
        </Panel>
      </Group>
      <footer className="playground-status-line" role="status">
        <StatusBanner notice={analysisStatus} />
        {messageNotice ? (
          <div className={`playground-message playground-message-${messageNotice.kind}`}>{messageNotice.text}</div>
        ) : (
          <p className="playground-message">
            This is a browser draft, not a durable local workspace — it lives only in this tab until you download it.{' '}
            <a href="https://modeller.website/privacy">Privacy</a>
          </p>
        )}
      </footer>
    </div>
  );
}
