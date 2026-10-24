import { INITIATIVE_FIELD_GUIDANCE } from '../initiativeFieldGuidance';
import {
  CHECKS_BY_GATE,
  INITIATIVE_FIELD_LABELS,
  buildStructuredFields,
  type GateCheck,
  type GateCheckResultDto,
  type GateKind,
  type InitiativeField,
  type InitiativeSessionDto,
} from '../initiativeTypes';
import { evaluateGatePrompt, parseGateEvaluation, type ChatPrompt, type GateEvaluation } from './prompts';

/**
 * The local model's gate. Small models are unreliable at saying whether a field is empty, so the
 * browser decides every "is there content?" check exactly, and asks the model only for the checks
 * that need judgement. The recommended next question comes from the field guidance, not the model.
 * The Gateway path is unchanged: the hosted model still evaluates every check on the server.
 */

/** The field each check is about, when there is one. */
const FIELD_OF_CHECK: Partial<Record<GateCheck, InitiativeField>> = {
  ProblemStatementDescribesBusinessProblem: 'ProblemStatement',
  AffectedUsersNamed: 'AffectedUsers',
  PainPointsAreConcrete: 'PainPoints',
  OutcomesAreObservable: 'Outcomes',
  SuccessCriteriaAreUnderstandable: 'SuccessCriteria',
  NonGoalsAreListed: 'NonGoals',
  ConstraintsAreListed: 'Constraints',
  AssumptionsAreListed: 'Assumptions',
  OpenQuestionsAreListed: 'OpenQuestions',
  RisksAreListed: 'Risks',
};

/** Checks that only ask whether something was recorded — decided exactly, never by the model. */
const PRESENCE_CHECKS = new Set<GateCheck>([
  'OriginalChangeRequestCaptured',
  'AffectedUsersNamed',
  'NonGoalsAreListed',
  'ConstraintsAreListed',
  'AssumptionsAreListed',
  'OpenQuestionsAreListed',
  'RisksAreListed',
  'SelectedTechnologyInterventionsHaveRationale',
]);

function presenceResult(session: InitiativeSessionDto, fields: Record<InitiativeField, string[]>, check: GateCheck): GateCheckResultDto {
  if (check === 'OriginalChangeRequestCaptured') {
    const passed = session.originalChangeRequest.trim().length > 0;
    return { check, passed, reason: passed ? 'The original change request is recorded.' : 'The original change request is empty.' };
  }
  if (check === 'SelectedTechnologyInterventionsHaveRationale') {
    const technology = session.selectedInterventions.filter((intervention) => intervention.type === 'Technology');
    const missing = technology.filter((intervention) => intervention.rationale.trim().length === 0).length;
    return {
      check,
      passed: missing === 0,
      reason: technology.length === 0
        ? 'No Technology intervention is selected.'
        : missing === 0 ? 'Every selected Technology intervention has a rationale.' : `${missing} selected Technology intervention(s) have no rationale.`,
    };
  }
  return emptyFieldResult(fields, check) ?? { check, passed: true, reason: acceptedAnswers(fields, check) };
}

/** A judgement or presence check fails without asking the model when its field has no accepted answer. */
function emptyFieldResult(fields: Record<InitiativeField, string[]>, check: GateCheck): GateCheckResultDto | null {
  const field = FIELD_OF_CHECK[check];
  if (!field || fields[field].length > 0) return null;
  return { check, passed: false, reason: `No accepted answer for ${INITIATIVE_FIELD_LABELS[field]} yet.` };
}

function acceptedAnswers(fields: Record<InitiativeField, string[]>, check: GateCheck): string {
  const field = FIELD_OF_CHECK[check]!;
  const count = fields[field].length;
  return `${INITIATIVE_FIELD_LABELS[field]}: ${count} accepted ${count === 1 ? 'answer' : 'answers'}.`;
}

/** The model prompt for only the judgement checks that have content to judge, or null if there are none. */
export function judgementPrompt(session: InitiativeSessionDto, kind: GateKind): { prompt: ChatPrompt; checks: GateCheck[] } | null {
  const fields = buildStructuredFields(session);
  const checks = CHECKS_BY_GATE[kind].filter((check) => !PRESENCE_CHECKS.has(check) && !emptyFieldResult(fields, check));
  if (checks.length === 0) return null;

  const base = evaluateGatePrompt(session, kind);
  const schema = base.schema as { properties: { checks: { items: { properties: { check: object } } } } };
  return {
    checks,
    prompt: {
      system: base.system,
      // Local only: the shared template asks for every check of the gate; here the browser has
      // already decided the rest.
      user: `${base.user}\n\nEvaluate only these checks: ${checks.join(', ')}. Use null for recommendedQuestion.`,
      schema: {
        ...base.schema,
        properties: {
          ...(base.schema as { properties: object }).properties,
          checks: {
            ...schema.properties.checks,
            minItems: checks.length,
            maxItems: checks.length,
            items: { ...schema.properties.checks.items, properties: { ...schema.properties.checks.items.properties, check: { enum: checks } } },
          },
        },
      },
    },
  };
}

/** Combines the exact checks and the model's judgement into one evaluation, in the gate's check order. */
export async function evaluateGateLocally(
  session: InitiativeSessionDto,
  kind: GateKind,
  complete: (prompt: ChatPrompt) => Promise<string>,
): Promise<GateEvaluation> {
  const fields = buildStructuredFields(session);
  const judgement = judgementPrompt(session, kind);
  const judged = judgement ? parseGateEvaluation(await complete(judgement.prompt)).results : [];

  const results = CHECKS_BY_GATE[kind].map((check): GateCheckResultDto =>
    PRESENCE_CHECKS.has(check)
      ? presenceResult(session, fields, check)
      : emptyFieldResult(fields, check)
        ?? judged.find((result) => result.check === check)
        ?? { check, passed: false, reason: 'The local model did not evaluate this check.' });

  const firstGap = results.find((result) => !result.passed && FIELD_OF_CHECK[result.check]);
  const field = firstGap ? FIELD_OF_CHECK[firstGap.check]! : null;
  return {
    results,
    recommendedQuestion: field ? { text: INITIATIVE_FIELD_GUIDANCE[field].exampleQuestion, field } : null,
  };
}
