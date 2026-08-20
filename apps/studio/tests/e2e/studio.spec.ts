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
