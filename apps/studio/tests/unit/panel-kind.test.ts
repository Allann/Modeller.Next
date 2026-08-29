import assert from 'node:assert/strict';
import { test } from 'node:test';
import { isPanelKind } from '../../electron/panel-windows';

test('the two real panel kinds are valid', () => {
  assert.equal(isPanelKind('diagram'), true);
  assert.equal(isPanelKind('generation'), true);
});

test('an unrecognised string is not a valid panel kind', () => {
  assert.equal(isPanelKind('problems'), false);
  assert.equal(isPanelKind(''), false);
});

test('non-string IPC payloads are not a valid panel kind', () => {
  assert.equal(isPanelKind(undefined), false);
  assert.equal(isPanelKind(null), false);
  assert.equal(isPanelKind(42), false);
  assert.equal(isPanelKind({ kind: 'diagram' }), false);
});
