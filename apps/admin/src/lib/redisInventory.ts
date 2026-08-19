import { Redis } from '@upstash/redis';

const KEY_PATTERN = 'modeller:initiative:*';
const ACTIVE_PREFIX = 'modeller:initiative:';
const ARCHIVE_PREFIX = 'modeller:initiative:archive:';
const MAX_ITEMS = 1_000;

type InitiativeDocument = {
  id?: string;
  originalChangeRequest?: string;
  participants?: unknown[];
  questions?: unknown[];
  responses?: unknown[];
  selectedInterventions?: unknown[];
  latestDiscoveryGateEvaluation?: unknown;
  latestShapeGateEvaluation?: unknown;
  finalization?: { status?: string } | null;
};

export type RedisInventoryItem = {
  key: string;
  initiativeId: string;
  originalChangeRequest: string;
  storage: 'Active' | 'Archive';
  phase: string;
  participants: number;
  questions: number;
  responses: number;
  interventions: number;
  ttlSeconds: number;
  expiresAt: string | null;
};

export type RedisInventory = {
  items: RedisInventoryItem[];
  truncated: boolean;
  generatedAt: string;
};

function redisClient(): Redis {
  const url = process.env.KV_REST_API_URL ?? process.env.UPSTASH_REDIS_REST_URL;
  const token = process.env.KV_REST_API_READ_ONLY_TOKEN ?? process.env.UPSTASH_REDIS_REST_TOKEN;
  if (!url || !token) throw new Error('Redis inventory is not configured.');
  return new Redis({ url, token });
}

async function scanInitiativeKeys(redis: Redis): Promise<{ keys: string[]; truncated: boolean }> {
  const keys: string[] = [];
  let cursor = '0';
  do {
    const [nextCursor, page] = await redis.scan(cursor, { match: KEY_PATTERN, count: 100 });
    keys.push(...page);
    cursor = nextCursor;
  } while (cursor !== '0' && keys.length < MAX_ITEMS);
  return { keys: keys.slice(0, MAX_ITEMS), truncated: cursor !== '0' || keys.length > MAX_ITEMS };
}

function count(value: unknown[] | undefined): number {
  return Array.isArray(value) ? value.length : 0;
}

function phase(document: InitiativeDocument): string {
  if (document.finalization) return document.finalization.status ?? 'Finalized';
  if (count(document.selectedInterventions) > 0 || document.latestShapeGateEvaluation) return 'Shape';
  if (document.latestDiscoveryGateEvaluation) return 'Frame';
  return 'Discover';
}

function initiativeId(key: string, document: InitiativeDocument): string {
  if (typeof document.id === 'string') return document.id;
  return key.startsWith(ARCHIVE_PREFIX) ? key.slice(ARCHIVE_PREFIX.length) : key.slice(ACTIVE_PREFIX.length);
}

export async function getRedisInventory(): Promise<RedisInventory> {
  const redis = redisClient();
  const { keys, truncated } = await scanInitiativeKeys(redis);
  const generatedAt = new Date();
  if (keys.length === 0) return { items: [], truncated, generatedAt: generatedAt.toISOString() };

  const documents = await redis.mget<InitiativeDocument[]>(...keys);
  const ttlPipeline = redis.pipeline();
  for (const key of keys) ttlPipeline.ttl(key);
  const ttls = await ttlPipeline.exec<number[]>();

  const items = keys.flatMap((key, index): RedisInventoryItem[] => {
    const document = documents[index];
    const ttlSeconds = ttls[index];
    if (!document || ttlSeconds === -2) return [];
    return [{
      key,
      initiativeId: initiativeId(key, document),
      originalChangeRequest: typeof document.originalChangeRequest === 'string' ? document.originalChangeRequest : 'Unavailable',
      storage: key.startsWith(ARCHIVE_PREFIX) ? 'Archive' : 'Active',
      phase: phase(document),
      participants: count(document.participants),
      questions: count(document.questions),
      responses: count(document.responses),
      interventions: count(document.selectedInterventions),
      ttlSeconds,
      expiresAt: ttlSeconds >= 0 ? new Date(generatedAt.getTime() + ttlSeconds * 1_000).toISOString() : null,
    }];
  });

  items.sort((left, right) => left.ttlSeconds - right.ttlSeconds || left.initiativeId.localeCompare(right.initiativeId));
  return { items, truncated, generatedAt: generatedAt.toISOString() };
}
