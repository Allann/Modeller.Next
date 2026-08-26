'use client';

import { useCallback, useEffect, useRef, useState } from 'react';
import { Group, Panel, Separator } from 'react-resizable-panels';
import { buildTree } from '@/lib/tree';
import { Explorer } from '@/components/workbench/Explorer';
import { EditorTabs } from '@/components/workbench/EditorTabs';
import { ProblemsPanel } from '@/components/workbench/ProblemsPanel';
import { DiagramView } from '@/components/workbench/DiagramView';
import '@/components/workbench/workbench.css';
import { useViewRootSelection } from '@/lib/useViewRootSelection';
import './playground.css';
import { PlaygroundEditor, applyDiagnosticMarkers } from './PlaygroundEditor';
import { StatusBanner, type Notice } from './StatusBanner';
import { GenerationPreview } from './GenerationPreview';
import { DiagramGenerationTabs, type DiagramGenerationTab } from './DiagramGenerationTabs';
import { loadDraft, resetToExample, saveDraft, type PlaygroundDraft } from '@/lib/playground/session-store';
import {
  analyzeWorkspace,
  completeWorkspace,
  exportWorkspace,
  generateWorkspace,
  fetchSupportedViews,
  EPHEMERAL_IDENTITY,
  DEFAULT_TEMPLATE_PACK_ID,
  type ApiDiagnostic,
  type GeneratedArtifactDto,
  type ProjectionResponseDto,
  type RootSummaryDto,
  type SemanticOutlineItemDto,
  type SemanticCountDto,
} from '@/lib/playground/api-client';
import { decodeShareLink, encodeShareLink, type ShareDecodeResult } from '@/lib/playground/share-link';
import { buildWorkspaceZip, downloadWorkspaceZip } from '@/lib/playground/workspace-bundle';
import { capture } from '@/lib/productAnalytics';
import { useElementWidthBreakpoint } from '@/lib/useElementWidthBreakpoint';

const VIEW_KINDS = ['BehaviourMap', 'Lifecycle', 'CausalityAndEventFlow', 'ContextMap', 'Structural', 'RuleDecision'] as const;
type ViewKind = (typeof VIEW_KINDS)[number];
const ANALYZE_DEBOUNCE_MS = 500;
// The generation preview (issue #135) is debounced on the same cadence as analysis, but is further
// throttled by its own circuit breaker below: at most one /v1/workspace/generate call may be
// in flight at a time, and calls may not start less than GENERATE_MIN_INTERVAL_MS apart.
const GENERATE_DEBOUNCE_MS = 500;
const GENERATE_MIN_INTERVAL_MS = 5000;
// The panel-group width (not the browser window's) at or above which the generation preview gets
// its own docked panel instead of sharing a tab with Diagram view.
const GENERATION_SPLIT_BREAKPOINT_PX = 1800;

declare global {
  interface Window {
    // Playwright's fake-clock (`page.clock`) conflicts with Monaco's own use of the `performance`
    // API (see the failing "Cannot read properties of undefined (reading 'duration')" error it
    // throws from inside Monaco when a page using both is time-advanced), so the circuit breaker's
    // acceptance test instead shortens this real interval via `page.addInitScript` — a deliberately
    // narrow test hook, inert unless a test sets it, that lets the 5s minimum interval be exercised
    // in real time without a multi-second test. Never set outside a test.
    __playgroundTestGenerateMinIntervalMs__?: number;
  }
}

