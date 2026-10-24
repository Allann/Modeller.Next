import { LocalAgentError, type ChatPrompt } from './prompts';

/** The one thing the cockpit needs from a model: a system/user prompt in, schema-shaped JSON text out. */
export interface LocalChatEngine {
  complete(prompt: ChatPrompt): Promise<string>;
}

/**
 * Where WebLLM keeps the model. The Cache API is WebLLM's default, but some browsers refuse to store
 * the model there with "QuotaExceededError" even when they report plenty of quota for the site; the
 * same model then stores fine in IndexedDB (web-llm issue #144). So the cockpit tries the Cache API
 * first and falls back to IndexedDB.
 */
export type CacheBackend = 'cache' | 'indexeddb';

/** Weights (1,807 MB) plus the WebGPU library and configuration, with a small margin. */
export const LOCAL_MODEL_STORAGE_BYTES = 1.9e9;

export type StorageEstimate = { quota: number; usage: number };

/**
 * The browser will not let this site store the model. The usual cause is not disk space: when Chrome
 * or Edge is set to delete site data on close (and in private windows), each site may store only about
 * 300 MB (web-llm issues #144 and #374). The panel shows the steps to exempt the site.
 */
export class StorageLimitError extends LocalAgentError {
  constructor(readonly quotaBytes: number) {
    super(`This browser lets modeller.website store only about ${Math.round(quotaBytes / 1e6)} MB. The local model needs about 1.8 GB.`);
  }
}

/** Shown when both backends refuse the model although the browser reports enough quota. */
const REFUSED_MESSAGE = 'The browser refused to store the local model, although it reports enough storage for this site. '
  + 'Reload the page and try again, or use the AI Gateway, or continue manually.';

/** WebLLM passes errors from its worker to the page as plain strings ("QuotaExceededError: ..."). */
export function describe(error: unknown): string {
  return error instanceof Error ? `${error.name}: ${error.message}` : String(error);
}

/**
 * A refused write. The Cache API reports "QuotaExceededError"; WebLLM's IndexedDB cache does not wait
 * for its write transaction to commit, so a refused write there surfaces only as
 * "ArtifactIndexedDBCache failed to fetch".
 */
function isRefusedWrite(error: unknown): boolean {
  return /QuotaExceeded|quota exceeded|ArtifactIndexedDBCache failed to fetch/i.test(describe(error));
}

/** The storage steps startWithFallback needs; separate from WebLLM so the decisions can be tested. */
export type EngineStartSteps = {
  cachedBackend(): Promise<CacheBackend | null>;
  /** The browser's storage estimate for this site, or null when the browser does not give one. */
  estimate(): Promise<StorageEstimate | null>;
  create(backend: CacheBackend): Promise<LocalChatEngine>;
  removePartialDownload(backend: CacheBackend): Promise<void>;
  onRetry(): void;
};

/** A site quota smaller than the model: no retry can succeed until the Facilitator exempts the site. */
async function quotaLimit(steps: EngineStartSteps, freeBytes: (estimate: StorageEstimate) => number): Promise<StorageLimitError | null> {
  const estimate = await steps.estimate();
  return estimate && freeBytes(estimate) < LOCAL_MODEL_STORAGE_BYTES ? new StorageLimitError(estimate.quota) : null;
}

/**
 * After a failed download: removes the partial download when the write was refused or the quota is too
 * small, and turns a small quota into a StorageLimitError. A browser may report a large quota before the
 * download and enforce a small one only when the site writes, so the quota is read again here.
 */
async function explainFailure(steps: EngineStartSteps, backend: CacheBackend, error: unknown): Promise<void> {
  const limit = await quotaLimit(steps, (estimate) => estimate.quota);
  if (!limit && !isRefusedWrite(error)) return;
  console.warn(`The browser refused to store the local model in ${backend}.`, describe(error));
  await steps.removePartialDownload(backend);
  if (limit) throw limit;
}

/**
 * Uses the backend that already holds the model. Otherwise checks the quota, downloads into the Cache
 * API, and when that write is refused, removes the partial download and tries IndexedDB once. Any
 * other error is passed on unchanged, and a fully downloaded model is never removed.
 */
export async function startWithFallback(steps: EngineStartSteps): Promise<LocalChatEngine> {
  const cached = await steps.cachedBackend();
  if (cached) return steps.create(cached);

  const limit = await quotaLimit(steps, (estimate) => estimate.quota - estimate.usage);
  if (limit) throw limit;

  try {
    return await steps.create('cache');
  } catch (error) {
    await explainFailure(steps, 'cache', error);
    if (!isRefusedWrite(error)) throw error;
    steps.onRetry();
  }

  try {
    return await steps.create('indexeddb');
  } catch (error) {
    await explainFailure(steps, 'indexeddb', error);
    if (!isRefusedWrite(error)) throw error;
    throw new LocalAgentError(REFUSED_MESSAGE);
  }
}
