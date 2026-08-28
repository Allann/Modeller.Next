'use client';

// Local Studio's counterpart to the playground's automatic generation preview (issue #135). The
// playground calls a fast, in-memory HTTP endpoint on an edit-debounce with a circuit breaker;
// local generation shells out to a full `dotnet` subprocess (GET /api/generate ->
// server/generation-process.ts -> `modeller generate --dry-run`), which is expensive enough per
// call that it is triggered by an explicit Generate button instead — the same manual-trigger
// convention WorkbenchShell already uses for loading a workspace, rather than running on every
// keystroke.
import { useRef, useState } from 'react';
import { GenerationPreview } from './GenerationPreview';
import type { ApiDiagnostic, GeneratedArtifactDto } from '@/lib/generation-types';

type GenerationRequestResult =
  | { ok: true; artifacts: GeneratedArtifactDto[]; diagnostics: ApiDiagnostic[] }
  | { ok: false; message: string };

// Isolated from onGenerate below so each function's own branching stays small and separately
// testable: this one owns "did the request succeed and does the response have the shape we need",
// onGenerate owns "what does the component do with that outcome".
async function requestGeneration(): Promise<GenerationRequestResult> {
  const response = await fetch('/api/generate');
  const data = (await response.json()) as { artifacts?: GeneratedArtifactDto[]; diagnostics?: ApiDiagnostic[]; error?: string };
  if (!response.ok || !data.artifacts) return { ok: false, message: data.error ?? 'Failed to generate a preview.' };
  return { ok: true, artifacts: data.artifacts, diagnostics: data.diagnostics ?? [] };
}

export function LocalGenerationPreview() {
  const [artifacts, setArtifacts] = useState<GeneratedArtifactDto[]>([]);
  const [previousContent, setPreviousContent] = useState<ReadonlyMap<string, string>>(new Map());
  const [status, setStatus] = useState<'idle' | 'generating' | 'error'>('idle');
  const [diagnostics, setDiagnostics] = useState<ApiDiagnostic[]>([]);
  const [errorMessage, setErrorMessage] = useState<string | undefined>();
  const [hasGenerated, setHasGenerated] = useState(false);
  const inFlightRef = useRef(false);
  const lastContentByPathRef = useRef<Map<string, string>>(new Map());

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
      setPreviousContent(new Map(lastContentByPathRef.current));
      lastContentByPathRef.current = new Map(result.artifacts.map((artifact) => [artifact.path, artifact.content]));
      setArtifacts(result.artifacts);
      setDiagnostics(result.diagnostics);
      setStatus('idle');
      setErrorMessage(undefined);
      setHasGenerated(true);
    } catch (error) {
      setStatus('error');
      setErrorMessage(error instanceof Error ? error.message : 'Failed to generate a preview.');
    } finally {
      inFlightRef.current = false;
    }
  };

  return (
    <div className="local-generation-preview">
      <div className="local-generation-toolbar diagram-toolbar">
        <button type="button" onClick={() => void onGenerate()} disabled={status === 'generating'}>
          {status === 'generating' ? 'Generating…' : hasGenerated ? 'Regenerate' : 'Generate'}
        </button>
        {!hasGenerated && status !== 'error' && (
          <span className="local-generation-status">Runs `modeller generate --dry-run` against this workspace.</span>
        )}
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
