import { expect, test } from '@playwright/test';

test('studio loads, lists sample documents, opens one in Monaco', async ({ page }) => {
  await page.goto('/');

  await expect(page.getByText('booking.modeller', { exact: true })).toBeVisible();

  await page.getByText('booking.modeller', { exact: true }).click();
  const editor = page.locator('.monaco-editor');
  await expect(editor).toBeVisible();
  await expect(page.locator('.view-lines')).toContainText('entity Booking');
});
