'use client';

import { Fragment, useEffect, useRef, useState } from 'react';
import { Group, Panel, Separator } from 'react-resizable-panels';
import { buildTree } from '@/lib/tree';
import { Explorer } from './Explorer';
import { EditorTabs } from './EditorTabs';
import { MonacoEditor } from './MonacoEditor';
import { ProblemsPanel } from './ProblemsPanel';
import { DiagramPane } from './DiagramPane';
import { LocalGenerationPreview } from './LocalGenerationPreview';
import { DiagramGenerationTabs, type DiagramGenerationTab } from './DiagramGenerationTabs';
import { StatusBar } from './StatusBar';
import { useElementWidthBreakpoint } from '@/lib/useElementWidthBreakpoint';
import { getElectronBridge, type PanelDetachState } from '@/lib/electronBridge';
import { computeDockedRightPanelKeys } from '@/lib/dockedPanels';
import { resetLanguageSession } from '@/lib/lsp/session';
import { mergeExternalChangeStatuses, resolveKeepMine, type DocumentStatusEntry } from '@/lib/documentSync';
import './workbench.css';

const NO_PANELS_DETACHED: PanelDetachState = { diagram: false, generation: false };

// The panel-group width (not the browser window's) at or above which the generation preview gets
// its own docked panel instead of sharing a tab with Diagram view — mirrors
// PlaygroundWorkbench.tsx's GENERATION_SPLIT_BREAKPOINT_PX.
const GENERATION_SPLIT_BREAKPOINT_PX = 1800;

interface OpenDocument {
  path: string;
  content: string;
  // The on-disk mtime this document's content is known to match — set from the open/save
  // response, and used as the baseline the external-change poll compares against.
  mtimeMs: number;
  // Set once the poll below observes an on-disk mtime that no longer matches `mtimeMs` — i.e. the
  // file changed outside Studio. Cleared by either reloading from disk or dismissing the prompt.
  externalChangeMtimeMs?: number;
  // Bumped only by reloadFromDisk, never by the autosave path — MonacoEditor keys on
  // `${path}:${reloadToken}` (see its render site) so a reload forces the fresh content into a
  // brand-new editor session. MonacoEditor otherwise only reads `content` once at mount (it's an
  // "uncontrolled" editor that pushes edits out via onChange but never pulls prop changes back in,
  // by design — syncing every keystroke's echoed `content` prop back into the model would fight
  // the user's own cursor position). Keying on `mtimeMs` itself would remount on every autosave
  // too (it changes 500ms after every edit), losing cursor position/undo history constantly —
  // this counter only moves for an explicit reload.
  reloadToken: number;
}

// How often open documents are checked for changes made outside Studio (another editor, git, a
// generator run). Cheap: a batched mtime-only stat per open document, not a content read.
const EXTERNAL_CHANGE_POLL_MS = 3000;

