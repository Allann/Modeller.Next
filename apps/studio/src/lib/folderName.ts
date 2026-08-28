// The workspace root arrives as a real OS path (Windows-style from Electron's dialog, POSIX-style
// in tests/other platforms) — the status bar only wants its last segment, matching how VS Code's
// status bar shows a folder name rather than its full path.
export function folderName(rootPath: string): string {
  const segments = rootPath.split(/[\\/]/).filter(Boolean);
  return segments.at(-1) ?? rootPath;
}
