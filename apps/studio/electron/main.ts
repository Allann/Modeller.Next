// Electron shell around the existing local Studio server (see server.ts) — a thin supervisor and
// window, not a rewrite. See server-supervisor.ts for the server-process lifecycle helpers, menu.ts
// for the File menu, and panel-windows.ts for the detachable Diagram/Generation tool windows this
// file orchestrates.
//
// Replaces the previous browser-launch installer's real bug: ModellerStudio.vbs ran the server
// with a hidden window (shell.Run(..., 0, False)) — there was no visible window, taskbar entry, or
// way to quit other than Task Manager. A BrowserWindow here has a real close button, and
// before-quit tears down the spawned server (and, transitively, the LSP dotnet subprocess it owns).
import { app, BrowserWindow, Menu, dialog, ipcMain, type NativeImage } from 'electron';
import path from 'node:path';
import type { ChildProcess } from 'node:child_process';
import { killServerTree, resolveForwardedArgs, spawnServer, waitForServerReady } from './server-supervisor';
import { buildApplicationMenu, openFolder, refreshApplicationMenu } from './menu';
import { closeAllPanelWindows, detachState, isPanelKind, openPanelWindow } from './panel-windows';
import { fetchAppIcon } from './app-icon';
import { addRecentWorkspace } from './recent-workspaces';

const SERVER_PORT = 3100;

let serverProcess: ChildProcess | undefined;
let mainWindow: BrowserWindow | undefined;
let appIcon: NativeImage | undefined;

function broadcastPanelDetachState(): void {
  // A detached panel is parented to mainWindow, so closing mainWindow closes it too (see
  // panel-windows.ts) — its own 'closed' handler calls back into here as part of that same
  // teardown, by which point mainWindow's webContents may already be destroyed even though the
  // mainWindow reference itself isn't nulled out until mainWindow's own 'closed' handler runs.
  if (!mainWindow || mainWindow.isDestroyed() || mainWindow.webContents.isDestroyed()) return;
  mainWindow.webContents.send('panel:detach-state', detachState());
}

const gotSingleInstanceLock = app.requestSingleInstanceLock();
if (!gotSingleInstanceLock) {
  // Another instance is already running (e.g. a second `.modeller-workspace` file was
  // double-clicked) — today's server binds a fixed port per workspace root, so a second instance
  // can't usefully run alongside the first. Focus the existing window instead.
  app.quit();
} else {
  app.on('second-instance', () => {
    if (!mainWindow) return;
    if (mainWindow.isMinimized()) mainWindow.restore();
    mainWindow.focus();
  });

  ipcMain.on('panel:detach', (_event, kind: unknown) => {
    if (!mainWindow || !isPanelKind(kind)) return;
    openPanelWindow(kind, SERVER_PORT, mainWindow, appIcon, broadcastPanelDetachState);
    broadcastPanelDetachState();
  });

  // Triggered by clicking the folder name in the renderer's status bar (StatusBar.tsx) — reuses
  // the exact same dialog + push behavior as the File menu's own "Open Folder..." item.
  ipcMain.on('dialog:open-folder-request', () => {
    if (mainWindow) void openFolder(mainWindow);
  });

  // Sent by WorkbenchShell's onLoadWorkspace only after a workspace actually finishes loading
  // (not merely picked in the dialog) — see recent-workspaces.ts. Rebuilds the menu so File > Open
  // Recent reflects it immediately, since Electron menus don't live-update their submenus.
  ipcMain.on('workspace:record-recent', (_event, root: string) => {
    addRecentWorkspace(root);
    if (mainWindow) refreshApplicationMenu(mainWindow, SERVER_PORT);
  });

  // WorkbenchShell's detachedPanels React state resets to "nothing detached" on every page load,
  // including Window > Reload/Force Reload — openPanels in panel-windows.ts is unaffected by the
  // main window reloading, so without this a reload would make WorkbenchShell re-dock panels that
  // are still genuinely open in their own windows. Pulled by the renderer on mount (rather than
  // pushed from did-finish-load) because a push can race a fresh page load and arrive before
  // WorkbenchShell's onPanelDetachState listener is attached, in which case it's simply missed.
  ipcMain.on('panel:detach-state-request', broadcastPanelDetachState);

  app.whenReady().then(async () => {
    // electron-dist/main.js's parent directory is the app root in both dev (apps/studio, `electron
    // .`) and packaged (resources/app, asar disabled) layouts — see electron-builder.json.
    const resourcesPath = path.resolve(__dirname, '..');
    const forwardedArgs = resolveForwardedArgs(process.argv, process.defaultApp);

    serverProcess = spawnServer(resourcesPath, forwardedArgs, SERVER_PORT);
    serverProcess.stderr?.on('data', (chunk: Buffer) => console.error(`[server] ${chunk.toString('utf-8')}`));
    serverProcess.on('exit', (code) => console.log(`[server] exited with code ${code}`));

    try {
      await waitForServerReady(SERVER_PORT);
    } catch (error) {
      console.error(error);
      // Loading the window against a server that never came up would just show Chromium's own
      // connection-error page — an unhelpful, unbranded dead end. Fail loudly and stop instead.
      dialog.showErrorBox(
        'Modeller Studio failed to start',
        `The local server did not become ready in time.\n\n${error instanceof Error ? error.message : String(error)}`,
      );
      if (serverProcess.pid) killServerTree(serverProcess.pid);
      app.quit();
      return;
    }
    appIcon = await fetchAppIcon(SERVER_PORT);

    mainWindow = new BrowserWindow({
      width: 1440,
      height: 900,
      title: 'Modeller Studio',
      icon: appIcon,
      webPreferences: {
        nodeIntegration: false,
        contextIsolation: true,
        preload: path.join(__dirname, 'preload.js'),
      },
    });
    mainWindow.on('closed', () => { mainWindow = undefined; });
    Menu.setApplicationMenu(buildApplicationMenu(mainWindow, SERVER_PORT));
    await mainWindow.loadURL(`http://localhost:${SERVER_PORT}`);
  });

  app.on('window-all-closed', () => app.quit());
  app.on('before-quit', () => {
    closeAllPanelWindows();
    if (serverProcess?.pid) killServerTree(serverProcess.pid);
  });
}
