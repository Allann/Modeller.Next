import assert from 'node:assert/strict';
import { test } from 'node:test';
import { isBoundsVisible } from '../../electron/panel-window-state';

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
