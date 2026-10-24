import { test, expect, type Page, type Route } from '@playwright/test';
import canonicalPrompts from '../../../../src/Modeller.Initiative.OpenAICompatible/agent-prompts.json';
import { CHECKS_BY_GATE, type InitiativeSessionDto } from '../../src/lib/initiativeTypes';
import { evaluateGateLocally, judgementPrompt } from '../../src/lib/localAgent/localGate';
import { startWithFallback, type CacheBackend, type EngineStartSteps } from '../../src/lib/localAgent/storageFallback';
import {
  AGENT_PROMPTS,
  LOCAL_MAX_PROMPT_CHARACTERS,
  LocalAgentError,
  evaluateGatePrompt,
  parseGateEvaluation,
  parseInterventions,
  parseQuestion,
  proposeQuestionPrompt,
  renderUser,
  structuredFieldsMarkdown,
} from '../../src/lib/localAgent/prompts';

// The in-browser local model. WebGPU and an 875 MB model are not available here, so the cockpit
// tests install a fake engine through window.__modellerLocalEngine (see src/lib/localAgent/engine.ts)
// and check the wiring: which prompt reaches the model, and what the cockpit sends to the API.

const SESSION_ID = '44444444-4444-4444-4444-444444444444';
const CREDENTIAL = 'facilitator-credential-token';
const API_BASE_URL = 'http://localhost:8080';

function session(overrides: Partial<InitiativeSessionDto> = {}): InitiativeSessionDto {
  return {
    id: SESSION_ID,
    originalChangeRequest: 'Build a new approval system',
    participants: [
      { id: 'fac-1', displayName: 'Fiona Facilitator', role: 'Facilitator' },
      { id: 'de-1', displayName: 'Derek Expert', role: 'DomainExpert' },
    ],
    questions: [{ id: 'q-1', text: 'Who approves today?', proposedBy: 'fac-1', authorRole: 'Facilitator', field: 'AffectedUsers', status: 'Sent' }],
    responses: [{ id: 'r-1', questionId: 'q-1', text: 'Site supervisors and estimators.', status: 'Accepted' }],
    selectedInterventions: [],
    gateOverrides: [],
    latestDiscoveryGateEvaluation: null,
    latestShapeGateEvaluation: null,
    finalization: null,
    ...overrides,
  } as InitiativeSessionDto;
}

