import { LocalAgentError, type ChatPrompt } from './prompts';

/**
 * The model the cockpit downloads: Llama 3.2 3B Instruct, about 1.8 GB to download once and about
 * 2.3 GB of GPU memory. In a comparison on the building-variations worked example (Qwen2.5 1.5B,
 * Qwen3 1.7B, Qwen3 4B, Llama 3.2 3B; three samples each) it asked by far the fewest solution-led
 * questions. Qwen2.5 3B was better than the smaller models but has a non-commercial licence.
 * Licence: Llama 3.2 Community License — the UI must show "Built with Llama" (LocalModelPanel).
 */
export const LOCAL_MODEL_ID = 'Llama-3.2-3B-Instruct-q4f16_1-MLC';
export const LOCAL_MODEL_LABEL = 'Llama 3.2 3B Instruct';
export const LOCAL_MODEL_DOWNLOAD_SIZE = '1.8 GB';

/** Room for the longest answer (a full Discovery Gate) inside the 4,096-token context window. */
const MAX_OUTPUT_TOKENS = 900;

export type LoadProgress = { fraction: number; text: string };

/** The one thing the cockpit needs from a model: a system/user prompt in, schema-shaped JSON text out. */
export interface LocalChatEngine {
  complete(prompt: ChatPrompt): Promise<string>;
}

declare global {
  interface Window {
    /** Test seam: Playwright installs a fake engine here, since WebGPU and an 875 MB model are not
     * available in CI. Never set in production. */
    __modellerLocalEngine?: LocalChatEngine;
  }
}

export function isWebGpuAvailable(): boolean {
  if (typeof window !== 'undefined' && window.__modellerLocalEngine) return true;
  return typeof navigator !== 'undefined' && 'gpu' in navigator;
}

export async function isModelCached(): Promise<boolean> {
  if (typeof window !== 'undefined' && window.__modellerLocalEngine) return false;
  try {
    const { hasModelInCache } = await import('@mlc-ai/web-llm');
    return await hasModelInCache(LOCAL_MODEL_ID);
  } catch {
    return false;
  }
}

let loading: Promise<LocalChatEngine> | null = null;

/** Loads the model once per page (downloading it on first use) and reuses it after that. */
export function loadLocalEngine(onProgress: (progress: LoadProgress) => void): Promise<LocalChatEngine> {
  if (typeof window !== 'undefined' && window.__modellerLocalEngine) return Promise.resolve(window.__modellerLocalEngine);
  loading ??= createEngine(onProgress).catch((error: unknown) => {
    loading = null;
    throw error instanceof LocalAgentError
      ? error
      : new LocalAgentError(`The local model could not start: ${error instanceof Error ? error.message : String(error)}`);
  });
  return loading;
}

async function createEngine(onProgress: (progress: LoadProgress) => void): Promise<LocalChatEngine> {
  // Imported on demand: WebLLM is several megabytes of JavaScript that only a Facilitator who opts
  // in to the local model should ever download.
  const { CreateWebWorkerMLCEngine } = await import('@mlc-ai/web-llm');
  const worker = new Worker(new URL('./worker.ts', import.meta.url), { type: 'module' });
  const engine = await CreateWebWorkerMLCEngine(worker, LOCAL_MODEL_ID, {
    initProgressCallback: (report) => onProgress({ fraction: report.progress, text: report.text }),
  });

  return {
    async complete(prompt) {
      const reply = await engine.chat.completions.create({
        messages: [
          { role: 'system', content: prompt.system },
          { role: 'user', content: prompt.user },
        ],
        response_format: { type: 'json_object', schema: JSON.stringify(prompt.schema) },
        max_tokens: MAX_OUTPUT_TOKENS,
        temperature: 0.3,
      });
      const choice = reply.choices[0];
      if (choice?.finish_reason === 'length') throw new LocalAgentError('The local model ran out of room for its answer. Try again.');
      return choice?.message.content ?? '';
    },
  };
}