export function WorkbenchShell() {
  const [sources, setSources] = useState<string[]>([]);
  const [root, setRoot] = useState<string>('');
  const [rootInput, setRootInput] = useState('');
  const [rootError, setRootError] = useState<string | undefined>();
  const [openedFromPackage, setOpenedFromPackage] = useState(false);
  const [openDocuments, setOpenDocuments] = useState<OpenDocument[]>([]);
  const [activePath, setActivePath] = useState<string | undefined>();
  const saveTimers = useRef<Record<string, ReturnType<typeof setTimeout>>>({});
  const [diagramGenerationTab, setDiagramGenerationTab] = useState<DiagramGenerationTab>('diagram');
  const groupElementRef = useRef<HTMLDivElement | null>(null);
  const isWideForGeneration = useElementWidthBreakpoint(groupElementRef, GENERATION_SPLIT_BREAKPOINT_PX);
  const [detachedPanels, setDetachedPanels] = useState<PanelDetachState>(NO_PANELS_DETACHED);
  const [changedCount, setChangedCount] = useState<number | undefined>(undefined);
  const [publishing, setPublishing] = useState(false);
  const [publishMessage, setPublishMessage] = useState<string | undefined>();

  useEffect(() => {
    void fetch('/api/workspace')
      .then(async (response) => {
        const data = (await response.json()) as { root?: string; sources?: string[]; openedFromPackage?: boolean; error?: string };
        if (!response.ok || !data.sources) {
          setRootError(data.error ?? 'Could not open the workspace.');
          return;
        }
        setRoot(data.root ?? '');
        setSources(data.sources);
        setOpenedFromPackage(data.openedFromPackage ?? false);
      });
  }, []);

  const onLoadWorkspace = async (requestedRoot: string) => {
    if (!requestedRoot.trim()) return;
    setRootError(undefined);
    const response = await fetch('/api/workspace', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ path: requestedRoot.trim() }),
    });
    const data = (await response.json()) as { root?: string; sources?: string[]; error?: string; openedFromPackage?: boolean };
    if (!response.ok || !data.sources) {
      setRootError(data.error ?? 'Could not load that workspace.');
      return;
    }
    const loadedRoot = data.root ?? requestedRoot.trim();
    resetLanguageSession();
    setRoot(loadedRoot);
    setSources(data.sources);
    setOpenedFromPackage(data.openedFromPackage ?? false);
    setOpenDocuments([]);
    setActivePath(undefined);
    setChangedCount(undefined);
    // Only a workspace that actually finished loading (not merely picked in a dialog) earns a spot
    // in File > Open Recent — see recent-workspaces.ts.
    getElectronBridge()?.recordRecentWorkspace(loadedRoot);
  };

  // File > Open Folder (electron/menu.ts) resolves the native dialog itself and pushes the chosen
  // path down — this listener just feeds it into the same loading path the text-input fallback
  // below uses. No-op outside Electron (getElectronBridge() returns undefined in a plain browser
  // tab, e.g. `npm run dev`).
  useEffect(() => getElectronBridge()?.onOpenFolder((path) => void onLoadWorkspace(path)), []);
  // Subscribe first, then pull the current state — including on Window > Reload/Force Reload,
  // which remounts this component while any genuinely detached panel windows stay open (see
  // main.ts's panel:detach-state-request handler for why this is a pull, not just a push).
  useEffect(() => {
    const bridge = getElectronBridge();
    if (!bridge) return undefined;
    const unsubscribe = bridge.onPanelDetachState(setDetachedPanels);
    bridge.requestPanelDetachState();
    return unsubscribe;
  }, []);

  const openDocument = (path: string) => {
    setActivePath(path);
    if (openDocuments.some((document) => document.path === path)) return;
    void fetch(`/api/document?path=${encodeURIComponent(path)}`)
      .then((response) => response.json())
      .then((data: { path: string; content: string; mtimeMs: number }) => {
        setOpenDocuments((previous) => [...previous, { path: data.path, content: data.content, mtimeMs: data.mtimeMs, reloadToken: 0 }]);
      });
  };

  const closeDocument = (path: string) => {
    setOpenDocuments((previous) => previous.filter((document) => document.path !== path));
    if (activePath === path) {
      const remaining = openDocuments.filter((document) => document.path !== path);
      setActivePath(remaining.at(-1)?.path);
    }
  };

  const onDocumentChange = (path: string, value: string) => {
    setOpenDocuments((previous) => previous.map((document) => (document.path === path ? { ...document, content: value } : document)));
    clearTimeout(saveTimers.current[path]);
    saveTimers.current[path] = setTimeout(() => {
      void fetch(`/api/document?path=${encodeURIComponent(path)}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ content: value }),
      })
        .then((response) => response.json())
        .then((data: { mtimeMs?: number }) => {
          if (typeof data.mtimeMs !== 'number') return;
          setOpenDocuments((previous) => previous.map((document) =>
            document.path === path ? { ...document, mtimeMs: data.mtimeMs! } : document));
        });
    }, 500);
  };

  // Detects a file changed outside Studio (another editor, git, a generator run) while it's open
  // in a tab. Deliberately always flags the conflict rather than silently reloading, even for a
  // tab with no local edits — an autosave could be mid-flight the instant the external write
  // lands, and a silent reload racing that write would either clobber the user's still-unsaved
  // keystrokes or immediately overwrite the external change right back, neither of which is what
  // "changed outside Studio" should do. See keepMine/reloadFromDisk for how the prompt resolves.
  //
  // Keyed on the set of open *paths*, not `openDocuments` itself: that array gets a new reference
  // on every keystroke (via onDocumentChange's setOpenDocuments), and depending on it directly
  // would tear down and restart this interval on every keystroke too — starving the poll entirely
  // during continuous typing. Reading `openDocuments` from a ref inside the tick keeps the
  // interval stable across edits while still checking current content each time it fires.
  const openDocumentsRef = useRef(openDocuments);
  useEffect(() => {
    openDocumentsRef.current = openDocuments;
  }, [openDocuments]);
  const openPathsKey = openDocuments.map((document) => document.path).join(' ');
  useEffect(() => {
    if (openPathsKey === '') return undefined;
    const interval = setInterval(() => {
      const params = new URLSearchParams();
      for (const document of openDocumentsRef.current) params.append('path', document.path);
      void fetch(`/api/document/status?${params.toString()}`)
        .then((response) => response.json())
        .then((data: { statuses?: DocumentStatusEntry[] }) => {
          setOpenDocuments((previous) => mergeExternalChangeStatuses(previous, data.statuses ?? []));
        });
    }, EXTERNAL_CHANGE_POLL_MS);
    return () => clearInterval(interval);
  }, [openPathsKey]);

  const keepMine = (path: string) => setOpenDocuments((previous) => resolveKeepMine(previous, path));

  const reloadFromDisk = (path: string) => {
    void fetch(`/api/document?path=${encodeURIComponent(path)}`)
      .then((response) => response.json())
      .then((data: { content: string; mtimeMs: number }) => {
        setOpenDocuments((previous) => previous.map((document) =>
          document.path === path
            ? { ...document, content: data.content, mtimeMs: data.mtimeMs, externalChangeMtimeMs: undefined, reloadToken: document.reloadToken + 1 }
            : document));
      });
  };

  const navigateToProblem = (path: string, _line: number) => {
    if (sources.includes(path)) openDocument(path);
  };

  const publish = async () => {
    const slug = window.prompt('Choose a URL slug for this published model (lowercase letters, digits, and hyphens).');
    if (!slug) return;
    const storedToken = window.localStorage.getItem('modeller.publish-token') ?? '';
    const token = window.prompt('Enter your publish token. It stays in this browser on this computer.', storedToken);
    if (!token) return;
    window.localStorage.setItem('modeller.publish-token', token);
    setPublishing(true);
    setPublishMessage(undefined);
    try {
      const snapshotResponse = await fetch('/api/publish-snapshot');
      const snapshot = await snapshotResponse.json();
      if (!snapshotResponse.ok) throw new Error(snapshot.error ?? 'Could not build the published snapshot.');
      const endpoint = process.env.NEXT_PUBLIC_MODELLER_PUBLISH_URL ?? 'https://modeller.website/studio/api/publish';
      const response = await fetch(endpoint, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` },
        body: JSON.stringify({ slug, snapshot }),
      });
      const result = await response.json() as { url?: string; error?: string };
      if (!response.ok || !result.url) throw new Error(result.error ?? 'Publishing failed.');
      setPublishMessage(`Published: ${new URL(result.url, endpoint).toString()}`);
    } catch (error) {
      setPublishMessage(error instanceof Error ? error.message : 'Publishing failed.');
    } finally {
      setPublishing(false);
    }
  };

  const tree = buildTree(sources);
  const activeDocument = openDocuments.find((document) => document.path === activePath);

  // A detached panel's slot is dropped entirely (not shown as a placeholder) so the remaining
  // panels reclaim its space — see electron/panel-windows.ts for what "detached" means, and
  // dockedPanels.ts (unit-tested) for which keys are docked and in what arrangement.
  const rightPanelSize = isWideForGeneration ? '12.5' : '25';
  const rightPanelMinSize = isWideForGeneration ? '10' : '15';
  const dockedRightPanels = computeDockedRightPanelKeys(isWideForGeneration, detachedPanels).map((key) => ({
    key,
    node: (
      <Panel defaultSize={rightPanelSize} minSize={rightPanelMinSize}>
        {key === 'diagram' ? (
          <DiagramPane key={root} />
        ) : key === 'generation' ? (
          <LocalGenerationPreview key={root} onResult={setChangedCount} />
        ) : (
          <DiagramGenerationTabs
            active={diagramGenerationTab}
            onChange={setDiagramGenerationTab}
            diagram={<DiagramPane key={root} />}
            generation={<LocalGenerationPreview key={root} onResult={setChangedCount} />}
          />
        )}
      </Panel>
    ),
  }));

  return (
    <div className="shell">
      <Group orientation="horizontal" className="panel-group" elementRef={groupElementRef}>
        <Panel defaultSize="20" minSize="12">
          <div className="explorer">
            <Explorer nodes={tree} activePath={activePath} onOpenDocument={openDocument} />
          </div>
        </Panel>
        <Separator className="resize-handle" />
        <Panel defaultSize="55" minSize="25">
          <div className="center">
            <EditorTabs
              openPaths={openDocuments.map((document) => document.path)}
              activePath={activePath}
              onSelect={setActivePath}
              onClose={closeDocument}
            />
            {activeDocument?.externalChangeMtimeMs !== undefined ? (
              <div className="external-change-banner" role="alert">
                <span>This file changed outside Studio.</span>
                <button type="button" className="workbench-action" onClick={() => reloadFromDisk(activeDocument.path)}>
                  Reload from disk
                </button>
                <button type="button" className="workbench-action" onClick={() => keepMine(activeDocument.path)}>
                  Keep mine
                </button>
              </div>
            ) : null}
            {activeDocument ? (
              <MonacoEditor
                key={`${activeDocument.path}:${activeDocument.reloadToken}`}
                path={activeDocument.path}
                content={activeDocument.content}
                onChange={(value) => onDocumentChange(activeDocument.path, value)}
              />
            ) : (
              <div className="empty-editor">Select a document from the explorer to begin.</div>
            )}
            <ProblemsPanel onNavigate={navigateToProblem} />
          </div>
        </Panel>
        {dockedRightPanels.map((panel) => (
          <Fragment key={panel.key}>
            <Separator className="resize-handle" />
            {panel.node}
          </Fragment>
        ))}
      </Group>
      <StatusBar
        root={root}
        rootError={rootError}
        openedFromPackage={openedFromPackage}
        changedCount={changedCount}
        rootInput={rootInput}
        onRootInputChange={setRootInput}
        onLoadWorkspace={onLoadWorkspace}
        publishing={publishing}
        publishMessage={publishMessage}
        onPublish={() => void publish()}
      />
    </div>
  );
}