test.describe('local agent prompts and parsing', () => {
  test('uses the server prompts unchanged', () => {
    expect(AGENT_PROMPTS).toEqual(canonicalPrompts);
  });

  test('renders placeholders once, like AgentPromptTemplate.RenderUser', () => {
    expect(renderUser('Field: {field}\nAnswer: {answer}\nOther: {unknown}', { field: 'Risks', answer: 'We said {field} twice' }))
      .toBe('Field: Risks\nAnswer: We said {field} twice\nOther: {unknown}');
  });

  test('builds the same structured-fields text as InitiativeStructuredFields.ToMarkdown', () => {
    const withIntervention = session({
      selectedInterventions: [
        { id: 'i-1', type: 'Process', description: 'One approver', rationale: 'Cuts delay', continuesToDesignWorkspace: false, designWorkspaceReference: null },
      ],
    });
    expect(structuredFieldsMarkdown(withIntervention)).toBe(
      [
        '## Original Change Request',
        '- Build a new approval system',
        '',
        '## Affected Users',
        '- Site supervisors and estimators.',
        '',
        '## Selected Interventions',
        '- [Process] One approver — Cuts delay',
      ].join('\n'),
    );
  });

  test('asks for the target field and constrains the answer to {"text"}', () => {
    const prompt = proposeQuestionPrompt(session(), 'Risks');
    expect(prompt.system).toBe(canonicalPrompts.proposeQuestion.system);
    expect(prompt.user).toContain('Target field: Risks');
    expect(prompt.schema).toMatchObject({ required: ['text'] });
  });

  test('requires every check of the gate', () => {
    const prompt = evaluateGatePrompt(session(), 'Shape');
    expect(prompt.schema).toMatchObject({ properties: { checks: { minItems: 2, maxItems: 2 } } });
  });

  test('refuses an Initiative that is too long for the local model', () => {
    const huge = session({ originalChangeRequest: 'x'.repeat(LOCAL_MAX_PROMPT_CHARACTERS) });
    expect(() => proposeQuestionPrompt(huge, 'Risks')).toThrow(LocalAgentError);
  });

  test('parses answers with the same rules as the server', () => {
    expect(parseQuestion('```json\n{"text": "Who signs off?"}\n```')).toBe('Who signs off?');
    expect(() => parseQuestion('{"text": ""}')).toThrow(LocalAgentError);
    expect(() => parseQuestion('not json')).toThrow(LocalAgentError);

    expect(parseInterventions('{"suggestions":[{"type":"noaction","description":"Keep it","rationale":"Baseline"}]}'))
      .toEqual([{ type: 'NoAction', description: 'Keep it', rationale: 'Baseline' }]);
    expect(() => parseInterventions('{"suggestions":[{"type":"Magic","description":"x","rationale":"y"}]}')).toThrow(LocalAgentError);
    expect(() => parseInterventions('{"suggestions":[]}')).toThrow(LocalAgentError);

    expect(parseGateEvaluation('{"checks":[{"check":"NoActionWasConsidered","passed":false,"reason":"Missing"}],"recommendedQuestion":{"text":"What if we do nothing?","field":"risks"}}'))
      .toEqual({ results: [{ check: 'NoActionWasConsidered', passed: false, reason: 'Missing' }], recommendedQuestion: { text: 'What if we do nothing?', field: 'Risks' } });
    expect(() => parseGateEvaluation('{"checks":[{"check":"NoActionWasConsidered","passed":"no","reason":"x"}]}')).toThrow(LocalAgentError);
    expect(() => parseGateEvaluation('{"checks":[],"recommendedQuestion":null}')).toThrow(LocalAgentError);
  });
});

test.describe('local gate', () => {
  const full = session({
    questions: (['ProblemStatement', 'AffectedUsers', 'PainPoints', 'Outcomes', 'SuccessCriteria', 'NonGoals', 'Constraints', 'Assumptions', 'OpenQuestions'] as const)
      .map((field, i) => ({ id: `q${i}`, text: 'Q?', proposedBy: 'fac-1', authorRole: 'Facilitator', field, status: 'Sent' })),
    responses: Array.from({ length: 9 }, (_, i) => ({ id: `r${i}`, questionId: `q${i}`, text: `Answer ${i}`, status: 'Accepted' })),
  });

  test('asks the model only for judgement checks whose field has content', () => {
    expect(judgementPrompt(full, 'Discovery')?.checks).toEqual([
      'ProblemStatementDescribesBusinessProblem', 'PainPointsAreConcrete', 'OutcomesAreObservable',
      'SuccessCriteriaAreUnderstandable', 'NoUnresolvedSolutionLedLanguage',
    ]);
    expect(judgementPrompt(session(), 'Discovery')?.checks).toEqual(['NoUnresolvedSolutionLedLanguage']);
  });

  test('decides presence checks exactly, even when the model disagrees', async () => {
    const evaluation = await evaluateGateLocally(full, 'Discovery', async () => JSON.stringify({
      checks: [
        { check: 'ProblemStatementDescribesBusinessProblem', passed: true, reason: 'Clear.' },
        { check: 'PainPointsAreConcrete', passed: false, reason: 'No numbers.' },
        { check: 'OutcomesAreObservable', passed: true, reason: 'Observable.' },
        { check: 'SuccessCriteriaAreUnderstandable', passed: true, reason: 'Measurable.' },
        { check: 'NoUnresolvedSolutionLedLanguage', passed: true, reason: 'None.' },
      ],
      recommendedQuestion: null,
    }));

    const failed = evaluation.results.filter((r) => !r.passed).map((r) => r.check);
    expect(failed).toEqual(['PainPointsAreConcrete', 'RisksAreListed']);
    expect(evaluation.recommendedQuestion?.field).toBe('PainPoints');
    expect(evaluation.results.find((r) => r.check === 'AffectedUsersNamed')?.reason).toBe('Affected users: 1 accepted answer.');
  });

  test('a check the model skipped is reported as not evaluated, never as passed', async () => {
    const evaluation = await evaluateGateLocally(session(), 'Shape', async () =>
      JSON.stringify({ checks: [{ check: 'SelectedTechnologyInterventionsHaveRationale', passed: false, reason: 'x' }], recommendedQuestion: null }));

    expect(evaluation.results).toEqual([
      { check: 'SelectedTechnologyInterventionsHaveRationale', passed: true, reason: 'No Technology intervention is selected.' },
      { check: 'NoActionWasConsidered', passed: false, reason: 'The local model did not evaluate this check.' },
    ]);
    expect(evaluation.recommendedQuestion).toBeNull();
  });

  test('a Technology intervention without a rationale fails exactly', async () => {
    const withTechnology = session({
      selectedInterventions: [{ id: 'i', type: 'Technology', description: 'An app', rationale: ' ', continuesToDesignWorkspace: true, designWorkspaceReference: null }],
    });
    const evaluation = await evaluateGateLocally(withTechnology, 'Shape', async () =>
      JSON.stringify({ checks: [{ check: 'NoActionWasConsidered', passed: true, reason: 'Considered.' }], recommendedQuestion: null }));

    expect(evaluation.results[0]).toEqual({ check: 'SelectedTechnologyInterventionsHaveRationale', passed: false, reason: '1 selected Technology intervention(s) have no rationale.' });
  });
});

