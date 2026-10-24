import assert from 'node:assert/strict';
import { test } from 'node:test';
import { mkdtemp, rm, stat, unlink, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { resolveDocumentStatus } from '../../src/server/document-status';
import type { Workspace } from '../../src/server/workspace';

async function withTempWorkspace(run: (workspace: Workspace, root: string) => Promise<void>) {
  const root = await mkdtemp(path.join(tmpdir(), 'modeller-document-status-'));
  try {
    await writeFile(path.join(root, 'booking.modeller'), 'entity Booking\nend\n', 'utf-8');
    const workspace: Workspace = { root, sources: ['booking.modeller'], logicalOutputRoot: 'generated' };
    await run(workspace, root);
  } finally {
    await rm(root, { recursive: true, force: true });
  }
}

test('resolveDocumentStatus reports the real on-disk mtime for a known source', () => withTempWorkspace(async (workspace, root) => {
  const expected = await stat(path.join(root, 'booking.modeller'));

  const status = await resolveDocumentStatus(workspace, 'booking.modeller');

  assert.equal(status.path, 'booking.modeller');
  assert.equal(status.mtimeMs, expected.mtimeMs);
}));

test('resolveDocumentStatus reports null for a path outside the workspace source list', () => withTempWorkspace(async (workspace) => {
  const status = await resolveDocumentStatus(workspace, 'not-a-declared-source.modeller');

  assert.equal(status.mtimeMs, null);
}));

test('resolveDocumentStatus reports null for a known source deleted since it was opened', () => withTempWorkspace(async (workspace, root) => {
  await unlink(path.join(root, 'booking.modeller'));

  const status = await resolveDocumentStatus(workspace, 'booking.modeller');

  assert.equal(status.mtimeMs, null);
}));

test('resolveDocumentStatus rejects an escaping relative path as unknown, not as a workspace-root read', () => withTempWorkspace(async (workspace) => {
  const status = await resolveDocumentStatus(workspace, '../outside.modeller');

  assert.equal(status.mtimeMs, null);
}));
