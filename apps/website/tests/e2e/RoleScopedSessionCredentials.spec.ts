import { test, expect, type Page, type Route } from '@playwright/test';
import type { InitiativeSessionDto } from '../../src/lib/initiativeTypes';

// Issue #146 QA procedure: tests/Modeller.Api.Acceptance/Features/RoleScopedSessionCredentials.qa.md
//
// Parts 1-5 of that procedure are server-side role enforcement (a facilitator-only action refused
// for the Domain Expert credential, bad/expired/cross-session credentials refused, the read
// endpoint honouring the credential over a claimed role, ...). Those are already exercised
// end-to-end — the same HTTP surface, the same production pipeline — by the Gherkin acceptance
// spec in tests/Modeller.Api.Acceptance/Features/RoleScopedSessionCredentials.feature via
// WebApplicationFactory<Program>. Re-driving them here through a browser would only re-assert the
// same server behaviour with a slower, flakier client; it would not test anything the website adds.
//
// Part 6 is different: it is about what apps/website itself does with the two credentials — the
// Facilitator cockpit page (src/app/initiative/[id]/page.tsx) offering a "Domain Expert" share
// link, and the Domain Expert respond page (src/app/initiative/[id]/respond/page.tsx) only ever
// being driven by a Domain Expert credential. That is client-side wiring (which credential a page
// reads from the URL/sessionStorage, which header it puts on its own requests, what it renders)
// that no API-only test can see. This spec drives real browser pages against a mocked
// Modeller.Api (via page.route) to check exactly that wiring, deterministically and without
// requiring a running .NET process — the server enforcement the mock stands in for is what the
// Gherkin spec above already proves.

const SESSION_ID = '11111111-1111-1111-1111-111111111111';
const QUESTION_ID = '22222222-2222-2222-2222-222222222222';
const FACILITATOR_CREDENTIAL = 'facilitator-credential-token';
const DOMAIN_EXPERT_CREDENTIAL = 'domain-expert-credential-token';
const API_BASE_URL = 'http://localhost:8080';

function baseSession(): InitiativeSessionDto {
  return {
    id: SESSION_ID,
    originalChangeRequest: 'Build a new approval system',
    participants: [
      { id: 'fac-1', displayName: 'Fiona Facilitator', role: 'Facilitator' },
      { id: 'de-1', displayName: 'Derek Expert', role: 'DomainExpert' },
    ],
    questions: [
      { id: QUESTION_ID, text: 'Who approves a purchase today?', proposedBy: 'fac-1', authorRole: 'Facilitator', field: 'ProblemStatement', status: 'Sent' },
    ],
    responses: [],
    selectedInterventions: [],
    gateOverrides: [],
    latestDiscoveryGateEvaluation: null,
    latestShapeGateEvaluation: null,
    finalization: null,
  };
}

/** Records every credential this mocked API observed, keyed by request path, so assertions can
 * confirm which credential a page actually put on the wire — not just what it rendered. */
type CredentialLog = { path: string; method: string; credential: string | null }[];

async function mockInitiativeApi(page: Page, log: CredentialLog) {
  await page.route(`${API_BASE_URL}/**`, async (route: Route) => {
    const request = route.request();
    const url = new URL(request.url());
    const credential = request.headers()['x-initiative-credential'] ?? null;
    log.push({ path: url.pathname, method: request.method(), credential });

    if (url.pathname === '/v1/initiative/agent-status') {
      return route.fulfill({ json: { available: false, model: null, requiresApiKey: true, freeModel: null } });
    }

    if (url.pathname === `/v1/initiative/${SESSION_ID}` && request.method() === 'GET') {
      if (credential === FACILITATOR_CREDENTIAL) return route.fulfill({ json: baseSession() });
      if (credential === DOMAIN_EXPERT_CREDENTIAL) {
        // The real server strips facilitator-only data from the Domain Expert projection
        // (Modeller.Api.Initiative.InitiativeSessionMapper.ToDomainExpertDto) — mirrored here only
        // to the extent this spec's own assertions depend on it (no gate/intervention data).
        const session = baseSession();
        return route.fulfill({ json: { ...session, latestDiscoveryGateEvaluation: null, latestShapeGateEvaluation: null } });
      }
      return route.fulfill({ status: 403, json: { code: 'InvalidCredential', message: 'This credential is not valid for this session.' } });
    }

    if (url.pathname === `/v1/initiative/${SESSION_ID}/questions/${QUESTION_ID}/responses` && request.method() === 'POST') {
      if (credential !== DOMAIN_EXPERT_CREDENTIAL) {
        return route.fulfill({ status: 403, json: { code: 'FacilitatorOnlyAction', message: 'Only the Domain Expert can respond to a question.' } });
      }
      const session = baseSession();
      session.responses.push({ id: 'resp-1', questionId: QUESTION_ID, text: 'Line managers, currently.', status: 'Pending' });
      return route.fulfill({ json: session });
    }

    if (url.pathname === `/v1/initiative/${SESSION_ID}/questions/${QUESTION_ID}/send` && request.method() === 'POST') {
      if (credential !== FACILITATOR_CREDENTIAL) {
        return route.fulfill({ status: 403, json: { code: 'FacilitatorOnlyAction', message: 'Only the Facilitator can send a question.' } });
      }
      return route.fulfill({ json: baseSession() });
    }

    return route.fulfill({ status: 404, json: { code: 'NotFound', message: `Unhandled mock route: ${request.method()} ${url.pathname}` } });
  });
}

