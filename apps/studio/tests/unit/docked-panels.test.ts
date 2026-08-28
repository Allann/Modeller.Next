import assert from 'node:assert/strict';
import { test } from 'node:test';
import { computeDockedRightPanelKeys } from '../../src/lib/dockedPanels';

test('wide layout docks both panels side by side when neither is detached', () => {
  assert.deepEqual(computeDockedRightPanelKeys(true, { diagram: false, generation: false }), ['diagram', 'generation']);
});

test('wide layout drops a detached panel entirely rather than showing a placeholder', () => {
  assert.deepEqual(computeDockedRightPanelKeys(true, { diagram: true, generation: false }), ['generation']);
  assert.deepEqual(computeDockedRightPanelKeys(true, { diagram: false, generation: true }), ['diagram']);
});

test('wide layout with both detached docks nothing, reclaiming the whole section', () => {
  assert.deepEqual(computeDockedRightPanelKeys(true, { diagram: true, generation: true }), []);
});

test('narrow layout shares one tabbed panel when neither is detached', () => {
  assert.deepEqual(computeDockedRightPanelKeys(false, { diagram: false, generation: false }), ['tabs']);
});

test('narrow layout with exactly one docked shows it directly, not behind a tab bar', () => {
  assert.deepEqual(computeDockedRightPanelKeys(false, { diagram: true, generation: false }), ['generation']);
  assert.deepEqual(computeDockedRightPanelKeys(false, { diagram: false, generation: true }), ['diagram']);
});

test('narrow layout with both detached docks nothing, reclaiming the whole section', () => {
  assert.deepEqual(computeDockedRightPanelKeys(false, { diagram: true, generation: true }), []);
});
