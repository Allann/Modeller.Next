import assert from 'node:assert/strict';
import { test } from 'node:test';
import { buildGenerateResponseBody } from '../../src/server/generation-response';
import type { GenerationResult } from '../../src/server/generation-process';

function applied(overrides: Partial<GenerationResult> = {}): GenerationResult {
  return {
    outputVersion: '1.0',
    changes: [],
    artifacts: [],
    diagnostics: [],
    ...overrides,
  };
}

test('a clean apply passes its own diagnostics through unchanged', () => {
  const result = buildGenerateResponseBody(
    applied({ changes: [{ path: 'Eligibility.cs', status: 'change', artifactId: 'rule:eligibility' }] }),
    { 'Eligibility.cs': 'old content' },
  );

  assert.deepEqual(result.diagnostics, []);
  assert.deepEqual(result.before, { 'Eligibility.cs': 'old content' });
});

test('a single conflicting artifact reports that nothing was written, since the apply is all-or-nothing', () => {
  // Mirrors OutputApplication.ExecuteAsync's atomic apply: a conflict on one artifact blocks every
  // write in the batch, not just that one file — see src/Modeller.Output/OutputApplication.cs.
  const result = buildGenerateResponseBody(
    applied({
      changes: [
        { path: 'Eligibility.cs', status: 'unchanged', artifactId: 'rule:eligibility' },
        { path: 'Entities/Application.cs', status: 'conflict', artifactId: 'entity:application' },
      ],
    }),
    {},
  );

  assert.equal(result.diagnostics.length, 1);
  assert.equal(result.diagnostics[0].code, 'workspace.generate.conflict');
  assert.match(result.diagnostics[0].message, /Entities\/Application\.cs/);
  assert.match(result.diagnostics[0].message, /Nothing was written/);
});

test('multiple conflicts are all named in the one diagnostic message', () => {
  const result = buildGenerateResponseBody(
    applied({
      changes: [
        { path: 'A.cs', status: 'conflict', artifactId: 'a' },
        { path: 'B.cs', status: 'conflict', artifactId: 'b' },
      ],
    }),
    {},
  );

  assert.equal(result.diagnostics.length, 1);
  assert.match(result.diagnostics[0].message, /A\.cs, B\.cs/);
});

test('a conflict diagnostic is prepended ahead of any diagnostics the apply already reported', () => {
  const result = buildGenerateResponseBody(
    applied({
      changes: [{ path: 'A.cs', status: 'conflict', artifactId: 'a' }],
      diagnostics: [{ code: 'workspace.output.stale', message: 'A stale artifact was reported.' }],
    }),
    {},
  );

  assert.equal(result.diagnostics.length, 2);
  assert.equal(result.diagnostics[0].code, 'workspace.generate.conflict');
  assert.equal(result.diagnostics[1].code, 'workspace.output.stale');
});
