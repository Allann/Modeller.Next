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
import './workbench.css';

const NO_PANELS_DETACHED: PanelDetachState = { diagram: false, generation: false };

// The panel-group width (not the browser window's) at or above which the generation preview gets
// its own docked panel instead of sharing a tab with Diagram view — mirrors
// PlaygroundWorkbench.tsx's GENERATION_SPLIT_BREAKPOINT_PX.
const GENERATION_SPLIT_BREAKPOINT_PX = 1800;

interface OpenDocument {
  path: string;
  content: string;
}

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
    const data = (await response.json()) as { root?: string; sources?: string[]; error?: string };
    if (!response.ok || !data.sources) {
      setRootError(data.error ?? 'Could not load that workspace.');
      return;
    }
    const loadedRoot = data.root ?? requestedRoot.trim();
    setRoot(loadedRoot);
    setSources(data.sources);
    setOpenedFromPackage(false);
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
      .then((data: { path: string; content: string }) => {
        setOpenDocuments((previous) => [...previous, { path: data.path, content: data.content }]);
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
      });
    }, 500);
  };

  const navigateToProblem = (path: string, _line: number) => {
    if (sources.includes(path)) openDocument(path);
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
          <DiagramPane />
        ) : key === 'generation' ? (
          <LocalGenerationPreview onResult={setChangedCount} />
        ) : (
          <DiagramGenerationTabs
            active={diagramGenerationTab}
            onChange={setDiagramGenerationTab}
            diagram={<DiagramPane />}
            generation={<LocalGenerationPreview onResult={setChangedCount} />}
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
            {activeDocument ? (
              <MonacoEditor
                key={activeDocument.path}
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
      />
    </div>
  );
}
