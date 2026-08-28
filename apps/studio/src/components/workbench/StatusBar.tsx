'use client';

// A VS Code-style status bar, replacing WorkbenchShell's old ribbon (brand + workspace-switcher) —
// the File menu now owns opening a folder (electron/menu.ts), so the only thing this still needs to
// surface directly is which folder is loaded, plus live counts a coder actually watches while
// working: problems and pending generation changes. The folder-slot mode decision and pluralization
// live in statusBarFolderSlot.ts (unit-tested) — this component just dispatches on their result.
import { useEffect, useState } from 'react';
import { editor as monacoEditor } from 'monaco-editor';
import { getElectronBridge } from '@/lib/electronBridge';
import { folderName } from '@/lib/folderName';
import { pluralize, resolveFolderSlotMode } from '@/lib/statusBarFolderSlot';

// Mirrors ProblemsPanel.tsx's own severity check — Monaco's MarkerSeverity.Error is 8.
const ERROR_SEVERITY = 8;

function useErrorCount(): number {
  const [errorCount, setErrorCount] = useState(0);
  useEffect(() => {
    const collect = () => setErrorCount(monacoEditor.getModelMarkers({}).filter((marker) => marker.severity >= ERROR_SEVERITY).length);
    collect();
    const subscription = monacoEditor.onDidChangeMarkers(collect);
    return () => subscription.dispose();
  }, []);
  return errorCount;
}

function FolderSlot({
  mode,
  root,
  rootInput,
  onRootInputChange,
  onLoadWorkspace,
}: {
  mode: ReturnType<typeof resolveFolderSlotMode>;
  root: string;
  rootInput: string;
  onRootInputChange: (value: string) => void;
  onLoadWorkspace: (path: string) => void;
}) {
  if (mode === 'button') {
    // getElectronBridge() is guaranteed defined when resolveFolderSlotMode returns 'button'.
    const bridge = getElectronBridge()!;
    return (
      <button type="button" className="status-bar-item status-bar-folder" onClick={() => bridge.requestOpenFolder()} title={root || 'Open a folder'}>
        {root ? folderName(root) : 'Open Folder…'}
      </button>
    );
  }
  if (mode === 'input') {
    return (
      <input
        className="status-bar-item status-bar-folder-input"
        type="text"
        placeholder={root || 'Workspace directory path'}
        value={rootInput}
        onChange={(event) => onRootInputChange(event.target.value)}
        onKeyDown={(event) => {
          if (event.key === 'Enter') onLoadWorkspace(rootInput);
        }}
        aria-label="Workspace directory path"
      />
    );
  }
  return <span className="status-bar-item">{root ? folderName(root) : 'Opened workspace package'}</span>;
}

export function StatusBar({
  root,
  rootError,
  openedFromPackage,
  changedCount,
  rootInput,
  onRootInputChange,
  onLoadWorkspace,
}: {
  root: string;
  rootError?: string;
  openedFromPackage: boolean;
  changedCount?: number;
  rootInput: string;
  onRootInputChange: (value: string) => void;
  onLoadWorkspace: (path: string) => void;
}) {
  const errorCount = useErrorCount();
  const folderSlotMode = resolveFolderSlotMode(!openedFromPackage, !!getElectronBridge());

  return (
    <footer className="status-bar" role="status">
      <FolderSlot mode={folderSlotMode} root={root} rootInput={rootInput} onRootInputChange={onRootInputChange} onLoadWorkspace={onLoadWorkspace} />
      {rootError && (
        <span className="status-bar-item status-bar-error" role="alert">
          {rootError}
        </span>
      )}
      <span className="status-bar-spacer" />
      <span className="status-bar-item">
        {errorCount} {pluralize(errorCount, 'error')}
      </span>
      {changedCount !== undefined && (
        <span className="status-bar-item">
          {changedCount} {pluralize(changedCount, 'diff')}
        </span>
      )}
    </footer>
  );
}
