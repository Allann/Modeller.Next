import assert from 'node:assert/strict';
import { test } from 'node:test';
import { countChangedArtifacts } from '../../src/lib/generationChanges';

test('counts create and change statuses as diffs', () => {
  assert.equal(
    countChangedArtifacts([
      { path: 'A.cs', status: 'create' },
      { path: 'B.cs', status: 'change' },
    ]),
    2,
  );
});

test('does not count unchanged, conflict, stale, or remove as diffs', () => {
  assert.equal(
    countChangedArtifacts([
      { path: 'A.cs', status: 'unchanged' },
      { path: 'B.cs', status: 'conflict' },
      { path: 'C.cs', status: 'stale' },
      { path: 'D.cs', status: 'remove' },
    ]),
    0,
  );
});

test('an empty change list has zero diffs', () => {
  assert.equal(countChangedArtifacts([]), 0);
});
