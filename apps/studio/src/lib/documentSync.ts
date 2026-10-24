// Pure decision logic behind WorkbenchShell's external-change detection (see that component for
// the polling/fetch plumbing around these). Kept dependency-free and framework-free so it can be
// unit tested directly, the same way src/server/generation-response.ts's buildGenerateResponseBody
// is tested via tests/unit/generate-route.test.ts rather than the Next.js route handler itself.

export interface DocumentStatusEntry {
  path: string;
  // null means "not a known source, or its stat failed" (e.g. deleted since it was opened) — see
  // resolveDocumentStatus in src/server/document-status.ts, the caller that produces these.
  mtimeMs: number | null;
}

export interface SyncableDocument {
  path: string;
  mtimeMs: number;
  externalChangeMtimeMs?: number;
}

// One tick of the external-change poll: for each open document, compare its last-known mtime
// against the freshly polled on-disk mtime and flag (or leave alone) accordingly.
//
// A `null` status — file deleted, moved, or briefly unreadable mid-write — deliberately does NOT
// flag a change: those are far more often transient (a save-in-progress from another tool, a git
// checkout mid-flight) than a deliberate delete, and "Reload from disk" couldn't succeed against a
// missing file anyway. The next successful poll naturally reconciles once the file settles.
export function mergeExternalChangeStatuses<T extends SyncableDocument>(
  documents: readonly T[],
  statuses: readonly DocumentStatusEntry[],
): T[] {
  return documents.map((document) => {
    const status = statuses.find((entry) => entry.path === document.path);
    if (!status || status.mtimeMs === null || status.mtimeMs === document.mtimeMs) return document;
    // Already flagged for exactly this on-disk mtime — leave the existing flag (and object
    // identity) alone rather than re-flagging on every subsequent poll tick.
    if (document.externalChangeMtimeMs === status.mtimeMs) return document;
    return { ...document, externalChangeMtimeMs: status.mtimeMs };
  });
}

// "Keep mine": dismiss a pending external-change flag without touching editor content. Adopting
// the external mtime as the new baseline (rather than just clearing the flag) stops the very next
// poll tick from immediately re-flagging the same already-seen change; the next autosave
// overwrites the file with the user's content and gets its own fresh mtime anyway.
export function resolveKeepMine<T extends SyncableDocument>(documents: readonly T[], path: string): T[] {
  return documents.map((document) =>
    document.path === path && document.externalChangeMtimeMs !== undefined
      ? { ...document, mtimeMs: document.externalChangeMtimeMs, externalChangeMtimeMs: undefined }
      : document);
}
