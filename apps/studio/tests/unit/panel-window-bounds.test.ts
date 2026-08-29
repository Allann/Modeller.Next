import assert from 'node:assert/strict';
import { test } from 'node:test';
import { isBoundsVisible, isValidBounds } from '../../electron/panel-window-state';

const PRIMARY_DISPLAY = { x: 0, y: 0, width: 1920, height: 1080 };
const SECOND_MONITOR = { x: 1920, y: 0, width: 1920, height: 1080 };

test('bounds centered on the primary display are visible', () => {
  assert.equal(isBoundsVisible({ x: 100, y: 100, width: 1000, height: 700 }, [PRIMARY_DISPLAY]), true);
});

test('bounds on a second, currently connected monitor are visible', () => {
  assert.equal(isBoundsVisible({ x: 2000, y: 100, width: 1000, height: 700 }, [PRIMARY_DISPLAY, SECOND_MONITOR]), true);
});

test('bounds on a monitor that is no longer connected are not visible', () => {
  assert.equal(isBoundsVisible({ x: 2000, y: 100, width: 1000, height: 700 }, [PRIMARY_DISPLAY]), false);
});

test('bounds far off any display are not visible', () => {
  assert.equal(isBoundsVisible({ x: -5000, y: -5000, width: 1000, height: 700 }, [PRIMARY_DISPLAY, SECOND_MONITOR]), false);
});

test('a well-formed saved bounds entry is valid', () => {
  assert.equal(isValidBounds({ x: 100, y: 100, width: 1000, height: 700, maximized: false }), true);
});

test('a negative x/y is still valid — a second monitor left of or above the primary is legitimate', () => {
  assert.equal(isValidBounds({ x: -1920, y: -200, width: 1000, height: 700, maximized: true }), true);
});

test('a non-numeric width fails validation', () => {
  assert.equal(isValidBounds({ x: 0, y: 0, width: '1000', height: 700, maximized: false }), false);
});

test('a zero or negative dimension fails validation', () => {
  assert.equal(isValidBounds({ x: 0, y: 0, width: 0, height: 700, maximized: false }), false);
  assert.equal(isValidBounds({ x: 0, y: 0, width: 1000, height: -700, maximized: false }), false);
});

test('a non-boolean maximized flag fails validation', () => {
  assert.equal(isValidBounds({ x: 0, y: 0, width: 1000, height: 700, maximized: 'true' }), false);
});

test('a missing field fails validation', () => {
  assert.equal(isValidBounds({ x: 0, y: 0, width: 1000, maximized: false }), false);
});

test('a non-object value fails validation', () => {
  assert.equal(isValidBounds(null), false);
  assert.equal(isValidBounds('not an object'), false);
});
