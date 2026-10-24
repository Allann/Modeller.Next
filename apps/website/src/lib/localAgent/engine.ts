import { LocalAgentError, type ChatPrompt } from './prompts';
import { describe, startWithFallback, type CacheBackend, type LocalChatEngine } from './storageFallback';

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
/** Weights (1,807 MB) plus the WebGPU library and configuration, with a small margin. */
const LOCAL_MODEL_STORAGE_BYTES = 1.9e9;

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

export function isWebGpuAvailable(): boolean {
  if (typeof window !== 'undefined' && window.__modellerLocalEngine) return true;
  return typeof navigator !== 'undefined' && 'gpu' in navigator;
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

function gigabytes(bytes: number): string {
  return `${(bytes / 1e9).toFixed(1)} GB`;
}

function storageMessage(availableBytes: number | null): string {
  const limit = availableBytes === null ? '' : ` This browser lets this site store only about ${gigabytes(Math.max(availableBytes, 0))}.`;
  return `The local model needs about ${LOCAL_MODEL_DOWNLOAD_SIZE} of browser storage.${limit} `
    + 'This usually means the browser deletes site data when it closes, which limits each site to about 0.3 GB, '
    + 'however much disk space is free. To fix it, let modeller.website keep its data. '
    + 'Chrome: Settings > Privacy and security > Site settings > Additional content settings > On-device site data > '
    + '"Allowed to save data on your device" > Add. '
    + 'Edge: Settings > Privacy, search, and services > "Choose what to clear every time you close the browser" > '
    + '"Cookies and other site data" > "Don’t clear" > Add. '
    + 'A private window has the same limit. Then reload the page and try again.';
}

/**
 * Stops before a download that cannot fit. The common cause is not disk space: when the browser is
 * set to clear site data on close (or in a private window), Chrome and Edge cap each site at about
 * 300 MB (web-llm issues #144 and #374). Without this check the download fails part-way with a bare
 * "QuotaExceededError". Also asks the browser to keep the model when storage gets low.
 */
async function ensureStorageForDownload(): Promise<void> {
  if (typeof navigator === 'undefined' || !navigator.storage?.estimate) return;
  try {
    await navigator.storage.persist?.();
  } catch {
    // Persistence is a request the browser may decline; the download still works without it.
  }
  const { quota, usage } = await navigator.storage.estimate();
  if (quota === undefined) return;
  const available = quota - (usage ?? 0);
  if (available < LOCAL_MODEL_STORAGE_BYTES) throw new LocalAgentError(storageMessage(available));
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
    throw error instanceof LocalAgentError ? error : new LocalAgentError(`The local model could not start: ${describe(error)}`);
  });
  return loading;
}

function startEngine(onProgress: (progress: LoadProgress) => void): Promise<LocalChatEngine> {
  return startWithFallback({
    cachedBackend,
    ensureStorageForDownload,
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
