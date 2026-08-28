'use client';

import { useEffect, useRef, useState } from 'react';
import { Group, Panel, Separator } from 'react-resizable-panels';
import { buildTree } from '@/lib/tree';
import { Explorer } from './Explorer';
import { EditorTabs } from './EditorTabs';
import { MonacoEditor } from './MonacoEditor';
import { ProblemsPanel } from './ProblemsPanel';
import { DiagramPane } from './DiagramPane';
import { LocalGenerationPreview } from './LocalGenerationPreview';
import { DiagramGenerationTabs, type DiagramGenerationTab } from './DiagramGenerationTabs';
import { useElementWidthBreakpoint } from '@/lib/useElementWidthBreakpoint';
import './workbench.css';

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

  const onLoadWorkspace = async () => {
    if (!rootInput.trim()) return;
    setRootError(undefined);
    const response = await fetch('/api/workspace', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ path: rootInput.trim() }),
    });
    const data = (await response.json()) as { root?: string; sources?: string[]; error?: string };
    if (!response.ok || !data.sources) {
      setRootError(data.error ?? 'Could not load that workspace.');
      return;
    }
    setRoot(data.root ?? rootInput.trim());
    setSources(data.sources);
    setOpenedFromPackage(false);
    setOpenDocuments([]);
    setActivePath(undefined);
  };

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

  return (
    <div className="shell">
      <div className="ribbon">
        <div className="brand">
          <span className="mark">M</span> Modeller Studio
        </div>
        {!openedFromPackage && (
          <div className="workspace-switcher" aria-label="Workspace">
            <input
              type="text"
              placeholder={root || 'Workspace directory path'}
              value={rootInput}
              onChange={(event) => setRootInput(event.target.value)}
              onKeyDown={(event) => {
                if (event.key === 'Enter') void onLoadWorkspace();
              }}
              aria-label="Workspace directory path"
            />
            <button onClick={() => void onLoadWorkspace()}>Load workspace</button>
          </div>
        )}
        {openedFromPackage && (
          <div className="workspace-switcher" aria-label="Workspace">
            <span>Opened workspace package</span>
          </div>
        )}
        {!openedFromPackage && rootError && (
          <span className="workspace-switcher-error" role="alert">
            {rootError}
          </span>
        )}
      </div>
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
        <Separator className="resize-handle" />
        {isWideForGeneration ? (
          <>
            <Panel defaultSize="12.5" minSize="10">
              <DiagramPane />
            </Panel>
            <Separator className="resize-handle" />
            <Panel defaultSize="12.5" minSize="10">
              <LocalGenerationPreview />
            </Panel>
          </>
        ) : (
          <Panel defaultSize="25" minSize="15">
            <DiagramGenerationTabs
              active={diagramGenerationTab}
              onChange={setDiagramGenerationTab}
              diagram={<DiagramPane />}
              generation={<LocalGenerationPreview />}
            />
          </Panel>
        )}
      </Group>
    </div>
  );
}
