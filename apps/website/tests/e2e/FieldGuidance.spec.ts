import { test, expect, type Page } from '@playwright/test';
import type { InitiativeSessionDto } from '../../src/lib/initiativeTypes';
import { INITIATIVE_FIELD_GUIDANCE } from '../../src/lib/initiativeFieldGuidance';

// The Facilitator cockpit explains the selected Discover/Frame field, so a first-time Facilitator
// knows what each heading records and has a starting question. Modeller.Api is mocked via
// page.route, the same way RoleScopedSessionCredentials.spec.ts does it.

const SESSION_ID = '33333333-3333-3333-3333-333333333333';
const FACILITATOR_CREDENTIAL = 'facilitator-credential-token';
const API_BASE_URL = 'http://localhost:8080';

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

test.describe('Discover & Frame field guidance', () => {
  test('explains the default field and follows the selection', async ({ page }) => {
    await openCockpit(page);
    const guidance = page.locator('#field-guidance');

    await expect(guidance).toContainText('Problem statement');
    await expect(guidance).toContainText(INITIATIVE_FIELD_GUIDANCE.ProblemStatement.captures);

    await page.getByLabel('Field to ask about').selectOption('Risks');

    await expect(guidance).toContainText(INITIATIVE_FIELD_GUIDANCE.Risks.captures);
    await expect(guidance).toContainText(INITIATIVE_FIELD_GUIDANCE.Risks.exampleQuestion);
  });

  test('"Use this question" copies the example into the question box', async ({ page }) => {
    await openCockpit(page);
    await page.getByLabel('Field to ask about').selectOption('SuccessCriteria');

    await page.getByRole('button', { name: 'Use this question' }).click();

    await expect(page.getByPlaceholder('Question text')).toHaveValue(INITIATIVE_FIELD_GUIDANCE.SuccessCriteria.exampleQuestion);
  });
});