function getGenerateMinIntervalMs(): number {
  if (typeof window !== 'undefined' && typeof window.__playgroundTestGenerateMinIntervalMs__ === 'number') {
    return window.__playgroundTestGenerateMinIntervalMs__;
  }
  return GENERATE_MIN_INTERVAL_MS;
}

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
  const { view, setView, rootId, setRootId } = useViewRootSelection<ViewKind>('Lifecycle');
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

  // Generation preview (issue #135) state.
  const [generatedArtifacts, setGeneratedArtifacts] = useState<GeneratedArtifactDto[]>([]);
  const [previousGeneratedContent, setPreviousGeneratedContent] = useState<ReadonlyMap<string, string>>(new Map());
  const [generateStatus, setGenerateStatus] = useState<'idle' | 'generating' | 'error'>('idle');
  const [generateDiagnostics, setGenerateDiagnostics] = useState<ApiDiagnostic[]>([]);
  const [generateErrorMessage, setGenerateErrorMessage] = useState<string | undefined>();
  const [diagramGenerationTab, setDiagramGenerationTab] = useState<DiagramGenerationTab>('diagram');
  const draftRef = useRef(draft);
  useEffect(() => {
    draftRef.current = draft;
  }, [draft]);
  const generateDebounceRef = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);
  const generateRetryTimerRef = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);
  const generateRequestIdRef = useRef(0);
  const generateInFlightRef = useRef(false);
  const generatePendingRef = useRef(false);
  const generateLastStartRef = useRef(0);
  const lastGeneratedContentByPathRef = useRef<Map<string, string>>(new Map());
  const groupElementRef = useRef<HTMLDivElement | null>(null);
  const isWideForGeneration = useElementWidthBreakpoint(groupElementRef, GENERATION_SPLIT_BREAKPOINT_PX);

  const loadSharedDraft = useCallback((shared: PlaygroundDraft) => {
    setDraft(shared);
    saveDraft(shared);
    setDraftRevision((revision) => revision + 1);
    setOpenPaths(shared.documents[0] ? [shared.documents[0].path] : []);
    setActivePath(shared.documents[0]?.path);
    setRootId('');
    setProjection(undefined);
  }, [setRootId]);

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
  }, [draft.documents, draft.identity, draft.configuration, view, rootId, setRootId]);

  const provideCompletions = useCallback((path: string, line: number, column: number, signal: AbortSignal) =>
    completeWorkspace(draft.documents, draft.identity, draft.configuration, path, line, column, signal),
  [draft.documents, draft.identity, draft.configuration]);

  useEffect(() => {
    clearTimeout(debounceRef.current);
    debounceRef.current = setTimeout(() => void runAnalysis(), ANALYZE_DEBOUNCE_MS);
    return () => clearTimeout(debounceRef.current);
  }, [runAnalysis]);

  // Generation preview (issue #135) circuit breaker. A trigger (the debounced effect below, or a
  // scheduled retry) always calls this same function; the guards decide whether it actually starts
  // a call or just records that one is wanted:
  //  - in-flight guard: a call already running -> set `pending`, do nothing else.
  //  - minimum-interval guard: less than GENERATE_MIN_INTERVAL_MS since the last call *started* ->
  //    set `pending` and schedule a timer for the remaining time.
  //  - otherwise: start the call now.
  // When a running call finishes, or a scheduled retry timer fires, `pending` is checked; if set,
  // it's cleared and exactly one new call starts for the current draft (read from `draftRef` so it
  // always reflects the latest edits, not whatever `draft` was when this closure was created).
  // This is trailing-edge throttling: a burst of edits produces at most one call per
  // GENERATE_MIN_INTERVAL_MS window, always ending on the latest state.
  //
  // `runGeneration` calls itself (from the retry timer, and from its own `finally` block) to start
  // that queued trailing call. It reaches itself through `runGenerationRef` rather than its own
  // name — the function is created once (empty dependency array) so the indirection changes
  // nothing at runtime, but referencing the not-yet-initialized `const` directly from inside its
  // own body reads as a temporal-dead-zone hazard to the linter.
  const runGenerationRef = useRef<() => void>(() => {});
  const runGeneration = useCallback(async () => {
    if (generateInFlightRef.current) {
      generatePendingRef.current = true;
      return;
    }
    const minIntervalMs = getGenerateMinIntervalMs();
    const elapsedSinceLastStart = Date.now() - generateLastStartRef.current;
    if (elapsedSinceLastStart < minIntervalMs) {
      generatePendingRef.current = true;
      clearTimeout(generateRetryTimerRef.current);
      generateRetryTimerRef.current = setTimeout(() => {
        if (!generatePendingRef.current) return;
        generatePendingRef.current = false;
        runGenerationRef.current();
      }, minIntervalMs - elapsedSinceLastStart);
      return;
    }

    generateInFlightRef.current = true;
    generateLastStartRef.current = Date.now();
    const requestId = ++generateRequestIdRef.current;
    setGenerateStatus('generating');
    const currentDraft = draftRef.current;
    try {
      const response = await generateWorkspace(currentDraft.documents, currentDraft.identity, currentDraft.configuration, DEFAULT_TEMPLATE_PACK_ID);
      if (requestId === generateRequestIdRef.current) {
        // A content failure (parse/validate/plan/render) still comes back as a 200 with
        // `diagnostics` populated and `artifacts` empty, per the API's own contract — it must not
        // advance the "previous render" baseline, or the next successful generation would diff
        // against nothing (an empty failed attempt) instead of the last real version. Only a
        // response that actually produced artifacts counts as a new baseline.
        if (response.artifacts.length > 0) {
          // Diff each artifact against its immediately previous render, captured before this
          // response's content overwrites it.
          setPreviousGeneratedContent(new Map(lastGeneratedContentByPathRef.current));
          lastGeneratedContentByPathRef.current = new Map(response.artifacts.map((artifact) => [artifact.path, artifact.content]));
          setGeneratedArtifacts(response.artifacts);
        }
        setGenerateDiagnostics(response.diagnostics);
        setGenerateStatus('idle');
        setGenerateErrorMessage(undefined);
      }
    } catch (error) {
      if (requestId === generateRequestIdRef.current) {
        setGenerateStatus('error');
        setGenerateErrorMessage(error instanceof Error ? error.message : 'Failed to generate the workspace preview.');
      }
    } finally {
      generateInFlightRef.current = false;
      if (generatePendingRef.current) {
        generatePendingRef.current = false;
        runGenerationRef.current();
      }
    }
  }, []);
  useEffect(() => {
    runGenerationRef.current = () => void runGeneration();
  }, [runGeneration]);

  useEffect(() => {
    clearTimeout(generateDebounceRef.current);
    generateDebounceRef.current = setTimeout(() => void runGeneration(), GENERATE_DEBOUNCE_MS);
    return () => clearTimeout(generateDebounceRef.current);
  }, [draft.documents, draft.identity, draft.configuration, runGeneration]);

  useEffect(() => () => clearTimeout(generateRetryTimerRef.current), []);

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
  const diagramView = (
    <DiagramView
      view={view}
      onViewChange={(next) => {
        setView(next as ViewKind);
        capture('projection_viewed', { view: next });
      }}
      viewOptions={availableViews}
      rootId={rootId}
      onRootChange={setRootId}
      rootOptions={roots.filter((root) => root.kind === view)}
      diagnostics={projection && !projection.succeeded ? projection.diagnostics : []}
      graph={projection?.graph}
      loading={!!rootId && (!projection || projection.succeeded) && !projection?.graph}
    />
  );
  const generationPreview = (
    <GenerationPreview
      artifacts={generatedArtifacts}
      previousContentByPath={previousGeneratedContent}
      status={generateStatus}
      diagnostics={generateDiagnostics}
      errorMessage={generateErrorMessage}
    />
  );
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
      <Group orientation="horizontal" className="panel-group" elementRef={groupElementRef}>
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
                const items = outline
                  .filter((item) => !item.ownerId && group.kinds.includes(item.kind))
                  .sort((a, b) => a.name.localeCompare(b.name));
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
        {isWideForGeneration ? (
          <>
            <Panel defaultSize="12.5" minSize="10">
              {diagramView}
            </Panel>
            <Separator className="resize-handle" />
            <Panel defaultSize="12.5" minSize="10">
              {generationPreview}
            </Panel>
          </>
        ) : (
          <Panel defaultSize="25" minSize="15">
            <DiagramGenerationTabs active={diagramGenerationTab} onChange={setDiagramGenerationTab} diagram={diagramView} generation={generationPreview} />
          </Panel>
        )}
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