test.describe('local model storage', () => {
  const engine = { complete: async () => '{}' };
  /** Records each step; `failures` says what create() throws for each backend. */
  function steps(failures: Partial<Record<CacheBackend, unknown>>, cached: CacheBackend | null = null) {
    const log: string[] = [];
    const s: EngineStartSteps = {
      cachedBackend: async () => cached,
      ensureStorageForDownload: async () => { log.push('check quota'); },
      create: async (backend) => {
        log.push(`create ${backend}`);
        if (failures[backend] !== undefined) throw failures[backend];
        return engine;
      },
      removePartialDownload: async (backend) => { log.push(`remove ${backend}`); },
      onRetry: () => { log.push('retry'); },
    };
    return { s, log };
  }

  test('a downloaded model starts from its own backend without a quota check', async () => {
    const { s, log } = steps({}, 'indexeddb');
    expect(await startWithFallback(s)).toBe(engine);
    expect(log).toEqual(['create indexeddb']);
  });

  test('a quota error from the worker (a plain string) falls back to IndexedDB', async () => {
    const { s, log } = steps({ cache: 'QuotaExceededError: Quota exceeded.' });
    expect(await startWithFallback(s)).toBe(engine);
    expect(log).toEqual(['check quota', 'create cache', 'remove cache', 'retry', 'create indexeddb']);
  });

  test('when both backends refuse, both partial downloads are removed and the reason is explained', async () => {
    const quota = new DOMException('Quota exceeded.', 'QuotaExceededError');
    const { s, log } = steps({ cache: quota, indexeddb: quota });
    await expect(startWithFallback(s)).rejects.toThrow('The browser refused to store the local model');
    expect(log).toEqual(['check quota', 'create cache', 'remove cache', 'retry', 'create indexeddb', 'remove indexeddb']);
  });

  test('other errors are passed on without a retry', async () => {
    const { s, log } = steps({ cache: 'OperationError: requestDevice failed' });
    await expect(startWithFallback(s)).rejects.toBe('OperationError: requestDevice failed');
    expect(log).toEqual(['check quota', 'create cache']);
  });
});

type ApiCall = { method: string; path: string; body: unknown; apiKey: string | null };

/** Opens the cockpit with a fake local engine that answers each task from `answers` and records
 * every prompt it receives. The Gateway is configured too, so the source choice is offered. */
