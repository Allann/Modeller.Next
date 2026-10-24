import {
  ALL_FIELDS,
  ALL_INTERVENTION_TYPES,
  CHECKS_BY_GATE,
  buildStructuredFields,
  type GateCheck,
  type GateCheckResultDto,
  type GateKind,
  type InitiativeField,
  type InitiativeSessionDto,
  type InterventionType,
} from '../initiativeTypes';
import agentPrompts from './agent-prompts.json';

/**
 * The in-browser local model's side of the Agent Participant: the same prompts (agent-prompts.json,
 * a byte-identical copy of the server's — see AgentPromptsTests), the same structured-fields text,
 * and the same validation rules as Modeller.Initiative.OpenAICompatible.OpenAiCompatibleAgentAdvisor.
 * Change them together.
 */

type PromptTemplate = { system: string; user: string; fieldDescriptions?: Record<string, string> };

export const AGENT_PROMPTS: Record<'proposeQuestion' | 'proposeInterventions' | 'evaluateGate', PromptTemplate> = agentPrompts;

/** Mirrors AgentPromptTemplate.DescribeField. */
function describeField(field: InitiativeField): string {
  return AGENT_PROMPTS.proposeQuestion.fieldDescriptions?.[field] ?? '';
}

/** Prompt budget for the local model's 4,096-token context window, with room left for the answer.
 * Much lower than the hosted model's Agent:MaxPromptCharacters (24,000). */
export const LOCAL_MAX_PROMPT_CHARACTERS = 8_000;

export class LocalAgentError extends Error {}

export type ChatPrompt = { system: string; user: string; schema: object };

/** Single-pass `{name}` substitution — mirrors AgentPromptTemplate.RenderUser. */
export function renderUser(template: string, values: Record<string, string>): string {
  return template.replace(/\{(\w+)\}/g, (match, name: string) => (name in values ? values[name] : match));
}

const MARKDOWN_TITLES: Record<InitiativeField, string> = {
  ProblemStatement: 'Problem Statement',
  AffectedUsers: 'Affected Users',
  PainPoints: 'Pain Points',
  Outcomes: 'Outcomes',
  SuccessCriteria: 'Success Criteria',
  NonGoals: 'Non-Goals',
  Constraints: 'Constraints',
  Assumptions: 'Assumptions',
  OpenQuestions: 'Open Questions',
  Risks: 'Risks',
};

/** Section order of InitiativeStructuredFields.ToMarkdown — not ALL_FIELDS' order. */
const MARKDOWN_ORDER: InitiativeField[] = [
  'ProblemStatement', 'AffectedUsers', 'PainPoints', 'Outcomes', 'SuccessCriteria',
  'NonGoals', 'Constraints', 'Assumptions', 'OpenQuestions', 'Risks',
];

/** Mirrors Modeller.Initiative.InitiativeStructuredFields.ToMarkdown. */
export function structuredFieldsMarkdown(session: InitiativeSessionDto): string {
  const fields = buildStructuredFields(session);
  const lines = ['## Original Change Request', `- ${session.originalChangeRequest}`];
  for (const field of MARKDOWN_ORDER) {
    if (fields[field].length === 0) continue;
    lines.push('', `## ${MARKDOWN_TITLES[field]}`, ...fields[field].map((entry) => `- ${entry}`));
  }
  if (session.selectedInterventions.length > 0) {
    lines.push('', '## Selected Interventions');
    for (const intervention of session.selectedInterventions) {
      const handoff = intervention.designWorkspaceReference
        ? ` (continues into System Design: ${intervention.designWorkspaceReference})`
        : intervention.continuesToDesignWorkspace
          ? ' (queued for System Design)'
          : '';
      lines.push(`- [${intervention.type}] ${intervention.description} — ${intervention.rationale}${handoff}`);
    }
  }
  return lines.join('\n');
}

function withinBudget(prompt: ChatPrompt): ChatPrompt {
  if (prompt.system.length + prompt.user.length > LOCAL_MAX_PROMPT_CHARACTERS) {
    throw new LocalAgentError('This Initiative is now too long for the local model. Use the AI Gateway, or continue manually.');
  }
  return prompt;
}

const nonEmptyString = { type: 'string', minLength: 1 };

export function proposeQuestionPrompt(session: InitiativeSessionDto, field: InitiativeField): ChatPrompt {
  return withinBudget({
    system: AGENT_PROMPTS.proposeQuestion.system,
    user: renderUser(AGENT_PROMPTS.proposeQuestion.user, {
      originalChangeRequest: session.originalChangeRequest,
      targetField: field,
      targetFieldDescription: describeField(field),
      currentFields: structuredFieldsMarkdown(session),
    }),
    schema: { type: 'object', properties: { text: nonEmptyString }, required: ['text'] },
  });
}

