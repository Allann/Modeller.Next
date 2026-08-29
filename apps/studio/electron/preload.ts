// Runs in an isolated context (contextIsolation: true, nodeIntegration: false — see main.ts) with
// only what's explicitly exposed here reaching the page. The page itself is just the existing local
// Studio web app; it never needs raw Node/Electron access, only these few narrow actions.
import { contextBridge, ipcRenderer } from 'electron';

type PanelKind = 'diagram' | 'generation';
interface PanelDetachState {
  diagram: boolean;
  generation: boolean;
}

contextBridge.exposeInMainWorld('modeller', {
  onOpenFolder(callback: (path: string) => void): () => void {
    const listener = (_event: Electron.IpcRendererEvent, path: string) => callback(path);
    ipcRenderer.on('workspace:open-folder', listener);
    return () => ipcRenderer.removeListener('workspace:open-folder', listener);
  },
  detachPanel(kind: PanelKind): void {
    ipcRenderer.send('panel:detach', kind);
  },
  requestOpenFolder(): void {
    ipcRenderer.send('dialog:open-folder-request');
  },
  recordRecentWorkspace(root: string): void {
    ipcRenderer.send('workspace:record-recent', root);
  },
  onPanelDetachState(callback: (state: PanelDetachState) => void): () => void {
    const listener = (_event: Electron.IpcRendererEvent, state: PanelDetachState) => callback(state);
    ipcRenderer.on('panel:detach-state', listener);
    return () => ipcRenderer.removeListener('panel:detach-state', listener);
  },
  // Pulls the current state rather than relying solely on main's did-finish-load push, which can
  // race a fresh page load: the push can arrive before this window's React tree has mounted and
  // called onPanelDetachState above, in which case it's simply missed (IPC events aren't queued for
  // listeners that don't exist yet) — this request always lands after that listener is attached,
  // since it's called from the same effect.
  requestPanelDetachState(): void {
    ipcRenderer.send('panel:detach-state-request');
  },
});