async function openCockpit(page: Page, answers: { question?: string; interventions?: string; gate?: string }) {
  const calls: ApiCall[] = [];
  await page.addInitScript((fakeAnswers) => {
    const prompts: { system: string; user: string }[] = [];
    (window as unknown as { __prompts: typeof prompts }).__prompts = prompts;
    window.__modellerLocalEngine = {
      async complete(prompt) {
        prompts.push({ system: prompt.system, user: prompt.user });
        if (prompt.system.includes('Prompted Question')) return fakeAnswers.question ?? '{}';
        if (prompt.system.includes('candidate')) return fakeAnswers.interventions ?? '{}';
        return fakeAnswers.gate ?? '{}';
      },
    };
  }, answers);

  await page.route(`${API_BASE_URL}/**`, async (route: Route) => {
    const request = route.request();
    const url = new URL(request.url());
    calls.push({ method: request.method(), path: url.pathname, body: request.postDataJSON(), apiKey: request.headers()['x-agent-api-key'] ?? null });
    if (url.pathname === '/v1/initiative/agent-status') {
      return route.fulfill({ json: { available: true, model: 'openai/gpt-5.4-mini', requiresApiKey: false, freeModel: 'reflection/beam-free' } });
    }
    if (url.pathname.startsWith(`/v1/initiative/${SESSION_ID}`)) return route.fulfill({ json: session() });
    return route.fulfill({ status: 404, json: { code: 'NotMocked', message: url.pathname } });
  });

  await page.goto(`/initiative/${SESSION_ID}?credential=${encodeURIComponent(CREDENTIAL)}`);
  return calls;
}

