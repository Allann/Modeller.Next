import { BrowserWindow, Menu, dialog, shell } from 'electron';
import { existsSync } from 'node:fs';
import path from 'node:path';
import { fetchWorkspaceInfo } from './workspace-client';
import { clearPanelWindowState } from './panel-window-state';
import { resetOpenPanelWindowBounds } from './panel-windows';

// Exported so main.ts's dialog:open-folder-request IPC listener (triggered by clicking the folder
// name in the renderer's status bar, see StatusBar.tsx) can reuse the exact same dialog + push
// behavior as the File menu's own "Open Folder..." item, instead of a second implementation.
export async function openFolder(mainWindow: BrowserWindow): Promise<void> {
  const result = await dialog.showOpenDialog(mainWindow, { properties: ['openDirectory'] });
  if (result.canceled || !result.filePaths[0]) return;
  mainWindow.webContents.send('workspace:open-folder', result.filePaths[0]);
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

export function buildApplicationMenu(mainWindow: BrowserWindow, port: number): Menu {
  return Menu.buildFromTemplate([
    {
      label: 'File',
      submenu: [
        { label: 'Open Folder...', accelerator: 'CmdOrCtrl+O', click: () => void openFolder(mainWindow) },
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
