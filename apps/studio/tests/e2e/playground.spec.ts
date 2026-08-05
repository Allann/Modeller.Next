import { expect, test, type Page } from '@playwright/test';

// Every scenario mocks Modeller.Api rather than hitting the live deployment —
// deterministic, offline-safe, and the only reliable way to trigger the
// service-failure path on demand. See docs/architecture/decisions/
// hosted-workspace-api.mdx for the request/response shape being mocked.
const SUPPORTED_VIEWS_RESPONSE = { apiVersion: '1.0', views: ['Lifecycle', 'RuleDecision'] };
const ORDER_LIFECYCLE_ROOT = { id: 'order-lifecycle-root', kind: 'Lifecycle', name: 'Order lifecycle', slug: 'order-lifecycle' };

interface AnalyzeRequestBody {
  documents: { path: string; content: string }[];
  projections?: { id: string; kind: string; roots: string[] }[];
}

async function mockSupportedViews(page: Page) {
  await page.route('**/v1/workspace/supported-views', (route) => route.fulfill({ json: SUPPORTED_VIEWS_RESPONSE }));
}

async function mockAnalyze(page: Page, respond: (body: AnalyzeRequestBody) => Record<string, unknown>) {
  await page.route('**/v1/workspace/analyze', (route) => {
    const body = route.request().postDataJSON() as AnalyzeRequestBody;
    route.fulfill({ json: respond(body) });
  });
}

function cleanResponse(body: AnalyzeRequestBody) {
  const projections = (body.projections ?? []).map((request) => ({
    id: request.id,
    succeeded: true,
    graph: { sourceRevision: 1, kind: request.kind, nodes: [{ id: 'stage:draft', role: 'stage', label: 'Draft', semanticIds: [] }], edges: [] },
    diagnostics: [],
  }));
  return { apiVersion: '1.0', diagnostics: [], roots: [ORDER_LIFECYCLE_ROOT], projections };
}

test('playground loads with the Ordering example, not a blank workbench', async ({ page }) => {
  await mockSupportedViews(page);
  await mockAnalyze(page, cleanResponse);

  await page.goto('/');

  await expect(page.getByText('order.modeller', { exact: true })).toBeVisible();
  await expect(page.locator('.monaco-editor')).toBeVisible();
  await expect(page.locator('.view-lines')).toContainText('context Ordering');
  await expect(page.getByText('browser draft')).toBeVisible();
});

test('editing the model re-analyzes and surfaces a source-mapped diagnostic', async ({ page }) => {
  await mockSupportedViews(page);
  await mockAnalyze(page, (body) => {
    const edited = body.documents.find((document) => document.content.includes('INVALID_MARKER'));
    if (!edited) return cleanResponse(body);
    return {
      apiVersion: '1.0',
      diagnostics: [{ code: 'rml.parse-error', message: 'Unexpected token.', location: { document: edited.path, line: 1, column: 1, length: 4 } }],
      roots: [ORDER_LIFECYCLE_ROOT],
      projections: [],
    };
  });

  await page.goto('/');
  await page.getByText('order.modeller', { exact: true }).click();
  const editor = page.locator('.monaco-editor');
  await editor.click();
  await page.keyboard.press('Control+A');
  await page.keyboard.type('INVALID_MARKER');

  await expect(page.locator('.problem-row')).toContainText('Unexpected token.');
});

test('selecting a projection root renders its graph', async ({ page }) => {
  await mockSupportedViews(page);
  await mockAnalyze(page, cleanResponse);

  await page.goto('/');
  await expect(page.getByRole('combobox').nth(1)).not.toBeDisabled();
  await page.getByRole('combobox').nth(1).selectOption({ label: 'Order lifecycle' });

  await expect(page.locator('.react-flow__node')).toContainText('Draft');
});

test('reset discards edits and restores the pristine example', async ({ page }) => {
  await mockSupportedViews(page);
  await mockAnalyze(page, cleanResponse);

  await page.goto('/');
  await page.getByText('order.modeller', { exact: true }).click();
  const editor = page.locator('.monaco-editor');
  await editor.click();
  await page.keyboard.press('Control+A');
  await page.keyboard.type('entity Something Else');
  await expect(page.locator('.view-lines')).toContainText('Something Else');

  await page.getByRole('button', { name: 'Reset example' }).click();

  await expect(page.locator('.view-lines')).toContainText('context Ordering');
});

test('a refresh restores the in-progress draft from sessionStorage, not the pristine example', async ({ page }) => {
  await mockSupportedViews(page);
  await mockAnalyze(page, cleanResponse);

  await page.goto('/');
  await page.getByText('order.modeller', { exact: true }).click();
  const editor = page.locator('.monaco-editor');
  await editor.click();
  await page.keyboard.press('Control+A');
  await page.keyboard.type('entity Something Else');
  await expect(page.locator('.view-lines')).toContainText('Something Else');
  // The debounced analyze call (500ms) also persists the draft to sessionStorage — give it a beat.
  await page.waitForTimeout(600);

  await page.reload();
  await page.getByText('order.modeller', { exact: true }).click();

  await expect(page.locator('.view-lines')).toContainText('Something Else');
});

test('an analysis-service failure surfaces a status banner without crashing the app', async ({ page }) => {
  await mockSupportedViews(page);
  await page.route('**/v1/workspace/analyze', (route) => route.fulfill({ status: 503, json: { apiVersion: '1.0', diagnostics: [], roots: [], projections: [] } }));

  await page.goto('/');

  await expect(page.getByRole('status')).toContainText("Couldn't reach the analysis service");
  await expect(page.locator('.monaco-editor')).toBeVisible();
});

test('local-only filesystem/CLI-subprocess API routes are disabled in playground mode', async ({ request }) => {
  for (const path of ['/api/document?path=model/context.modeller', '/api/workspace', '/api/projection?view=Lifecycle&root=x', '/api/projection/roots?view=Lifecycle']) {
    const response = await request.get(path);
    expect(response.status(), path).toBe(404);
  }
});
