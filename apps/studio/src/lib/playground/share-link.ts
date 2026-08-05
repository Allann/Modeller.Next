// Stateless share links (issue #73): the entire draft lives in the URL *fragment* (after `#`),
// which browsers never send to any server merely by opening the link — the origin's server sees
// only the path, not the fragment. Compression uses the browser-native CompressionStream/
// DecompressionStream (no library needed); the envelope deliberately carries no identity — a
// recipient always opens a share link as a fresh ephemeral draft rather than silently inheriting
// the sharer's specific semantic ids (identity continuity is a *download* concern, not a share one).
import type { ConfigurationDto, WorkspaceDocumentDto } from './api-client';

const SHARE_VERSION = 1;
const FRAGMENT_PREFIX = '#s=';

// Generous but bounded: this is a sanity cap on the JSON payload before compression, not the
// practical constraint (that's MAX_ENCODED_LENGTH below).
const MAX_RAW_BYTES = 500_000;
// Keeps the final URL comfortably under every realistic browser/proxy length limit.
const MAX_ENCODED_LENGTH = 6_000;

interface ShareEnvelope {
  v: number;
  documents: WorkspaceDocumentDto[];
  configuration: ConfigurationDto;
}

export type ShareEncodeResult = { ok: true; url: string } | { ok: false; reason: 'too-large' };

export type ShareDecodeResult =
  | { ok: true; documents: WorkspaceDocumentDto[]; configuration: ConfigurationDto }
  | { ok: false; reason: 'malformed' | 'unsupported-version' | 'too-large' };

export async function encodeShareLink(
  documents: readonly WorkspaceDocumentDto[],
  configuration: ConfigurationDto,
): Promise<ShareEncodeResult> {
  const payload: ShareEnvelope = { v: SHARE_VERSION, documents: [...documents], configuration };
  const rawBytes = new TextEncoder().encode(JSON.stringify(payload));
  if (rawBytes.length > MAX_RAW_BYTES) return { ok: false, reason: 'too-large' };

  const compressed = await gzip(rawBytes);
  const encoded = toBase64Url(compressed);
  if (encoded.length > MAX_ENCODED_LENGTH) return { ok: false, reason: 'too-large' };

  return { ok: true, url: `${window.location.origin}${window.location.pathname}${FRAGMENT_PREFIX}${encoded}` };
}

export async function decodeShareLink(hash: string): Promise<ShareDecodeResult | undefined> {
  if (!hash.startsWith(FRAGMENT_PREFIX)) return undefined; // not a share link at all — not an error
  const encoded = hash.slice(FRAGMENT_PREFIX.length);
  if (encoded.length === 0) return { ok: false, reason: 'malformed' };
  if (encoded.length > MAX_ENCODED_LENGTH) return { ok: false, reason: 'too-large' };

  let compressed: Uint8Array;
  try {
    compressed = fromBase64Url(encoded);
  } catch {
    return { ok: false, reason: 'malformed' };
  }

  let rawBytes: Uint8Array;
  try {
    rawBytes = await gunzip(compressed);
  } catch {
    return { ok: false, reason: 'malformed' };
  }
  if (rawBytes.length > MAX_RAW_BYTES) return { ok: false, reason: 'too-large' };

  let parsed: unknown;
  try {
    parsed = JSON.parse(new TextDecoder().decode(rawBytes));
  } catch {
    return { ok: false, reason: 'malformed' };
  }
  if (!isShareEnvelope(parsed)) return { ok: false, reason: 'malformed' };
  if (parsed.v !== SHARE_VERSION) return { ok: false, reason: 'unsupported-version' };

  return { ok: true, documents: parsed.documents, configuration: parsed.configuration };
}

function isShareEnvelope(value: unknown): value is ShareEnvelope {
  if (!value || typeof value !== 'object') return false;
  const envelope = value as Partial<ShareEnvelope>;
  return (
    typeof envelope.v === 'number' &&
    Array.isArray(envelope.documents) &&
    envelope.documents.length > 0 &&
    envelope.documents.every((document) => typeof document?.path === 'string' && typeof document?.content === 'string') &&
    typeof envelope.configuration === 'object' &&
    envelope.configuration !== null
  );
}

async function gzip(bytes: Uint8Array): Promise<Uint8Array> {
  const stream = new Blob([bytes as BlobPart]).stream().pipeThrough(new CompressionStream('gzip'));
  return new Uint8Array(await new Response(stream).arrayBuffer());
}

async function gunzip(bytes: Uint8Array): Promise<Uint8Array> {
  const stream = new Blob([bytes as BlobPart]).stream().pipeThrough(new DecompressionStream('gzip'));
  return new Uint8Array(await new Response(stream).arrayBuffer());
}

function toBase64Url(bytes: Uint8Array): string {
  let binary = '';
  for (const byte of bytes) binary += String.fromCharCode(byte);
  return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

function fromBase64Url(value: string): Uint8Array {
  const padded = value.replace(/-/g, '+').replace(/_/g, '/') + '='.repeat((4 - (value.length % 4)) % 4);
  const binary = atob(padded);
  const bytes = new Uint8Array(binary.length);
  for (let index = 0; index < binary.length; index++) bytes[index] = binary.charCodeAt(index);
  return bytes;
}
