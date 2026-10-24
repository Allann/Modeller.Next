'use client';

import { LOCAL_MODEL_DOWNLOAD_SIZE, LOCAL_MODEL_LABEL } from '@/lib/localAgent/engine';
import type { LocalAgentState } from '@/lib/localAgent/useLocalAgent';
import { LocalModelStorageHelp } from './LocalModelStorageHelp';

export type AiSource = 'local' | 'gateway';

/**
 * Lets a Facilitator run the Agent Participant on their own device: no API key, and the Initiative
 * text is sent to no AI service. The model downloads once (with consent) and the browser caches it.
 */
export function LocalModelPanel({
  state,
  onStart,
  gatewayUsable,
  source,
  onSourceChange,
}: {
  state: LocalAgentState;
  onStart: () => void;
  gatewayUsable: boolean;
  source: AiSource;
  onSourceChange: (source: AiSource) => void;
}) {
  if (state.status === 'checking') return null;

  return (
    <section className="local-model-panel" aria-label="Local AI model">
      <p className="local-model-title">AI on this device</p>
      {state.status === 'unsupported' && (
        <p>
          This browser cannot run the local model, because it does not support WebGPU. Use a current version of Chrome or
          Edge on a desktop computer{gatewayUsable ? ', or use the AI Gateway' : ''}.
        </p>
      )}

      {(state.status === 'idle' || state.status === 'error') && (
        <>
          <p>
            Run the AI helper in this browser. You do not need an API key, and your Initiative text is not sent to an AI
            service. {state.cached
              ? `The model (${LOCAL_MODEL_LABEL}) is already downloaded.`
              : `The first start downloads the model (${LOCAL_MODEL_LABEL}, about ${LOCAL_MODEL_DOWNLOAD_SIZE}). Your browser keeps it for later visits.`}
          </p>
          {state.status === 'error' && (state.storageQuotaBytes !== undefined
            ? <LocalModelStorageHelp quotaBytes={state.storageQuotaBytes} />
            : <p className="form-error" role="alert">{state.message}</p>)}
          <button className="secondary-action" type="button" onClick={onStart}>
            {state.cached ? 'Start the local model' : `Download (${LOCAL_MODEL_DOWNLOAD_SIZE}) and start the local model`}
          </button>
        </>
      )}

      {state.status === 'loading' && (
        <>
          <progress max={1} value={state.progress.fraction} aria-label="Local model loading progress" />
          <p className="hero-note" aria-live="polite">{state.progress.text}</p>
        </>
      )}

      {state.status === 'ready' && (
        <>
          <p>The local model ({LOCAL_MODEL_LABEL}) is ready.</p>
          {gatewayUsable && (
            <fieldset className="ai-source-choice">
              <legend>Use AI from</legend>
              <label>
                <input type="radio" name="ai-source" checked={source === 'local'} onChange={() => onSourceChange('local')} />
                This device (local model)
              </label>
              <label>
                <input type="radio" name="ai-source" checked={source === 'gateway'} onChange={() => onSourceChange('gateway')} />
                AI Gateway (higher quality)
              </label>
            </fieldset>
          )}
        </>
      )}
      {state.status !== 'unsupported' && (
        <p className="hero-note">
          Built with Llama.{' '}
          <a href="https://www.llama.com/llama3_2/license/" target="_blank" rel="noreferrer">Llama 3.2 Community License</a>
        </p>
      )}
    </section>
  );
}
