import { expect, test, type Route } from '@playwright/test';

const publicApi = process.env.MODELLER_PUBLIC_API_URL ?? 'https://modeller-next.vercel.app';

test('a reader discovers the Child Care story and verifies its model change in the playground', async ({ page }) => {
  const forwardToPublicApi = async (route: Route) => {
    const path = new URL(route.request().url()).pathname;
    const response = await route.fetch({ url: `${publicApi}${path}` });
    await route.fulfill({ response });
  };
  await page.route('**/v1/workspace/supported-views', forwardToPublicApi);
  await page.route('**/v1/workspace/analyze', forwardToPublicApi);
  await page.route('**/v1/workspace/complete', (route) => route.fulfill({
    json: { apiVersion: '1.0', items: [], diagnostics: [] },
  }));
  await page.goto('/');
  const caseStudyLink = page.getByRole('link', { name: 'Read the case study', exact: true });
  const caseStudyHref = await caseStudyLink.getAttribute('href');
  expect(caseStudyHref).toBe('/docs/case-studies/child-care-request-more-information');
  await page.goto(new URL(caseStudyHref!, page.url()).href);
  await expect(page.getByRole('heading', { name: 'Request more information for an ACCS decision', exact: true }).first()).toBeVisible();
  await expect(page.getByRole('link', { name: 'Architecture 101' })).toBeVisible();
  await page.getByRole('link', { name: 'Child Care model in the public playground' }).click();

  await expect(page).toHaveURL(/localhost:3113\/\?example=child-care/);
  await expect(page.locator('.view-lines')).toContainText('context Child Care');
  await page.getByText('accs-determination-application.modeller', { exact: true }).click();
  const editor = page.locator('.editor-container .monaco-editor');
  await editor.click();
  await page.keyboard.press('Control+A');
  await page.keyboard.type('rml 1.0\nentity ACCS determination application\n  lifecycle ACCS determination application lifecycle\n    stage Draft\n    stage Submitted\n    stage Awaiting information\n  end\nend');

  await expect(page.locator('.react-flow__node', { hasText: 'Awaiting information' })).toBeVisible();
  await expect(page.getByRole('status')).toContainText('Ready');

  // Guards against the diagram-type selector silently truncating the view list to a stale
  // client-side default instead of the kinds the live API actually reports as supported.
  const viewSelect = page.getByRole('combobox').nth(0);
  const optionLabels = await viewSelect.locator('option').allTextContents();
  expect(optionLabels).toEqual(['BehaviourMap', 'Lifecycle', 'CausalityAndEventFlow', 'ContextMap', 'Structural', 'RuleDecision']);

  // Proves a newly-added projection kind (not just the pre-existing Lifecycle view) actually
  // renders a diagram end-to-end against the real deployed API.
  await viewSelect.selectOption({ label: 'ContextMap' });
  await page.getByRole('combobox').nth(1).selectOption({ label: 'Child Care' });
  await expect(page.locator('.react-flow__node', { hasText: 'Child Care' })).toBeVisible();
});