export function proposeInterventionsPrompt(session: InitiativeSessionDto): ChatPrompt {
  return withinBudget({
    system: AGENT_PROMPTS.proposeInterventions.system,
    user: renderUser(AGENT_PROMPTS.proposeInterventions.user, { currentFields: structuredFieldsMarkdown(session) }),
    schema: {
      type: 'object',
      properties: {
        suggestions: {
          type: 'array',
          minItems: 3,
          maxItems: 5,
          items: {
            type: 'object',
            properties: { type: { enum: ALL_INTERVENTION_TYPES }, description: nonEmptyString, rationale: nonEmptyString },
            required: ['type', 'description', 'rationale'],
          },
        },
      },
      required: ['suggestions'],
    },
  });
}

export function evaluateGatePrompt(session: InitiativeSessionDto, kind: GateKind): ChatPrompt {
  const checks = CHECKS_BY_GATE[kind];
  return withinBudget({
    system: AGENT_PROMPTS.evaluateGate.system,
    user: renderUser(AGENT_PROMPTS.evaluateGate.user, { gate: kind, currentFields: structuredFieldsMarkdown(session) }),
    schema: {
      type: 'object',
      properties: {
        checks: {
          type: 'array',
          minItems: checks.length,
          maxItems: checks.length,
          items: {
            type: 'object',
            properties: { check: { enum: checks }, passed: { type: 'boolean' }, reason: nonEmptyString },
            required: ['check', 'passed', 'reason'],
          },
        },
        // The question object comes first: with `null` first, small models always chose null, even
        // when checks failed.
        recommendedQuestion: {
          anyOf: [
            { type: 'object', properties: { text: nonEmptyString, field: { enum: ALL_FIELDS } }, required: ['text', 'field'] },
            { type: 'null' },
          ],
        },
      },
      required: ['checks', 'recommendedQuestion'],
    },
  });
}

// --- Parsing: the same acceptance rules as OpenAiCompatibleAgentAdvisor's parsers. ---

function parseJson(content: string): Record<string, unknown> {
  let payload = content.trim();
  if (payload.startsWith('```')) {
    payload = payload.slice(payload.indexOf('\n') + 1).trim();
    if (payload.endsWith('```')) payload = payload.slice(0, -3).trim();
  }
  try {
    const parsed: unknown = JSON.parse(payload);
    if (parsed && typeof parsed === 'object' && !Array.isArray(parsed)) return parsed as Record<string, unknown>;
  } catch {
    // Falls through to the same error as a non-object payload.
  }
  throw new LocalAgentError('The local model did not return valid JSON. Try again.');
}

function requireString(value: Record<string, unknown>, name: string): string {
  const text = value[name];
  if (typeof text === 'string' && text.trim().length > 0) return text;
  throw new LocalAgentError(`The local model's answer was missing '${name}'. Try again.`);
}

function matchIgnoringCase<T extends string>(options: readonly T[], value: string): T | undefined {
  return options.find((option) => option.toLowerCase() === value.toLowerCase());
}

export function parseQuestion(content: string): string {
  return requireString(parseJson(content), 'text');
}

export type InterventionSuggestion = { type: InterventionType; description: string; rationale: string };

export function parseInterventions(content: string): InterventionSuggestion[] {
  const suggestions = parseJson(content).suggestions;
  if (!Array.isArray(suggestions)) throw new LocalAgentError('The local model did not return any interventions. Try again.');
  const parsed = suggestions.map((item: Record<string, unknown>) => {
    const type = matchIgnoringCase(ALL_INTERVENTION_TYPES, requireString(item, 'type'));
    if (!type) throw new LocalAgentError('The local model suggested an unknown intervention type. Try again.');
    return { type, description: requireString(item, 'description'), rationale: requireString(item, 'rationale') };
  });
  if (parsed.length === 0) throw new LocalAgentError('The local model did not return any interventions. Try again.');
  return parsed;
}

export type GateEvaluation = {
  results: GateCheckResultDto[];
  recommendedQuestion: { text: string; field: InitiativeField } | null;
};

const ALL_GATE_CHECKS: GateCheck[] = [...CHECKS_BY_GATE.Discovery, ...CHECKS_BY_GATE.Shape];

export function parseGateEvaluation(content: string): GateEvaluation {
  const root = parseJson(content);
  if (!Array.isArray(root.checks)) throw new LocalAgentError('The local model did not return any gate checks. Try again.');
  const results = root.checks.map((item: Record<string, unknown>) => {
    const check = matchIgnoringCase(ALL_GATE_CHECKS, requireString(item, 'check'));
    if (!check || typeof item.passed !== 'boolean') throw new LocalAgentError('The local model returned a malformed gate check. Try again.');
    return { check, passed: item.passed, reason: requireString(item, 'reason') };
  });
  if (results.length === 0) throw new LocalAgentError('The local model did not return any gate checks. Try again.');

  let recommendedQuestion: GateEvaluation['recommendedQuestion'] = null;
  const recommended = root.recommendedQuestion;
  if (recommended && typeof recommended === 'object' && !Array.isArray(recommended)) {
    const field = matchIgnoringCase(ALL_FIELDS, requireString(recommended as Record<string, unknown>, 'field'));
    if (!field) throw new LocalAgentError(`The local model recommended a question for an unknown field. Try again.`);
    recommendedQuestion = { text: requireString(recommended as Record<string, unknown>, 'text'), field };
  }
  return { results, recommendedQuestion };
}
