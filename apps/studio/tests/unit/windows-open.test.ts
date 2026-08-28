import assert from 'node:assert/strict';
import { mkdtemp, readFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { test } from 'node:test';
import path from 'node:path';
import { strToU8, zipSync } from 'fflate';
import {
  defaultExtractedWorkspaceRoot,
  extractWorkspacePackage,
  resolveWorkspacePackageArgument,
  WorkspacePackageOpenError,
} from '../../src/server/workspace-package';
import { buildWorkspaceZip, WINDOWS_STUDIO_INSTALLER_URL } from '../../src/lib/playground/workspace-bundle';

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

test('a downloaded workspace package contains the Windows opener metadata', () => {
  const bytes = buildWorkspaceZip(
    [{ path: 'model/context.modeller', content: 'rml 1.0\ncontext Downloaded\nend\n' }],
    { kind: 'durable', version: '1.0', documents: { 'model/context.modeller': ['context-id'] } },
    { generationContractVersion: '1.0', logicalOutputRoot: 'generated/', profile: 'child-care-csharp' },
  );
  const files = Object.fromEntries(
    Object.entries(zipSync({})).map(([name, content]) => [name, new TextDecoder().decode(content)]),
  );
  Object.assign(files, Object.fromEntries(
    Object.entries(require('fflate').unzipSync(bytes)).map(([name, content]) => [name, new TextDecoder().decode(content as Uint8Array)]),
  ));

  assert.equal(JSON.parse(files['.modeller/package.json']).windowsInstallerUrl, WINDOWS_STUDIO_INSTALLER_URL);
  assert.equal(JSON.parse(files['.modeller/package.json']).windowsFileExtension, '.modeller-workspace');
  assert.match(files.README, /Double-click this package/);
  assert.match(files.README, /ModellerStudioSetup\.exe/);
});

test('the default extracted workspace root is stable for the same package bytes', () => {
  const originalLocalAppData = process.env.LOCALAPPDATA;
  process.env.LOCALAPPDATA = 'C:\\Users\\Reader\\AppData\\Local';
  try {
    const first = Buffer.from(packageBytes());
    const second = Buffer.from(packageBytes());

    assert.equal(defaultExtractedWorkspaceRoot(first), defaultExtractedWorkspaceRoot(second));
    assert.match(
      defaultExtractedWorkspaceRoot(first),
      /^C:\\Users\\Reader\\AppData\\Local\\Modeller Studio\\OpenedWorkspaces\\[a-f0-9]{16}$/,
    );
  } finally {
    if (originalLocalAppData === undefined) {
      delete process.env.LOCALAPPDATA;
    } else {
      process.env.LOCALAPPDATA = originalLocalAppData;
    }
  }
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

test('opening a downloaded package rejects a missing declared source', async () => {
  const targetRoot = await mkdtemp(path.join(tmpdir(), 'modeller-opened-workspace-'));
  try {
    const missingSourcePackage = packageBytes({
      '.modeller/config.json': JSON.stringify({
        version: '1.0',
        sources: ['model/missing.modeller'],
      }),
    });

    await assert.rejects(
      () => extractWorkspacePackage(missingSourcePackage, targetRoot),
      (error) => error instanceof WorkspacePackageOpenError && /missing a source document/.test(error.message),
    );
  } finally {
    await rm(targetRoot, { recursive: true, force: true });
  }
});

test('opening a downloaded package does not extract path traversal entries', async () => {
  const targetRoot = await mkdtemp(path.join(tmpdir(), 'modeller-opened-workspace-'));
  const outsideFile = path.join(path.dirname(targetRoot), 'escape.modeller');
  try {
    await rm(outsideFile, { force: true });

    await extractWorkspacePackage(packageBytes({ '../escape.modeller': 'escaped' }), targetRoot);

    await assert.rejects(() => readFile(outsideFile, 'utf-8'), /ENOENT/);
  } finally {
    await rm(targetRoot, { recursive: true, force: true });
    await rm(outsideFile, { force: true });
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

test('the electron-builder config registers the workspace file association and per-user NSIS install', async () => {
  const config = JSON.parse(await readFile(path.join(process.cwd(), 'electron-builder.json'), 'utf-8'));

  assert.deepEqual(
    config.fileAssociations.map((association: { ext: string }) => association.ext),
    ['modeller-workspace'],
  );
  assert.equal(config.win.target, 'nsis');
  assert.equal(config.nsis.perMachine, false);
  // Preserved from the previous Inno Setup installer: workspace-bundle.ts's
  // WINDOWS_STUDIO_INSTALLER_URL points at a GitHub release asset with this exact filename.
  assert.equal(config.artifactName, 'ModellerStudioSetup.exe');
  assert.equal(config.asar, false);
});

test('the Electron main process forwards a file-association argument to the spawned server', async () => {
  const { resolveForwardedArgs } = await import('../../electron/server-supervisor');

  // Dev (`electron .`): argv is [electronExe, mainJsPath, ...userArgs].
  assert.deepEqual(
    resolveForwardedArgs(['electron.exe', 'main.js', 'C:\\Users\\Reader\\Downloads\\sample.modeller-workspace'], true),
    ['C:\\Users\\Reader\\Downloads\\sample.modeller-workspace'],
  );
  // Packaged: argv is [appExe, ...userArgs] — no separate script entry.
  assert.deepEqual(
    resolveForwardedArgs(['ModellerStudio.exe', 'C:\\Users\\Reader\\Downloads\\sample.modeller-workspace'], false),
    ['C:\\Users\\Reader\\Downloads\\sample.modeller-workspace'],
  );
  assert.deepEqual(resolveForwardedArgs(['electron.exe', 'main.js'], true), []);
});
