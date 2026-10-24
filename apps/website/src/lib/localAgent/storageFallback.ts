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

/** Shown when both storage backends refuse the model although the browser reported enough room. */
const REFUSED_MESSAGE = `The browser refused to store the local model (about 1.8 GB), although it reports `
  + 'enough storage for this site. Check that modeller.website may keep its data '
  + '(Chrome: Settings > Privacy and security > Site settings > Additional content settings > On-device site data; '
  + 'Edge: Settings > Privacy, search, and services > "Choose what to clear every time you close the browser"), '
  + 'reload the page, and try again. Otherwise use the AI Gateway, or continue manually.';

/** WebLLM passes errors from its worker to the page as plain strings ("QuotaExceededError: ..."). */
export function describe(error: unknown): string {
  return error instanceof Error ? `${error.name}: ${error.message}` : String(error);
}

function isQuotaError(error: unknown): boolean {
  return /QuotaExceeded|quota exceeded/i.test(describe(error));
}

/** The storage steps startWithFallback needs; separate from WebLLM so the decisions can be tested. */
export type EngineStartSteps = {
  cachedBackend(): Promise<CacheBackend | null>;
  ensureStorageForDownload(): Promise<void>;
  create(backend: CacheBackend): Promise<LocalChatEngine>;
  removePartialDownload(backend: CacheBackend): Promise<void>;
  onRetry(): void;
};

/**
 * Uses the backend that already holds the model. Otherwise checks the quota, downloads into the Cache
 * API, and on a quota error removes the partial download and tries IndexedDB once. Any other error is
 * passed on unchanged.
 */
export async function startWithFallback(steps: EngineStartSteps): Promise<LocalChatEngine> {
  const cached = await steps.cachedBackend();
  if (cached) return steps.create(cached);

  await steps.ensureStorageForDownload();
  try {
    return await steps.create('cache');
  } catch (error) {
    if (!isQuotaError(error)) throw error;
    console.warn('The Cache API refused the local model; retrying with IndexedDB.', describe(error));
    await steps.removePartialDownload('cache');
    steps.onRetry();
  }

  try {
    return await steps.create('indexeddb');
  } catch (error) {
    if (!isQuotaError(error)) throw error;
    console.warn('IndexedDB also refused the local model.', describe(error));
    await steps.removePartialDownload('indexeddb');
    throw new LocalAgentError(REFUSED_MESSAGE);
  }
}

