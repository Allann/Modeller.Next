// Side-effect module: runs Monaco's editor services (tokenization, diff, etc.) on their own worker
// thread instead of the main thread. Import it once from every entry point that creates a Monaco
// instance — without it Monaco falls back to fetching a default worker path that does not exist
// here, and the editor logs "Failed to load worker script for label: editorWorkerService" and
// limps along on the main thread.
//
// The previous attempt at this used the specifier 'monaco-editor/esm/vs/editor/editor.worker.js',
// which looked right (it's the real on-disk path) but doesn't match monaco-editor's own
// package.json "exports" map (`"./*.js": "./esm/vs/*.js"` — the esm/vs prefix is added BY the
// mapping, not part of the specifier), so it 404s under any exports-map-respecting resolver,
// Turbopack included. Dropping the redundant prefix resolves correctly. This app only uses the base
// editor services plus custom TextMate-backed languages (see monaco-languages.ts), not any of
// Monaco's built-in per-language workers (TS/JSON/CSS/HTML), so a single worker for every label is
// sufficient — no per-label dispatch needed.
self.MonacoEnvironment = {
  getWorker() {
    return new Worker(new URL('monaco-editor/editor/editor.worker.js', import.meta.url), { type: 'module' });
  },
};
