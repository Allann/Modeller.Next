import assert from 'node:assert/strict';
import { test } from 'node:test';
import { normalizeGenerationResult } from '../../src/server/generation-process';

test('normalizeGenerationResult passes a full result through unchanged', () => {
  const result = normalizeGenerationResult({
    outputVersion: '1.0',
    changes: [{ path: 'A.cs', status: 'change', artifactId: 'a' }],
    artifacts: [{ path: 'A.cs', owner: 'x', packId: 'p', templateId: 't', content: 'content' }],
    diagnostics: [{ code: 'workspace.output.stale', message: 'stale' }],
  });

  assert.equal(result.changes.length, 1);
  assert.equal(result.artifacts.length, 1);
  assert.equal(result.diagnostics.length, 1);
});

// This is the actual shape WorkspaceLoader.Failure prints in the CLI for an early failure (a bad
// config.json, an unreadable source, a missing identity-registry entry) — no `changes` or
// `artifacts` key at all. Before this normalization, that missing `artifacts` made the client treat
// a real, specific diagnostic as a generic "Failed to generate" with no information.
test('normalizeGenerationResult defaults missing changes/artifacts to empty arrays, keeping diagnostics', () => {
  const result = normalizeGenerationResult({
    outputVersion: '1.0',
    diagnostics: [{ code: 'workspace.configuration.invalid', message: 'The workspace configuration is invalid.' }],
  });

  assert.deepEqual(result.changes, []);
  assert.deepEqual(result.artifacts, []);
  assert.equal(result.diagnostics.length, 1);
  assert.equal(result.diagnostics[0].code, 'workspace.configuration.invalid');
});

test('normalizeGenerationResult defaults a missing diagnostics array too', () => {
  const result = normalizeGenerationResult({ outputVersion: '1.0' });

  assert.deepEqual(result.diagnostics, []);
});

test('normalizeGenerationResult falls back to a version string when outputVersion is missing', () => {
  const result = normalizeGenerationResult({});

  assert.equal(result.outputVersion, '1.0');
});
