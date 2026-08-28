import assert from 'node:assert/strict';
import { test } from 'node:test';
import { pluralize, resolveFolderSlotMode } from '../../src/lib/statusBarFolderSlot';

test('a package-opened workspace always shows plain text, regardless of Electron', () => {
  assert.equal(resolveFolderSlotMode(false, true), 'text');
  assert.equal(resolveFolderSlotMode(false, false), 'text');
});

test('a switchable workspace shows the native-dialog button under Electron', () => {
  assert.equal(resolveFolderSlotMode(true, true), 'button');
});

test('a switchable workspace falls back to a text input outside Electron', () => {
  assert.equal(resolveFolderSlotMode(true, false), 'input');
});

test('pluralize picks the singular only at exactly one', () => {
  assert.equal(pluralize(1, 'error'), 'error');
  assert.equal(pluralize(0, 'error'), 'errors');
  assert.equal(pluralize(2, 'error'), 'errors');
});

test('pluralize accepts an irregular plural', () => {
  assert.equal(pluralize(2, 'child', 'children'), 'children');
});
