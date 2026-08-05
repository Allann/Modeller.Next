import { readFile, writeFile } from 'node:fs/promises';
import { NextRequest, NextResponse } from 'next/server';
import { isKnownSource, loadWorkspace, resolveSourcePath } from '@/server/workspace';
import { localOnlyRouteGuard } from '@/server/playground-guard';

export async function GET(request: NextRequest) {
  const guarded = localOnlyRouteGuard();
  if (guarded) return guarded;

  const relativePath = request.nextUrl.searchParams.get('path');
  if (!relativePath) return NextResponse.json({ error: 'Missing path.' }, { status: 400 });

  const workspace = await loadWorkspace();
  if (!isKnownSource(workspace, relativePath)) {
    return NextResponse.json({ error: 'Unknown or unsafe document path.' }, { status: 403 });
  }

  const content = await readFile(resolveSourcePath(workspace, relativePath), 'utf-8');
  return NextResponse.json({ path: relativePath, content });
}

export async function PUT(request: NextRequest) {
  const guarded = localOnlyRouteGuard();
  if (guarded) return guarded;

  const relativePath = request.nextUrl.searchParams.get('path');
  if (!relativePath) return NextResponse.json({ error: 'Missing path.' }, { status: 400 });

  const workspace = await loadWorkspace();
  if (!isKnownSource(workspace, relativePath)) {
    return NextResponse.json({ error: 'Unknown or unsafe document path.' }, { status: 403 });
  }

  const { content } = (await request.json()) as { content: string };
  await writeFile(resolveSourcePath(workspace, relativePath), content, 'utf-8');
  return NextResponse.json({ path: relativePath, saved: true });
}
