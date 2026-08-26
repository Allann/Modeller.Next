import { expect, test, type Page } from '@playwright/test';

// Every scenario mocks Modeller.Api rather than hitting the live deployment —
// deterministic, offline-safe, and the only reliable way to trigger the
// service-failure path on demand. See docs/architecture/decisions/
// hosted-workspace-api.mdx for the request/response shape being mocked.
const SUPPORTED_VIEWS_RESPONSE = {
  apiVersion: '1.0',
  views: ['BehaviourMap', 'Lifecycle', 'CausalityAndEventFlow', 'ContextMap', 'Structural', 'RuleDecision'],
};
const ORDER_LIFECYCLE_ROOT = { id: 'order-lifecycle-root', kind: 'Lifecycle', name: 'Order lifecycle', slug: 'order-lifecycle' };

interface AnalyzeRequestBody {
  documents: { path: string; content: string }[];
  identity: { kind: 'ephemeral' } | { kind: 'durable'; version: string; documents: Record<string, string[]> };
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

async function mockExport(page: Page) {
  await page.route('**/v1/workspace/export', (route) => {
    const body = route.request().postDataJSON() as AnalyzeRequestBody;
    const documents = body.documents.map((document) => ({ ...document, content: `${document.content}\n# @id=test-identity\n` }));
    const identity = {
      kind: 'durable',
      version: '1.0',
      documents: Object.fromEntries(body.documents.map((document) => [document.path, ['test-identity']])),
    };
    route.fulfill({ json: { apiVersion: '1.0', diagnostics: [], documents, identity } });
  });
}

interface GenerateRequestBody {
  documents: { path: string; content: string }[];
  identity: { kind: 'ephemeral' } | { kind: 'durable'; version: string; documents: Record<string, string[]> };
  configuration: { generationContractVersion: string; logicalOutputRoot: string; profile?: string };
  templatePackId: string;
}

const GENERATED_ARTIFACTS = [
  { path: 'Order.g.cs', owner: 'order', packId: 'csharp/domain-project', templateId: 'entity', content: 'public partial class Order {}\n', digest: 'digest-order-1' },
  { path: 'Orderline.g.cs', owner: 'orderline', packId: 'csharp/domain-project', templateId: 'entity', content: 'public partial class Orderline {}\n', digest: 'digest-orderline-1' },
];

async function mockGenerate(page: Page, respond: (body: GenerateRequestBody) => Record<string, unknown> = () => ({
  apiVersion: '1.0',
  diagnostics: [],
  artifacts: GENERATED_ARTIFACTS,
})) {
  await page.route('**/v1/workspace/generate', (route) => {
    const body = route.request().postDataJSON() as GenerateRequestBody;
    route.fulfill({ json: respond(body) });
  });
}

async function mockCompletion(page: Page) {
  await page.route('**/v1/workspace/complete', (route) => route.fulfill({ json: {
    apiVersion: '1.0',
    items: [{
      label: 'Determine order readiness',
      kind: 'Rule',
      detail: "Reference the rule 'Determine order readiness' from this workspace.",
      insertText: 'Determine order readiness',
      replacementStartColumn: 13,
    }],
    diagnostics: [],
  } }));
}

function cleanResponse(body: AnalyzeRequestBody) {
  const projections = (body.projections ?? []).map((request) => ({
    id: request.id,
    succeeded: true,
    graph: { sourceRevision: 1, kind: request.kind, nodes: [{ id: 'stage:draft', role: 'stage', label: 'Draft', semanticIds: [] }], edges: [] },
    diagnostics: [],
  }));
  const hasOrderline = body.documents.some((document) => document.content.includes('entity Orderline'));
  const outline = [
    { id: 'behaviour', kind: 'Behaviour', name: 'Place order', location: { document: 'model/behaviours/place-order.modeller', line: 2, column: 1, length: 21 } },
    { id: 'order', kind: 'Entity', name: 'Order', location: { document: 'model/entities/order.modeller', line: 2, column: 1, length: 12 } },
    ...(hasOrderline ? [{ id: 'orderline', kind: 'Entity', name: 'Orderline', location: { document: 'model/entities/order.modeller', line: 4, column: 1, length: 16 } }] : []),
    { id: 'fact', kind: 'Fact', name: 'Payment is confirmed', location: { document: 'model/facts/payment-confirmation.modeller', line: 2, column: 1, length: 25 } },
    { id: 'lifecycle', kind: 'Lifecycle', name: 'Order lifecycle', ownerId: 'order', location: { document: 'model/entities/order.modeller', line: 3, column: 3, length: 27 } },
    { id: 'draft', kind: 'LifecycleStage', name: 'Draft', ownerId: 'lifecycle', location: { document: 'model/entities/order.modeller', line: 4, column: 5, length: 15 } },
    { id: 'placed', kind: 'LifecycleStage', name: 'Placed', ownerId: 'lifecycle', location: { document: 'model/entities/order.modeller', line: 5, column: 5, length: 16 } },
    { id: 'rule', kind: 'Rule', name: 'Determine order readiness', location: { document: 'model/rules/determine-order-readiness.modeller', line: 2, column: 1, length: 31 } },
  ];
  return { apiVersion: '1.0', diagnostics: [], roots: [ORDER_LIFECYCLE_ROOT], outline, summary: [
    { kind: 'Behaviour', count: 1 }, { kind: 'Entity', count: hasOrderline ? 2 : 1 }, { kind: 'Fact', count: 1 },
    { kind: 'Lifecycle', count: 1 }, { kind: 'LifecycleStage', count: 2 }, { kind: 'Rule', count: 1 },
  ], projections };
}

test('playground loads with the Ordering example, not a blank workbench', async ({ page }) => {
  await mockSupportedViews(page);
  await mockAnalyze(page, cleanResponse);

  await page.goto('/');

  await expect(page.getByText('order.modeller', { exact: true })).toBeVisible();
  await expect(page.locator('.editor-container .monaco-editor')).toBeVisible();
  await expect(page.locator('.view-lines')).toContainText('context Ordering');
  await expect(page.getByText('browser draft')).toBeVisible();
  await expect(page.getByRole('link', { name: 'RML syntax reference' })).toHaveAttribute(
    'href',
    'https://modeller.wiki/docs/reference/readable-modelling-language',
  );
  await expect(page.getByRole('status')).toContainText('Ready');
  await expect(page.getByLabel('Model explorer')).toContainText('Valid');
  await expect(page.getByRole('heading', { name: 'Entities' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'lifecycle Order lifecycle' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'lifecycle stage Draft' })).toBeVisible();
});

test('a second entity in one file appears in the model explorer and summary', async ({ page }) => {
  await mockSupportedViews(page);
  await mockAnalyze(page, cleanResponse);
  await page.goto('/');
  const footerHeight = await page.locator('.playground-status-line').evaluate((element) => element.getBoundingClientRect().height);

  await page.getByText('order.modeller', { exact: true }).click();
  const editor = page.locator('.editor-container .monaco-editor');
  await editor.click();
  await page.keyboard.press('Control+A');
  await page.keyboard.type('rml 1.0\nentity Order\nend\nentity Orderline\nend');

  await expect(page.getByRole('button', { name: 'entity Orderline' })).toBeVisible();
  await expect(page.getByLabel('Model explorer')).toContainText('2 entities');
  await expect(page.getByRole('status')).toContainText('Ready');
  await expect.poll(() => page.locator('.playground-status-line').evaluate((element) => element.getBoundingClientRect().height)).toBe(footerHeight);
});

test('model explorer groups nested concepts, scrolls independently, and navigates to a declaration', async ({ page }) => {
  await mockSupportedViews(page);
  await mockAnalyze(page, cleanResponse);
  await page.goto('/');

  const model = page.getByLabel('Model explorer');
  await expect(model).toHaveCSS('overflow-y', 'auto');
  await expect(page.getByRole('heading', { name: 'Entities' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Facts' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'lifecycle Order lifecycle' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'lifecycle stage Draft' })).toBeVisible();

  await page.getByRole('button', { name: 'fact Payment is confirmed' }).click();
  await expect(page.locator('.tab.active')).toContainText('payment-confirmation.modeller');
  await page.keyboard.press('End');
  await page.keyboard.type(' NAVIGATED');
  await expect(page.locator('.view-lines')).toContainText('NAVIGATED');
});

test('playground shows the Ordering lifecycle without requiring a root selection', async ({ page }) => {
  await mockSupportedViews(page);
  await mockAnalyze(page, cleanResponse);

  await page.goto('/');

  await expect(page.getByRole('combobox').nth(1)).toHaveValue(ORDER_LIFECYCLE_ROOT.id);
  await expect(page.locator('.react-flow__node')).toContainText('Draft');
});

test('every view kind reported by the API appears in the diagram-type selector', async ({ page }) => {
  await mockSupportedViews(page);
  await mockAnalyze(page, cleanResponse);

  await page.goto('/');
  await expect(page.getByRole('status')).toContainText('Ready');

  const viewSelect = page.getByRole('combobox').nth(0);
  const optionLabels = await viewSelect.locator('option').allTextContents();
  expect(optionLabels).toEqual(SUPPORTED_VIEWS_RESPONSE.views);
});

test('playground reuses discovered identities when it requests the selected projection', async ({ page }) => {
  await mockSupportedViews(page);
  await mockAnalyze(page, (body) => {
    const identity = { kind: 'durable' as const, version: '1.0', documents: {} };
    if (body.identity.kind === 'ephemeral') {
      return { apiVersion: '1.0', diagnostics: [], roots: [ORDER_LIFECYCLE_ROOT], projections: [], identity };
    }
    return cleanResponse(body);
  });

  await page.goto('/');

  await expect(page.locator('.react-flow__node')).toContainText('Draft');
});

test('analysis status stays in the fixed footer while a request is running', async ({ page }) => {
  await mockSupportedViews(page);
  await page.route('**/v1/workspace/analyze', async (route) => {
    const body = route.request().postDataJSON() as AnalyzeRequestBody;
    await new Promise((resolve) => setTimeout(resolve, 1_000));
    await route.fulfill({ json: cleanResponse(body) });
  });

  await page.goto('/');

  const statusLine = page.locator('.playground-status-line');
  await expect(statusLine).toContainText('Analysing…');
  await expect(statusLine).toContainText('browser draft');
  await expect(statusLine).toContainText('Ready');
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
  const editor = page.locator('.editor-container .monaco-editor');
  await editor.click();
  await page.keyboard.press('Control+A');
  await page.keyboard.type('INVALID_MARKER');

  await expect(page.locator('.problem-row')).toContainText('Unexpected token.');
  await expect(page.getByRole('status')).toContainText('1 problem');
  await expect(page.getByLabel('Model explorer')).not.toContainText('Valid');
});

test('context completion inserts a quoted workspace reference without changing indentation', async ({ page }) => {
  await mockSupportedViews(page);
  await mockAnalyze(page, cleanResponse);
  await mockCompletion(page);
  await page.goto('/');
  await page.getByText('place-order.modeller', { exact: true }).click();

  const editor = page.locator('.editor-container .monaco-editor');
  await editor.click();
  await page.keyboard.press('Control+A');
  await page.keyboard.type('rml 1.0\nbehaviour Place order\n  requires "');
  await page.keyboard.press('Control+Space');
  await expect(page.locator('.suggest-widget')).toContainText('Determine order readiness');
  await page.keyboard.press('Enter');

  await expect(page.locator('.view-lines')).toContainText('requires "Determine order readiness');
});

test('a rejected projection shows its diagnostic without an endless loading message', async ({ page }) => {
  await mockSupportedViews(page);
  await mockAnalyze(page, (body) => {
    if (!(body.projections ?? []).length) return cleanResponse(body);
    return {
      apiVersion: '1.0',
      diagnostics: [],
      roots: [ORDER_LIFECYCLE_ROOT],
      projections: [{ id: 'active', succeeded: false, diagnostics: [{ code: 'project.root.invalid', message: 'The selected root is invalid.' }] }],
    };
  });

  await page.goto('/');

  await expect(page.getByText('The selected root is invalid.')).toBeVisible();
  await expect(page.getByText('Loading…')).not.toBeVisible();
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
  const editor = page.locator('.editor-container .monaco-editor');
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
  const editor = page.locator('.editor-container .monaco-editor');
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
  await expect(page.locator('.editor-container .monaco-editor')).toBeVisible();
});

test('local-only filesystem/CLI-subprocess API routes are disabled in playground mode', async ({ request }) => {
  for (const path of ['/api/document?path=model/context.modeller', '/api/workspace', '/api/projection?view=Lifecycle&root=x', '/api/projection/roots?view=Lifecycle']) {
    const response = await request.get(path);
    expect(response.status(), path).toBe(404);
  }
});

test('a share link reproduces the edited model for a fresh recipient, without server-side persistence', async ({ page, browser }) => {
  await mockSupportedViews(page);
  await mockAnalyze(page, cleanResponse);

  await page.goto('/');
  await page.getByText('order.modeller', { exact: true }).click();
  const editor = page.locator('.editor-container .monaco-editor');
  await editor.click();
  await page.keyboard.press('Control+A');
  await page.keyboard.type('entity Shared Widget');
  await expect(page.locator('.view-lines')).toContainText('Shared Widget');

  await page.getByRole('button', { name: 'Share' }).click();
  const shareInput = page.getByRole('textbox', { name: 'Share link' });
  await expect(shareInput).toBeVisible();
  const shareUrl = await shareInput.inputValue();
  expect(shareUrl).toContain('#s=');

  // A fresh browser context has no sessionStorage from the sharer's tab — the only way the
  // recipient's page can reproduce the edit is by decoding the URL fragment itself.
  const recipientContext = await browser.newContext();
  try {
    const recipientPage = await recipientContext.newPage();
    await mockSupportedViews(recipientPage);
    await mockAnalyze(recipientPage, cleanResponse);

    await recipientPage.goto(shareUrl);
    await recipientPage.getByText('order.modeller', { exact: true }).click();

    await expect(recipientPage.locator('.view-lines')).toContainText('Shared Widget');
  } finally {
    await recipientContext.close();
  }
});

test('a malformed share link fails safely and falls back to the pristine example', async ({ page }) => {
  await mockSupportedViews(page);
  await mockAnalyze(page, cleanResponse);

  await page.goto('/#s=not-valid-base64!!!');

  await expect(page.getByRole('status')).toContainText("couldn't be read");
  await expect(page.locator('.view-lines')).toContainText('context Ordering');
});

test('an oversized share link fails safely', async ({ page }) => {
  await mockSupportedViews(page);
  await mockAnalyze(page, cleanResponse);

  await page.goto(`/#s=${'A'.repeat(7000)}`);

  await expect(page.getByRole('status')).toContainText('too large');
  await expect(page.locator('.view-lines')).toContainText('context Ordering');
});

test('an unsupported share-link version fails safely', async ({ page }) => {
  await mockSupportedViews(page);
  await mockAnalyze(page, cleanResponse);
  await page.goto('/');

  // Builds a validly-compressed fragment (same scheme as share-link.ts) but with a version this
  // build doesn't understand, to prove the version check itself — not just malformed input — is
  // enforced.
  const fragment = await page.evaluate(async () => {
    const payload = JSON.stringify({
      v: 2,
      documents: [{ path: 'model/context.modeller', content: 'rml 1.0\n' }],
      configuration: { generationContractVersion: '1.0', logicalOutputRoot: 'generated' },
    });
    const compressedStream = new Blob([new TextEncoder().encode(payload)]).stream().pipeThrough(new CompressionStream('gzip'));
    const compressed = new Uint8Array(await new Response(compressedStream).arrayBuffer());
    let binary = '';
    for (const byte of compressed) binary += String.fromCharCode(byte);
    const encoded = btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
    return `#s=${encoded}`;
  });

  await page.goto(`/${fragment}`);

  await expect(page.getByRole('status')).toContainText('newer version');
  await expect(page.locator('.view-lines')).toContainText('context Ordering');
});

test('downloading the workspace produces a zip with durable-identity content', async ({ page }) => {
  await mockSupportedViews(page);
  await mockAnalyze(page, cleanResponse);
  await mockExport(page);

  await page.goto('/');
  const downloadPromise = page.waitForEvent('download');
  await page.getByRole('button', { name: 'Download workspace' }).click();
  const download = await downloadPromise;

  expect(download.suggestedFilename()).toBe('modeller-workspace.zip');
  await expect(page.getByRole('status')).toContainText('durable identities');
});

test('the public playground has no workspace-directory control and refuses to load a local workspace', async ({ page, request }) => {
  await mockSupportedViews(page);
  await mockAnalyze(page, cleanResponse);
  await page.goto('/');

  await expect(page.getByLabel('Workspace directory path')).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Load workspace' })).toHaveCount(0);

  // Even if something tried to call the underlying route directly, playground mode's
  // localOnlyRouteGuard must refuse it — the visitor's session is otherwise untouched.
  const response = await request.post('/api/workspace', { data: { path: 'samples/child-care' } });
  expect(response.status()).toBe(404);
});

test.describe('generation preview panel (issue #135)', () => {
  test('on a narrow viewport, the generation preview is a tab alongside Diagram view', async ({ page }) => {
    await page.setViewportSize({ width: 1200, height: 800 });
    await mockSupportedViews(page);
    await mockAnalyze(page, cleanResponse);
    await mockGenerate(page);

    await page.goto('/');

    const tabbar = page.getByRole('tablist', { name: 'Diagram and generation preview' });
    await expect(tabbar).toBeVisible();
    const diagramTab = page.getByRole('tab', { name: 'Diagram' });
    const generationTab = page.getByRole('tab', { name: 'Generated files' });
    await expect(diagramTab).toHaveAttribute('aria-selected', 'true');
    // Wide-split panels never appear alongside the tab bar.
    await expect(page.locator('.generation-preview-pane')).toHaveCount(1);

    await generationTab.click();
    await expect(generationTab).toHaveAttribute('aria-selected', 'true');
    await expect(page.getByLabel('Generated file')).toBeVisible();
  });

  test('on a wide viewport, Diagram view and the generation preview are both visible as separate panels', async ({ page }) => {
    await page.setViewportSize({ width: 2200, height: 1000 });
    await mockSupportedViews(page);
    await mockAnalyze(page, cleanResponse);
    await mockGenerate(page);

    await page.goto('/');

    // No tab switcher when split — both panels are simply visible at once.
    await expect(page.getByRole('tablist', { name: 'Diagram and generation preview' })).toHaveCount(0);
    await expect(page.locator('.diagram-pane')).toBeVisible();
    await expect(page.locator('.generation-preview-pane')).toBeVisible();
    await expect(page.getByLabel('Generated file')).toBeVisible();
  });

  test('the file dropdown is populated from the generate response', async ({ page }) => {
    await page.setViewportSize({ width: 2200, height: 1000 });
    await mockSupportedViews(page);
    await mockAnalyze(page, cleanResponse);
    await mockGenerate(page);

    await page.goto('/');

    const fileSelect = page.getByLabel('Generated file');
    await expect(fileSelect).toBeVisible();
    // The first generate call fires after the 500ms debounce — poll rather than asserting
    // immediately after navigation.
    await expect.poll(() => fileSelect.locator('option').allTextContents()).toEqual(GENERATED_ARTIFACTS.map((artifact) => artifact.path));
  });

  test('a burst of rapid edits produces far fewer generate calls than edits, spaced at least the minimum interval apart', async ({ page }) => {
    await mockSupportedViews(page);
    await mockAnalyze(page, cleanResponse);

    // Playwright's fake clock (page.clock) conflicts with Monaco's own use of the `performance`
    // API — advancing it throws from inside Monaco's internals. So this exercises the real
    // GENERATE_MIN_INTERVAL_MS=5000 breaker in real time, but through a test-only hook
    // (window.__playgroundTestGenerateMinIntervalMs__, wired up in PlaygroundWorkbench.tsx) that
    // shortens the interval — otherwise this scenario would need to run for 5+ real seconds.
    const shortMinIntervalMs = 1_200;
    await page.addInitScript((intervalMs) => {
      window.__playgroundTestGenerateMinIntervalMs__ = intervalMs;
    }, shortMinIntervalMs);

    let generateCallCount = 0;
    const callTimestamps: number[] = [];
    await page.route('**/v1/workspace/generate', (route) => {
      generateCallCount += 1;
      callTimestamps.push(Date.now());
      route.fulfill({ json: { apiVersion: '1.0', diagnostics: [], artifacts: GENERATED_ARTIFACTS } });
    });

    await page.goto('/');

    // Let the initial mount's debounce (500ms) fire the first generate call.
    await expect.poll(() => generateCallCount).toBe(1);

    // A burst of edits, fired well inside the shortened minimum-interval window.
    await page.getByText('order.modeller', { exact: true }).click();
    const editor = page.locator('.editor-container .monaco-editor');
    await editor.click();
    await page.keyboard.press('End');
    const edits = ['A', 'B', 'C', 'D', 'E'];
    for (const character of edits) {
      await page.keyboard.type(character, { delay: 0 });
    }

    // The whole burst collapses into at most one trailing call, started no sooner than
    // shortMinIntervalMs after the first one — never once per edit.
    await expect.poll(() => generateCallCount, { timeout: 5_000 }).toBe(2);

    expect(generateCallCount).toBeLessThan(edits.length);
    expect(callTimestamps[1] - callTimestamps[0]).toBeGreaterThanOrEqual(shortMinIntervalMs - 50); // small tolerance for timer scheduling jitter
  });
});
