import { BrowserWindow, Menu, dialog, shell } from 'electron';
import { existsSync } from 'node:fs';
import path from 'node:path';
import { fetchWorkspaceInfo } from './workspace-client';
import { clearPanelWindowState } from './panel-window-state';
import { resetOpenPanelWindowBounds } from './panel-windows';
import { clearRecentWorkspaces, loadRecentWorkspaces } from './recent-workspaces';

// Sends a workspace root (or a `.modeller-workspace` package path — see workspace.ts's
// setWorkspaceRoot) to the renderer to load — the shared last step behind the folder dialog, an
// "Open Recent" click, and main.ts's second-instance handler, so WorkbenchShell's onOpenFolder
// listener (and everything that follows from a successful load, including re-recording this same
// root as most-recent) doesn't need to know which one triggered it.
export function pushWorkspaceRoot(mainWindow: BrowserWindow, root: string): void {
  mainWindow.webContents.send('workspace:open-folder', root);
}

// Exported so main.ts's dialog:open-folder-request IPC listener (triggered by clicking the folder
// name in the renderer's status bar, see StatusBar.tsx) can reuse the exact same dialog + push
// behavior as the File menu's own "Open Folder..." item, instead of a second implementation.
export async function openFolder(mainWindow: BrowserWindow): Promise<void> {
  const result = await dialog.showOpenDialog(mainWindow, { properties: ['openDirectory'] });
  if (result.canceled || !result.filePaths[0]) return;
  pushWorkspaceRoot(mainWindow, result.filePaths[0]);
}

// Mirrors StatusBar.tsx's folderName() for the same "just the last path segment" display rule —
// duplicated rather than imported because electron/tsconfig.json compiles this folder in isolation
// from src/ (see its own rootDir/include).
function folderName(rootPath: string): string {
  const segments = rootPath.split(/[\\/]/).filter(Boolean);
  return segments.at(-1) ?? rootPath;
}

async function revealGeneratedOutput(mainWindow: BrowserWindow, port: number): Promise<void> {
  const workspace = await fetchWorkspaceInfo(port);
  const folder = workspace ? path.join(workspace.root, workspace.logicalOutputRoot) : undefined;
  if (!folder || !existsSync(folder)) {
    await dialog.showMessageBox(mainWindow, { type: 'info', message: 'No generated output yet — click Generate first.' });
    return;
  }
  await shell.openPath(folder);
}

// Un-maximizes/resizes any currently open detached panel windows back to the default (see
// resetOpenPanelWindowBounds) and clears what's persisted for future detaches — the "or reset them"
// half of the panel windows' remembered-position behavior (see panel-window-state.ts).
function resetPanelWindowPositions(): void {
  resetOpenPanelWindowBounds();
  clearPanelWindowState();
}

// VS Code-style "Open Recent" submenu: each entry re-opens that root the same way a fresh dialog
// pick would, and "Clear Recently Opened" empties the list — both need the menu rebuilt afterward
// (Electron submenus don't live-update), which is why every entry point that changes the list also
// calls refreshApplicationMenu (see main.ts's workspace:record-recent handler and clearRecent below).
function buildOpenRecentSubmenu(mainWindow: BrowserWindow, port: number): Electron.MenuItemConstructorOptions[] {
  const recent = loadRecentWorkspaces();
  if (recent.length === 0) return [{ label: 'No Recently Opened', enabled: false }];
  return [
    ...recent.map(
      (root): Electron.MenuItemConstructorOptions => ({
        label: folderName(root),
        sublabel: root,
        click: () => pushWorkspaceRoot(mainWindow, root),
      }),
    ),
    { type: 'separator' },
    {
      label: 'Clear Recently Opened',
      click: () => {
        clearRecentWorkspaces();
        refreshApplicationMenu(mainWindow, port);
      },
    },
  ];
}

export function buildApplicationMenu(mainWindow: BrowserWindow, port: number): Menu {
  return Menu.buildFromTemplate([
    {
      label: 'File',
      submenu: [
        { label: 'Open Folder...', accelerator: 'CmdOrCtrl+O', click: () => void openFolder(mainWindow) },
        { label: 'Open Recent', submenu: buildOpenRecentSubmenu(mainWindow, port) },
        { label: 'Reveal Generated Output', click: () => void revealGeneratedOutput(mainWindow, port) },
        { type: 'separator' },
        { role: 'quit' },
      ],
    },
    { role: 'editMenu' },
    { role: 'viewMenu' },
    {
      label: 'Window',
      submenu: [
        { role: 'minimize' },
        { role: 'zoom' },
        { role: 'close' },
        { type: 'separator' },
        { label: 'Reset Panel Window Positions', click: resetPanelWindowPositions },
      ],
    },
  ]);
}

// Called whenever the recent-workspaces list changes (main.ts's workspace:record-recent handler,
// and Clear Recently Opened above) — Electron menus are a static snapshot, so "Open Recent" only
// reflects the current list if the whole menu is rebuilt and reassigned.
export function refreshApplicationMenu(mainWindow: BrowserWindow, port: number): void {
  Menu.setApplicationMenu(buildApplicationMenu(mainWindow, port));
}
