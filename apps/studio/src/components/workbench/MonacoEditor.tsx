'use client';

import { useEffect, useRef } from 'react';
import type { StudioEditorSession } from '@/lib/languageclient-setup';
import { startLanguageClient } from '@/lib/languageclient-setup';

// One instance per open document (see WorkbenchShell — mounted with `key={path}`
// so switching tabs unmounts/remounts rather than trying to swap models on a
// shared instance). See the KNOWN PHASE-1 SIMPLIFICATION note in
// languageclient-setup.ts for the follow-up to make this share one connection.
export function MonacoEditor({ path, content, onChange }: { path: string; content: string; onChange: (value: string) => void }) {
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const container = containerRef.current;
    if (!container) return;

    let cancelled = false;
    let activeSession: StudioEditorSession | undefined;

    void startLanguageClient(container, path, content, () => cancelled).then((session) => {
      // If cancelled before this resolved, startLanguageClient already
      // disposed everything itself — see the isCancelled checks there.
      if (!session) return;
      session.editor.onDidChangeModelContent(() => {
        onChange(session.editor.getValue());
      });
      // Cleanup may have already fired while this was still in flight (it
      // only found `activeSession` unset at the time, so couldn't dispose
      // this session itself) — cover that narrow race window here.
      if (cancelled) {
        session.dispose();
        return;
      }
      activeSession = session;
    }).catch((error: unknown) => {
      console.error(`Failed to start the editor session for ${path}:`, error);
    });

    return () => {
      cancelled = true;
      // The ordinary case: the tab is being closed (or the path is changing)
      // after the session was already established — dispose its model/editor/
      // connection now so the model doesn't linger (which both blocks
      // reopening the same document and leaves its diagnostics stuck in the
      // Problems panel, since markers live on the model until it's disposed).
      activeSession?.dispose();
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [path]);

  return <div ref={containerRef} className="editor-container" />;
}
