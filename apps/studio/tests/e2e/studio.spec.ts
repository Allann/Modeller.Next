import { mkdtemp, mkdir, readFile, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { expect, test } from '@playwright/test';

test('studio loads, lists sample documents, opens one in Monaco', async ({ page }) => {
  await page.goto('/');

  await expect(page.getByText('booking.modeller', { exact: true })).toBeVisible();

  await page.getByText('booking.modeller', { exact: true }).click();
  const editor = page.locator('.monaco-editor');
  await expect(editor).toBeVisible();
  await expect(page.locator('.view-lines')).toContainText('entity Booking');
});

test('opening a single package document does not show a false context-declaration diagnostic', async ({ page }) => {
  // Regression test for issue #59/#116: booking.modeller declares no context of its own (only
  // context.modeller does), and used to show a false "context declaration required" diagnostic
  // because Modeller.LanguageServer only knew about whichever document had been explicitly
  // didOpen'd. Opening booking.modeller alone, without ever opening context.modeller, now relies
  // on the server loading sibling sources from .modeller/config.json itself.
  await page.goto('/');

  await page.getByText('booking.modeller', { exact: true }).click();
  const editor = page.locator('.monaco-editor');
  await expect(editor).toBeVisible();
  await expect(page.locator('.view-lines')).toContainText('entity Booking');

  // Give the LSP round trip (didOpen -> publishDiagnostics) a beat to land.
  await page.waitForTimeout(600);
  await expect(page.locator('.squiggly-error')).toHaveCount(0);
});

// The active workspace is a single, server-wide value (see src/server/workspace.ts) rather than
// per-request, so any test that switches it must switch it back — otherwise a later test (in this
// run or the next) starts from whichever workspace the previous switching test left behind.
test.afterEach(async ({ request }) => {
  await request.post('/api/workspace', { data: { path: '../../samples/child-care' } });
});

test('loads a different local workspace directory without restarting', async ({ page }) => {
  await page.goto('/');
  await expect(page.getByText('booking.modeller', { exact: true })).toBeVisible();

  await page.getByLabel('Workspace directory path').fill('../../samples/ordering');
  await page.getByRole('button', { name: 'Load workspace' }).click();

  await expect(page.getByText('order.modeller', { exact: true })).toBeVisible();
  await expect(page.getByText('booking.modeller', { exact: true })).not.toBeVisible();
});

test('reports an error and keeps the previous workspace when the directory has no workspace configuration', async ({ page }) => {
  const notAWorkspace = await mkdtemp(path.join(tmpdir(), 'modeller-studio-empty-'));
  try {
    await page.goto('/');
    await expect(page.getByText('booking.modeller', { exact: true })).toBeVisible();

    await page.getByLabel('Workspace directory path').fill(notAWorkspace);
    await page.getByRole('button', { name: 'Load workspace' }).click();

    await expect(page.locator('.workspace-switcher-error')).toContainText("isn't a recognised Modeller workspace");
    await expect(page.getByText('booking.modeller', { exact: true })).toBeVisible();
  } finally {
    await rm(notAWorkspace, { recursive: true, force: true });
  }
});

test('loads a workspace directory outside samples/, and edits reach disk only once the debounced save fires', async ({ page }) => {
  const workspaceRoot = await mkdtemp(path.join(tmpdir(), 'modeller-studio-workspace-'));
  try {
    await mkdir(path.join(workspaceRoot, '.modeller'), { recursive: true });
    await mkdir(path.join(workspaceRoot, 'model'), { recursive: true });
    await writeFile(
      path.join(workspaceRoot, '.modeller', 'config.json'),
      JSON.stringify({ version: '1.0', sources: ['model/context.modeller'] }, null, 2),
    );
    const documentPath = path.join(workspaceRoot, 'model', 'context.modeller');
    await writeFile(documentPath, 'rml 1.0\ncontext Standalone Workspace\n  version 1.0.0\nend\n');

    await page.goto('/');
    await page.getByLabel('Workspace directory path').fill(workspaceRoot);
    await page.getByRole('button', { name: 'Load workspace' }).click();

    await page.getByText('context.modeller', { exact: true }).click();
    const editor = page.locator('.monaco-editor');
    await expect(editor).toBeVisible();
    await expect(page.locator('.view-lines')).toContainText('Standalone Workspace');

    const savePut = page.waitForResponse(
      (response) => response.url().includes('/api/document') && response.request().method() === 'PUT',
    );

    await editor.click();
    await page.keyboard.press('End');
    await page.keyboard.type(' edited');

    // Immediately after typing, the edit lives only in the browser session — the debounced save
    // (WorkbenchShell's onDocumentChange) hasn't fired yet (it waits 500ms after the last keystroke).
    const onDiskBeforeSave = await readFile(documentPath, 'utf-8');
    expect(onDiskBeforeSave).not.toContain('edited');

    // Once the debounce elapses and the save request completes, the edit reaches disk.
    await savePut;
    const onDiskAfterSave = await readFile(documentPath, 'utf-8');
    expect(onDiskAfterSave).toContain('edited');
  } finally {
    await rm(workspaceRoot, { recursive: true, force: true });
  }
});