test.describe('cockpit with the local model', () => {
  test('starts only after consent, then offers a choice between the local model and the Gateway', async ({ page }) => {
    await openCockpit(page, {});
    const panel = page.getByRole('region', { name: 'Local AI model' });

    await expect(panel).toContainText('1.8 GB');
    await expect(panel).toContainText('Built with Llama');
    await panel.getByRole('button', { name: /Download .* and start the local model/ }).click();

    await expect(panel).toContainText('is ready');
    await expect(panel.getByRole('radio', { name: 'This device (local model)' })).toBeChecked();
    await expect(panel.getByRole('radio', { name: 'AI Gateway (higher quality)' })).not.toBeChecked();
  });

  test('stops before the download when the browser cannot store the model', async ({ page }) => {
    // The real engine path (no fake engine): WebGPU is present, but this site may store only 0.5 GB.
    const modelRequests: string[] = [];
    page.on('request', (request) => {
      if (/huggingface\.co|hf\.co|raw\.githubusercontent\.com/.test(request.url())) modelRequests.push(request.url());
    });
    await page.addInitScript(() => {
      Object.defineProperty(navigator, 'gpu', { value: {}, configurable: true });
      Object.defineProperty(navigator, 'storage', {
        value: { estimate: async () => ({ quota: 500e6, usage: 0 }), persist: async () => false },
        configurable: true,
      });
    });
    await page.route(`${API_BASE_URL}/**`, (route: Route) => {
      const url = new URL(route.request().url());
      return url.pathname === '/v1/initiative/agent-status'
        ? route.fulfill({ json: { available: false, model: null, requiresApiKey: true, freeModel: null } })
        : route.fulfill({ json: session() });
    });
    await page.goto(`/initiative/${SESSION_ID}?credential=${encodeURIComponent(CREDENTIAL)}`);
    const panel = page.getByRole('region', { name: 'Local AI model' });

    await panel.getByRole('button', { name: /Download .* and start the local model/ }).click();

    await expect(panel.getByRole('alert')).toContainText('deletes site data when it closes');
    await expect(panel.getByRole('alert')).toContainText('only about 0.5 GB');
    expect(modelRequests).toEqual([]);
  });

  test('a blank question is written by the local model and saved as ordinary text', async ({ page }) => {
    const calls = await openCockpit(page, { question: '{"text": "What could go wrong if nothing changes?"}' });
    await page.getByRole('button', { name: /start the local model/ }).click();

    await page.getByLabel('Field to ask about').selectOption('Risks');
    await page.getByRole('button', { name: 'Propose' }).click();

    await expect.poll(() => calls.find((c) => c.method === 'POST' && c.path.endsWith('/questions'))?.body)
      .toEqual({ field: 'Risks', text: 'What could go wrong if nothing changes?' });
    const prompts = await page.evaluate(() => (window as unknown as { __prompts: { user: string }[] }).__prompts);
    expect(prompts[0].user).toContain('Target field: Risks');
    expect(prompts[0].user).toContain('- Site supervisors and estimators.');
  });

  test('choosing the Gateway sends the blank question to the server instead', async ({ page }) => {
    const calls = await openCockpit(page, { question: '{"text": "unused"}' });
    await page.getByRole('button', { name: /start the local model/ }).click();
    await page.getByRole('radio', { name: 'AI Gateway (higher quality)' }).check();

    await page.getByRole('button', { name: 'Propose' }).click();

    await expect.poll(() => calls.find((c) => c.method === 'POST' && c.path.endsWith('/questions'))?.body)
      .toEqual({ field: 'ProblemStatement', text: null });
  });

  test('a local gate evaluation records every check and the guidance question for the first gap', async ({ page }) => {
    const calls = await openCockpit(page, {
      gate: JSON.stringify({
        checks: [{ check: 'NoUnresolvedSolutionLedLanguage', passed: true, reason: 'No solution is assumed.' }],
        recommendedQuestion: null,
      }),
    });
    await page.getByRole('button', { name: /start the local model/ }).click();

    await page.getByRole('region', { name: 'Discovery Gate' }).getByRole('button', { name: 'Ask AI to evaluate' }).click();

    await expect.poll(() => calls.find((c) => c.path.endsWith('/gate-evaluations'))?.body).toMatchObject({
      kind: 'Discovery',
      recommendedQuestion: { text: 'What problem would the requested change solve?', field: 'ProblemStatement' },
    });
    const body = calls.find((c) => c.path.endsWith('/gate-evaluations'))!.body as { manualResults: { check: string; passed: boolean }[] };
    expect(body.manualResults.map((r) => r.check)).toEqual(CHECKS_BY_GATE.Discovery);
    expect(body.manualResults.find((r) => r.check === 'AffectedUsersNamed')?.passed).toBe(true);
    expect(body.manualResults.find((r) => r.check === 'RisksAreListed')?.passed).toBe(false);
    expect(body.manualResults.find((r) => r.check === 'NoUnresolvedSolutionLedLanguage')?.passed).toBe(true);
    const prompts = await page.evaluate(() => (window as unknown as { __prompts: { user: string }[] }).__prompts);
    expect(prompts.at(-1)!.user).toContain('Evaluate only these checks: NoUnresolvedSolutionLedLanguage.');
  });

  test('a bad local answer is reported, and nothing is sent to the API', async ({ page }) => {
    const calls = await openCockpit(page, { question: 'not json' });
    await page.getByRole('button', { name: /start the local model/ }).click();

    await page.getByRole('button', { name: 'Propose' }).click();

    await expect(page.locator('p.form-error[role="alert"]')).toContainText('did not return valid JSON');
    expect(calls.some((c) => c.method === 'POST' && c.path.endsWith('/questions'))).toBe(false);
  });

  test('local candidate interventions fill the Shape form', async ({ page }) => {
    await openCockpit(page, {
      interventions: '{"suggestions":[{"type":"Process","description":"One approver per site","rationale":"Removes waiting"},{"type":"NoAction","description":"Keep today\'s process","rationale":"Baseline"}]}',
    });
    await page.getByRole('button', { name: /start the local model/ }).click();
    await page.getByRole('button', { name: /Shape/ }).first().click();

    await page.getByRole('button', { name: 'Ask AI for candidate interventions' }).click();
    await page.getByRole('listitem').filter({ hasText: 'One approver per site' }).getByRole('button', { name: 'Use this' }).click();

    await expect(page.getByPlaceholder('Description')).toHaveValue('One approver per site');
    await expect(page.getByPlaceholder('Rationale')).toHaveValue('Removes waiting');
  });
});
