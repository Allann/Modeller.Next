import assert from 'node:assert/strict';
import { test } from 'node:test';
import { folderName } from '../../src/lib/folderName';

test('extracts the last segment of a Windows path', () => {
  assert.equal(folderName('C:\\Users\\Reader\\workspaces\\child-care'), 'child-care');
});

test('extracts the last segment of a POSIX path', () => {
  assert.equal(folderName('/home/reader/workspaces/child-care'), 'child-care');
});

test('ignores a trailing separator', () => {
  assert.equal(folderName('C:\\Users\\Reader\\workspaces\\child-care\\'), 'child-care');
});

test('returns the input unchanged when it has no separators', () => {
  assert.equal(folderName('child-care'), 'child-care');
});
