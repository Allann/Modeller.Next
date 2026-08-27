import assert from 'node:assert/strict';
import { mkdtemp, readFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { test } from 'node:test';
import path from 'node:path';
import { strToU8, zipSync } from 'fflate';
import {
  extractWorkspacePackage,
  resolveWorkspacePackageArgument,
  WorkspacePackageOpenError,
} from '../../src/server/workspace-package';

function packageBytes(overrides: Record<string, string> = {}): Uint8Array {
  return zipSync({
    '.modeller/package.json': strToU8(JSON.stringify({
      packageVersion: '1.0',
      packageKind: 'ModellerStudioWorkspace',
    })),
    '.modeller/config.json': strToU8(JSON.stringify({
      version: '1.0',
      sources: ['model/context.modeller'],
    })),
    'model/context.modeller': strToU8('rml 1.0\ncontext Downloaded\nend\n'),
    ...Object.fromEntries(Object.entries(overrides).map(([name, content]) => [name, strToU8(content)])),
  });
}

test('a Windows package argument is recognised from shell open command args', () => {
  assert.equal(
    resolveWorkspacePackageArgument(['--open-workspace-package', 'C:\\Users\\Reader\\Downloads\\sample.modeller-workspace']),
    'C:\\Users\\Reader\\Downloads\\sample.modeller-workspace',
  );
  assert.equal(
    resolveWorkspacePackageArgument(['C:\\Users\\Reader\\Downloads\\sample.modeller-workspace']),
    'C:\\Users\\Reader\\Downloads\\sample.modeller-workspace',
  );
});

test('opening a downloaded workspace package extracts a writable local workspace', async () => {
  const targetRoot = await mkdtemp(path.join(tmpdir(), 'modeller-opened-workspace-'));
  try {
    await rm(targetRoot, { recursive: true, force: true });

    const openedRoot = await extractWorkspacePackage(packageBytes(), targetRoot);

    assert.equal(openedRoot, targetRoot);
    assert.match(await readFile(path.join(targetRoot, '.modeller', 'config.json'), 'utf-8'), /model\/context\.modeller/);
    assert.match(await readFile(path.join(targetRoot, 'model', 'context.modeller'), 'utf-8'), /context Downloaded/);
  } finally {
    await rm(targetRoot, { recursive: true, force: true });
  }
});

test('an unsupported package fails with a plain user message', async () => {
  const targetRoot = await mkdtemp(path.join(tmpdir(), 'modeller-opened-workspace-'));
  try {
    const unsupportedPackage = packageBytes({
      '.modeller/package.json': JSON.stringify({
        packageVersion: '2.0',
        packageKind: 'ModellerStudioWorkspace',
      }),
    });

    await assert.rejects(
      () => extractWorkspacePackage(unsupportedPackage, targetRoot),
      (error) => error instanceof WorkspacePackageOpenError && /newer Modeller Studio/.test(error.message),
    );
  } finally {
    await rm(targetRoot, { recursive: true, force: true });
  }
});

test('the Windows installer registers workspace files with the opened file placeholder', async () => {
  const script = await readFile(path.join(process.cwd(), 'scripts', 'create-windows-dist.mjs'), 'utf-8');

  assert.ok(script.includes('ModellerStudio.Workspace\\\\shell\\\\open\\\\command'));
  assert.ok(script.includes('ModellerStudio.cmd\\\\" \\\\"%%1'));
});
