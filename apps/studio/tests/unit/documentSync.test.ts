import assert from 'node:assert/strict';
import { test } from 'node:test';
import { mergeExternalChangeStatuses, resolveKeepMine, type SyncableDocument } from '../../src/lib/documentSync';

function doc(overrides: Partial<SyncableDocument> = {}): SyncableDocument {
  return { path: 'booking.modeller', mtimeMs: 100, ...overrides };
}

test('mergeExternalChangeStatuses leaves a document alone when its mtime is unchanged', () => {
  const result = mergeExternalChangeStatuses([doc()], [{ path: 'booking.modeller', mtimeMs: 100 }]);

  assert.equal(result[0].externalChangeMtimeMs, undefined);
});

test('mergeExternalChangeStatuses flags a document whose on-disk mtime moved forward', () => {
  const result = mergeExternalChangeStatuses([doc()], [{ path: 'booking.modeller', mtimeMs: 200 }]);

  assert.equal(result[0].externalChangeMtimeMs, 200);
});

test('mergeExternalChangeStatuses does not re-flag an already-acknowledged mtime', () => {
  const flagged = doc({ externalChangeMtimeMs: 200 });
  const result = mergeExternalChangeStatuses([flagged], [{ path: 'booking.modeller', mtimeMs: 200 }]);

  // Same object identity, not just an equal value — proves no redundant state update happens on
  // every poll tick once a change has already been surfaced once.
  assert.equal(result[0], flagged);
});

test('mergeExternalChangeStatuses ignores a null mtime (deleted/unreadable) rather than flagging it', () => {
  const result = mergeExternalChangeStatuses([doc()], [{ path: 'booking.modeller', mtimeMs: null }]);

  assert.equal(result[0].externalChangeMtimeMs, undefined);
});

test('mergeExternalChangeStatuses leaves a document alone when no status was returned for it', () => {
  const result = mergeExternalChangeStatuses([doc()], []);

  assert.equal(result[0].externalChangeMtimeMs, undefined);
});

test('mergeExternalChangeStatuses only touches the document the status matches', () => {
  const other = doc({ path: 'user-notification.modeller', mtimeMs: 50 });
  const result = mergeExternalChangeStatuses([doc(), other], [{ path: 'booking.modeller', mtimeMs: 200 }]);

  assert.equal(result[0].externalChangeMtimeMs, 200);
  assert.equal(result[1], other);
});

test('resolveKeepMine clears the flag and adopts the external mtime as the new baseline', () => {
  const flagged = doc({ mtimeMs: 100, externalChangeMtimeMs: 200 });
  const result = resolveKeepMine([flagged], 'booking.modeller');

  assert.equal(result[0].mtimeMs, 200);
  assert.equal(result[0].externalChangeMtimeMs, undefined);
});

test('resolveKeepMine is a no-op for a document with no pending flag', () => {
  const clean = doc();
  const result = resolveKeepMine([clean], 'booking.modeller');

  assert.equal(result[0], clean);
});

test('resolveKeepMine only affects the named document', () => {
  const flagged = doc({ externalChangeMtimeMs: 200 });
  const other = doc({ path: 'user-notification.modeller', externalChangeMtimeMs: 50 });
  const result = resolveKeepMine([flagged, other], 'booking.modeller');

  assert.equal(result[0].externalChangeMtimeMs, undefined);
  assert.equal(result[1], other);
});