test.describe('Issue #146 QA Part 6 — the links the website hands out', () => {
  test('step 21: the cockpit\'s "Domain Expert" share link carries only the Domain Expert credential, and that link only ever drives Domain Expert actions', async ({ page }) => {
    const log: CredentialLog = [];
    await mockInitiativeApi(page, log);

    // Seed this tab's session storage the same way the create flow does (src/app/page.tsx,
    // saveInitiativeCredentials) so the cockpit can resolve the Domain Expert link.
    await page.addInitScript(
      ([id, fac, de]) => {
        window.sessionStorage.setItem(`modeller:initiative:credential:facilitator:${id}`, fac);
        window.sessionStorage.setItem(`modeller:initiative:credential:domain-expert:${id}`, de);
      },
      [SESSION_ID, FACILITATOR_CREDENTIAL, DOMAIN_EXPERT_CREDENTIAL],
    );

    await page.goto(`/initiative/${SESSION_ID}?credential=${encodeURIComponent(FACILITATOR_CREDENTIAL)}`);
    await expect(page.getByRole('heading', { name: 'Build a new approval system' })).toBeVisible();

    // The rendered share link must embed the Domain Expert credential — never the Facilitator's.
    const shareLink = page.getByRole('link', { name: new RegExp(`/initiative/${SESSION_ID}/respond`) });
    const href = await shareLink.getAttribute('href');
    expect(href).toContain(`credential=${encodeURIComponent(DOMAIN_EXPERT_CREDENTIAL)}`);
    expect(href).not.toContain(FACILITATOR_CREDENTIAL);

    // Follow that exact link, as the Domain Expert would after receiving it — a fresh navigation,
    // so only requests from this point on are the respond page's own. (The cockpit page's own
    // earlier GET, above, legitimately used the Facilitator credential — that's a different page.)
    const respondPageLogStart = log.length;
    await page.goto(href!);
    await expect(page.getByText('Who approves a purchase today?')).toBeVisible();
    const respondPageLog = log.slice(respondPageLogStart);

    // The respond page must have fetched the session using the Domain Expert credential, never
    // the Facilitator's — confirming the link "on its own" only ever behaves as the Domain Expert.
    const respondGet = respondPageLog.find((entry) => entry.path === `/v1/initiative/${SESSION_ID}` && entry.credential === DOMAIN_EXPERT_CREDENTIAL);
    expect(respondGet).toBeTruthy();
    expect(respondPageLog.some((entry) => entry.path === `/v1/initiative/${SESSION_ID}` && entry.credential === FACILITATOR_CREDENTIAL)).toBe(false);

    // The page itself offers no facilitator-only action (Part 1's actions) — there is nothing on
    // this page that could even attempt one, per the acceptance criteria's "enforced on the wire,
    // not just hidden by the page" plus the corresponding UI simply not existing here.
    for (const facilitatorOnlyLabel of ['Send to Domain Expert', 'Reject', 'Accept', 'Finalize', 'Reopen']) {
      await expect(page.getByRole('button', { name: facilitatorOnlyLabel })).toHaveCount(0);
    }

    // Submitting the response drives the mocked API with the Domain Expert credential.
    await page.getByPlaceholder('Your answer…').fill('Line managers, currently.');
    await page.getByRole('button', { name: 'Submit response' }).click();
    await expect(page.getByRole('button', { name: 'Submitting…' })).toHaveCount(0);
    const submitCall = log.find((entry) => entry.path === `/v1/initiative/${SESSION_ID}/questions/${QUESTION_ID}/responses`);
    expect(submitCall?.credential).toBe(DOMAIN_EXPERT_CREDENTIAL);
  });

  test('step 22: the Facilitator\'s own link only ever behaves as the Facilitator, and a fresh tab that never saw the create response cannot get at the Domain Expert credential', async ({ page }) => {
    const log: CredentialLog = [];
    await mockInitiativeApi(page, log);

    // Deliberately do NOT seed sessionStorage — this simulates "given to someone else": a browser
    // tab that only ever received the Facilitator's own link, never the original create response.
    await page.goto(`/initiative/${SESSION_ID}?credential=${encodeURIComponent(FACILITATOR_CREDENTIAL)}`);
    await expect(page.getByRole('heading', { name: 'Build a new approval system' })).toBeVisible();

    // It renders the full Facilitator cockpit (Part 2: the Facilitator's link still works as
    // before) — e.g. it can act on the sent question via the Facilitator-only "Propose" form
    // being present, and the fetched view is the full one (proven by the GET credential below).
    await expect(page.getByRole('button', { name: 'Propose' })).toBeVisible();

    // Without ever having seen the Domain Expert credential, this tab cannot present the Domain
    // Expert share link — it must show the "return to that tab" fallback, not a stale or guessed
    // credential. This is the concrete browser-level guarantee behind "only ever behaves as the
    // Facilitator, never as the Domain Expert": this tab structurally cannot leak or fabricate
    // the other role's credential.
    await expect(page.getByText('The Domain Expert link is only available in the browser tab')).toBeVisible();
    await expect(page.locator('a[href*="/respond"]')).toHaveCount(0);

    // Every request this page made used the Facilitator credential — never the Domain Expert's,
    // which this tab never even had access to.
    expect(log.length).toBeGreaterThan(0);
    expect(log.every((entry) => entry.credential === null || entry.credential === FACILITATOR_CREDENTIAL)).toBe(true);
  });
});
