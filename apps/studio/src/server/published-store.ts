import { Redis } from '@upstash/redis';
import { publishedSnapshotKey, type PublishedSnapshot } from '@/lib/published-snapshot';

function redis(): Redis {
  const url = process.env.UPSTASH_REDIS_REST_URL ?? process.env.KV_REST_API_URL;
  const token = process.env.UPSTASH_REDIS_REST_TOKEN ?? process.env.KV_REST_API_TOKEN;
  if (!url || !token) throw new Error('Published snapshot storage is not configured.');
  return new Redis({ url, token });
}

export async function savePublishedSnapshot(slug: string, snapshot: PublishedSnapshot): Promise<void> {
  await redis().set(publishedSnapshotKey(slug), snapshot);
}

export function loadPublishedSnapshot(slug: string): Promise<PublishedSnapshot | null> {
  return redis().get<PublishedSnapshot>(publishedSnapshotKey(slug));
}
