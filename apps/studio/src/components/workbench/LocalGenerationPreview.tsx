'use client';

// Local Studio's counterpart to the playground's automatic generation preview (issue #135). The
// playground calls a fast, in-memory HTTP endpoint on an edit-debounce with a circuit breaker;
// local generation shells out to a full `dotnet` subprocess (GET /api/generate ->
// server/generation-process.ts -> `modeller generate`), which is expensive enough per call that it
// is triggered by an explicit Generate button instead — the same manual-trigger convention
// WorkbenchShell already uses for loading a workspace, rather than running on every keystroke.
//
// Unlike the playground, this writes real files (local Studio already writes edited documents to
// disk unprompted). The server snapshots each artifact's actual on-disk content just before
// overwriting it and returns that as `before`, so the diff view is always against real state — not
// a session's previous in-memory render.
import { useRef, useState } from 'react';
import { GenerationPreview } from './GenerationPreview';
import type { ApiDiagnostic, GeneratedArtifactDto } from '@/lib/generation-types';
import { getElectronBridge } from '@/lib/electronBridge';
import { countChangedArtifacts, type GenerationChange } from '@/lib/generationChanges';

type GenerationRequestResult =
  | { ok: true; artifacts: GeneratedArtifactDto[]; before: Record<string, string>; changes: GenerationChange[]; diagnostics: ApiDiagnostic[] }
  | { ok: false; message: string };

// Isolated from onGenerate below so each function's own branching stays small and separately
// testable: this one owns "did the request succeed and does the response have the shape we need",
// onGenerate owns "what does the component do with that outcome".
async function requestGeneration(): Promise<GenerationRequestResult> {
  const response = await fetch('/api/generate');
  const data = (await response.json()) as {
    artifacts?: GeneratedArtifactDto[];
    before?: Record<string, string>;
    changes?: GenerationChange[];
    diagnostics?: ApiDiagnostic[];
    error?: string;
  };
  if (!response.ok || !data.artifacts) return { ok: false, message: data.error ?? 'Failed to generate.' };
  return { ok: true, artifacts: data.artifacts, before: data.before ?? {}, changes: data.changes ?? [], diagnostics: data.diagnostics ?? [] };
}

export function LocalGenerationPreview({
  showDetach = true,
  onResult,
}: {
  showDetach?: boolean;
  onResult?: (changedCount: number) => void;
}) {
  const [artifacts, setArtifacts] = useState<GeneratedArtifactDto[]>([]);
  const [previousContent, setPreviousContent] = useState<ReadonlyMap<string, string>>(new Map());
  const [status, setStatus] = useState<'idle' | 'generating' | 'error'>('idle');
  const [diagnostics, setDiagnostics] = useState<ApiDiagnostic[]>([]);
  const [errorMessage, setErrorMessage] = useState<string | undefined>();
  const [hasGenerated, setHasGenerated] = useState(false);
  const inFlightRef = useRef(false);

  const onGenerate = async () => {
    if (inFlightRef.current) return;
    inFlightRef.current = true;
    setStatus('generating');
    try {
      const result = await requestGeneration();
      if (!result.ok) {
        setStatus('error');
        setErrorMessage(result.message);
        return;
      }
      setArtifacts(result.artifacts);
      setPreviousContent(new Map(Object.entries(result.before)));
      setDiagnostics(result.diagnostics);
      setStatus('idle');
      setErrorMessage(undefined);
      setHasGenerated(true);
      onResult?.(countChangedArtifacts(result.changes));
    } catch (error) {
      setStatus('error');
      setErrorMessage(error instanceof Error ? error.message : 'Failed to generate.');
    } finally {
      inFlightRef.current = false;
    }
  };

  const bridge = showDetach ? getElectronBridge() : undefined;

  return (
    <div className="local-generation-preview">
      <div className="local-generation-toolbar diagram-toolbar">
        <button type="button" onClick={() => void onGenerate()} disabled={status === 'generating'}>
          {status === 'generating' ? 'Generating…' : hasGenerated ? 'Regenerate' : 'Generate'}
        </button>
        {!hasGenerated && status !== 'error' && (
          <span className="local-generation-status">Writes generated output to the workspace&apos;s output folder.</span>
        )}
        {bridge ? (
          <button type="button" className="panel-detach-btn" onClick={() => bridge.detachPanel('generation')} title="Open in a separate window">
            Detach
          </button>
        ) : !showDetach ? (
          // This is itself the content of a detached panel window (see panels/generation/page.tsx)
          // — closing it re-docks the panel in the main window (see panel-windows.ts's 'closed' handler).
          <button type="button" className="panel-detach-btn" onClick={() => window.close()} title="Return to the main window">
            Reattach
          </button>
        ) : null}
      </div>
      <div className="local-generation-body">
        <GenerationPreview
          artifacts={artifacts}
          previousContentByPath={previousContent}
          status={status}
          diagnostics={diagnostics}
          errorMessage={errorMessage}
        />
      </div>
    </div>
  );
}
