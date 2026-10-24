import { NextRequest, NextResponse } from 'next/server';
import { isPublishSlug, PUBLISHED_SNAPSHOT_VERSION, type PublishedSnapshot } from '@/lib/published-snapshot';
import { savePublishedSnapshot } from '@/server/published-store';

const corsHeaders = {
  'Access-Control-Allow-Origin': '*',
  'Access-Control-Allow-Headers': 'Authorization, Content-Type',
  'Access-Control-Allow-Methods': 'POST, OPTIONS',
};

export function OPTIONS() {
  return new NextResponse(null, { status: 204, headers: corsHeaders });
}

export async function POST(request: NextRequest) {
  const expectedToken = process.env.MODELLER_PUBLISH_TOKEN;
  if (!expectedToken) return NextResponse.json({ error: 'Publishing is not configured.' }, { status: 503, headers: corsHeaders });
  if (request.headers.get('authorization') !== `Bearer ${expectedToken}`) {
    return NextResponse.json({ error: 'The publish token is invalid.' }, { status: 401, headers: corsHeaders });
  }

  const body = await request.json() as { slug?: unknown; snapshot?: PublishedSnapshot };
  if (typeof body.slug !== 'string' || !isPublishSlug(body.slug) || !body.snapshot || body.snapshot.version !== PUBLISHED_SNAPSHOT_VERSION || !Array.isArray(body.snapshot.diagrams)) {
    return NextResponse.json({ error: 'The publish request is invalid.' }, { status: 400, headers: corsHeaders });
  }

  try {
    await savePublishedSnapshot(body.slug, body.snapshot);
    return NextResponse.json({ slug: body.slug, url: `/studio/published/${body.slug}` }, { headers: corsHeaders });
  } catch (error) {
    return NextResponse.json({ error: error instanceof Error ? error.message : 'Could not store the published snapshot.' }, { status: 503, headers: corsHeaders });
  }
}
