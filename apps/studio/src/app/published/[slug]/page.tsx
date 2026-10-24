import { notFound } from 'next/navigation';
import { PublishedDiagram } from '@/components/published/PublishedDiagram';
import { isPublishSlug, PUBLISHED_SNAPSHOT_VERSION } from '@/lib/published-snapshot';
import { loadPublishedSnapshot } from '@/server/published-store';

export default async function PublishedPage({ params }: { params: Promise<{ slug: string }> }) {
  const { slug } = await params;
  if (!isPublishSlug(slug)) notFound();
  const snapshot = await loadPublishedSnapshot(slug);
  if (!snapshot) notFound();
  if (snapshot.version !== PUBLISHED_SNAPSHOT_VERSION || !Array.isArray(snapshot.diagrams)) notFound();
  return <PublishedDiagram snapshot={snapshot} />;
}
