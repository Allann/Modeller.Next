import { LocalAgentError, type ChatPrompt } from './prompts';
import { describe, startWithFallback, type CacheBackend, type LocalChatEngine, type StorageEstimate } from './storageFallback';

export type { LocalChatEngine } from './storageFallback';

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

declare global {
  interface Window {
    /** Test seam: Playwright installs a fake engine here, since WebGPU and a 1.8 GB model are not
     * available in CI. Never set in production. */
    __modellerLocalEngine?: LocalChatEngine;
  }
}

/** `no-gpu`: the browser supports WebGPU, but it gives this site no GPU to use. */
export type WebGpuSupport = 'available' | 'no-webgpu' | 'no-gpu';

/** Thrown when WebLLM finds no GPU, which can happen even after checkWebGpu found one. */
export class NoGpuError extends LocalAgentError {
  constructor() {
    super('The browser did not give this site a GPU to use.');
  }
}

type GpuLike = { requestAdapter(options?: { powerPreference?: string }): Promise<unknown> };

/**
 * The WebGPU API alone is not enough: with graphics acceleration off, a blocked GPU or driver, or a
 * remote desktop, the browser has the API but gives no adapter, and WebLLM fails after the download
 * starts. Asks for an adapter the same way WebLLM does.
 */
export async function checkWebGpu(): Promise<WebGpuSupport> {
  if (typeof window !== 'undefined' && window.__modellerLocalEngine) return 'available';
  const gpu = typeof navigator === 'undefined' ? undefined : (navigator as { gpu?: GpuLike }).gpu;
  if (!gpu) return 'no-webgpu';
  try {
    return (await gpu.requestAdapter({ powerPreference: 'high-performance' })) ? 'available' : 'no-gpu';
  } catch {
    return 'no-gpu';
  }
}

/** WebLLM's errors for a missing adapter or WebGPU, which reach the page as plain text from its worker. */
function isNoGpu(error: unknown): boolean {
  const text = describe(error);
  return /Unable to find a compatible GPU|WebGPU is not supported|Cannot find WebGPU/.test(text);
}

const BACKENDS: CacheBackend[] = ['cache', 'indexeddb'];

async function appConfigFor(backend: CacheBackend) {
  const { prebuiltAppConfig } = await import('@mlc-ai/web-llm');
  return { ...prebuiltAppConfig, cacheBackend: backend };
}

/** The backend that already holds the whole model, or null when it is not downloaded. */
async function cachedBackend(): Promise<CacheBackend | null> {
  if (typeof window !== 'undefined' && window.__modellerLocalEngine) return null;
  try {
    const { hasModelInCache } = await import('@mlc-ai/web-llm');
    for (const backend of BACKENDS) {
      if (await hasModelInCache(LOCAL_MODEL_ID, await appConfigFor(backend))) return backend;
    }
  } catch {
    // Unknown cache state: treat the model as not downloaded.
  }
  return null;
}

export async function isModelCached(): Promise<boolean> {
  return (await cachedBackend()) !== null;
}

/**
 * The browser's storage estimate for this site. Also asks the browser to keep the model when storage
 * gets low; the browser may decline, and the download works either way.
 */
async function estimate(): Promise<StorageEstimate | null> {
  if (typeof navigator === 'undefined' || !navigator.storage?.estimate) return null;
  try {
    await navigator.storage.persist?.();
    const { quota, usage } = await navigator.storage.estimate();
    return quota === undefined ? null : { quota, usage: usage ?? 0 };
  } catch {
    return null;
  }
}

/** A failed download leaves part of the model in storage, using quota the next attempt needs. */
async function removePartialDownload(backend: CacheBackend): Promise<void> {
  try {
    const { deleteModelAllInfoInCache } = await import('@mlc-ai/web-llm');
    await deleteModelAllInfoInCache(LOCAL_MODEL_ID, await appConfigFor(backend));
  } catch {
    // Nothing more can be done here; the message already tells the Facilitator what to do.
  }
}

let loading: Promise<LocalChatEngine> | null = null;

/** Loads the model once per page (downloading it on first use) and reuses it after that. */
export function loadLocalEngine(onProgress: (progress: LoadProgress) => void): Promise<LocalChatEngine> {
  if (typeof window !== 'undefined' && window.__modellerLocalEngine) return Promise.resolve(window.__modellerLocalEngine);
  loading ??= startEngine(onProgress).catch((error: unknown) => {
    loading = null;
    if (error instanceof LocalAgentError) throw error;
    if (isNoGpu(error)) throw new NoGpuError();
    throw new LocalAgentError(`The local model could not start: ${describe(error)}`);
  });
  return loading;
}

function startEngine(onProgress: (progress: LoadProgress) => void): Promise<LocalChatEngine> {
  return startWithFallback({
    cachedBackend,
    estimate,
    create: (backend) => createEngine(onProgress, backend),
    removePartialDownload,
    onRetry: () => onProgress({ fraction: 0, text: 'The browser cache refused the model. Trying the browser database instead…' }),
  });
}

async function createEngine(onProgress: (progress: LoadProgress) => void, backend: CacheBackend): Promise<LocalChatEngine> {
  // Imported on demand: WebLLM is several megabytes of JavaScript that only a Facilitator who opts
  // in to the local model should ever download.
  const { CreateWebWorkerMLCEngine } = await import('@mlc-ai/web-llm');
  const worker = new Worker(new URL('./worker.ts', import.meta.url), { type: 'module' });
  let engine: Awaited<ReturnType<typeof CreateWebWorkerMLCEngine>>;
  try {
    engine = await CreateWebWorkerMLCEngine(worker, LOCAL_MODEL_ID, {
      appConfig: await appConfigFor(backend),
      initProgressCallback: (report) => onProgress({ fraction: report.progress, text: report.text }),
    });
  } catch (error) {
    worker.terminate();
    throw error;
  }

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
