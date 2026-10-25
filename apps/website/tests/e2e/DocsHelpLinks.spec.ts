import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { test, expect, type Page } from '@playwright/test';
import type { InitiativeSessionDto } from '../../src/lib/initiativeTypes';
import { DOCS_HELP, docsHelpUrl, type DocsHelpTopic } from '../../src/lib/docsHelp';

// The Discovery Session puts an info icon next to each step, so help is one click away. The icons
// link to explicit `[#id]` heading ids in the repo-root docs/ folder. Modeller.Api is mocked via
// page.route, the same way FieldGuidance.spec.ts does it.

const SESSION_ID = '44444444-4444-4444-4444-444444444444';
const FACILITATOR_CREDENTIAL = 'facilitator-credential-token';
const API_BASE_URL = 'http://localhost:8080';
const DOCS_ROOT = path.resolve(__dirname, '..', '..', '..', '..', 'docs');

function session(): InitiativeSessionDto {
  return {
    id: SESSION_ID,
    originalChangeRequest: 'Build a new approval system',
    participants: [
      { id: 'fac-1', displayName: 'Fiona Facilitator', role: 'Facilitator' },
      { id: 'de-1', displayName: 'Derek Expert', role: 'DomainExpert' },
    ],
    questions: [],
    responses: [],
    selectedInterventions: [],
    gateOverrides: [],
    latestDiscoveryGateEvaluation: null,
    latestShapeGateEvaluation: null,
    finalization: null,
  };
}

async function openCockpit(page: Page) {
  await page.route(`${API_BASE_URL}/**`, async (route) => {
    const url = new URL(route.request().url());
    if (url.pathname === '/v1/initiative/agent-status') {
      return route.fulfill({ json: { available: false, model: null, requiresApiKey: true, freeModel: null } });
    }
    if (url.pathname === `/v1/initiative/${SESSION_ID}`) return route.fulfill({ json: session() });
    return route.fulfill({ status: 404, json: { code: 'NotMocked', message: url.pathname } });
  });
  await page.goto(`/initiative/${SESSION_ID}?credential=${encodeURIComponent(FACILITATOR_CREDENTIAL)}`);
}

function readDocsPage(pagePath: string): string {
  const file = ['.md', '.mdx'].map((extension) => path.join(DOCS_ROOT, pagePath + extension)).find(existsSync);
  if (!file) throw new Error(`No docs page at docs/${pagePath}.md or .mdx`);
  return readFileSync(file, 'utf-8');
}

test.describe('Docs help links', () => {
  for (const [name, topic] of Object.entries(DOCS_HELP) as [string, DocsHelpTopic][]) {
    test(`"${name}" points at a docs page and heading id that exist`, () => {
      const markdown = readDocsPage(topic.path);
      if (topic.anchor) expect(markdown).toMatch(new RegExp(`^#+ .*\\[#${topic.anchor}\\]$`, 'm'));
    });
  }

  test('the Discover & Frame step links to its help sections', async ({ page }) => {
    await openCockpit(page);

    await expect(page.getByRole('link', { name: `Help: ${DOCS_HELP.questionLoop.label}` })).toHaveAttribute('href', docsHelpUrl('questionLoop'));
    await expect(page.getByRole('link', { name: `Help: ${DOCS_HELP.discoveryGate.label}` })).toHaveAttribute('href', docsHelpUrl('discoveryGate'));
    await expect(page.locator('#field-guidance').getByRole('link', { name: `Help: ${DOCS_HELP.discover.label}` })).toBeVisible();

    await page.getByLabel('Field to ask about').selectOption('Risks');

    await expect(page.locator('#field-guidance').getByRole('link', { name: `Help: ${DOCS_HELP.frame.label}` })).toHaveAttribute('href', docsHelpUrl('frame'));
  });

  test('the Shape step links to the Shape section', async ({ page }) => {
    await openCockpit(page);
    await page.getByRole('button', { name: '2. Shape' }).click();

    await expect(page.getByRole('link', { name: `Help: ${DOCS_HELP.shape.label}` }).first()).toHaveAttribute('href', docsHelpUrl('shape'));
  });
});
