// Mirrors src/Modeller.Api/Initiative/InitiativeSessionDto.cs. Kept as plain types (not
// generated) since the API is small and stable for v1; regenerate by hand if the DTO shape moves.

export type ParticipantRole = 'Facilitator' | 'DomainExpert' | 'Agent';

export type InitiativeField =
  | 'ProblemStatement'
  | 'AffectedUsers'
  | 'PainPoints'
  | 'Outcomes'
  | 'SuccessCriteria'
  | 'NonGoals'
  | 'Constraints'
  | 'Assumptions'
  | 'OpenQuestions'
  | 'Risks';

export type DiscoveryPhase = 'Discover' | 'Frame';

// Mirrors Modeller.Initiative.InitiativeFields.PhaseOf (src/Modeller.Initiative/Phases.cs).
export const PHASE_OF_FIELD: Record<InitiativeField, DiscoveryPhase> = {
  ProblemStatement: 'Discover',
  PainPoints: 'Discover',
  AffectedUsers: 'Discover',
  Outcomes: 'Frame',
  SuccessCriteria: 'Frame',
  NonGoals: 'Frame',
  Constraints: 'Frame',
  Assumptions: 'Frame',
  OpenQuestions: 'Frame',
  Risks: 'Frame',
};

export const ALL_FIELDS: InitiativeField[] = [
  'ProblemStatement',
  'PainPoints',
  'AffectedUsers',
  'Outcomes',
  'SuccessCriteria',
  'NonGoals',
  'Constraints',
  'Assumptions',
  'OpenQuestions',
  'Risks',
];

export const INITIATIVE_FIELD_LABELS: Record<InitiativeField, string> = {
  ProblemStatement: 'Problem statement',
  AffectedUsers: 'Affected users',
  PainPoints: 'Pain points',
  Outcomes: 'Outcomes',
  SuccessCriteria: 'Success criteria',
  NonGoals: 'Non-goals',
  Constraints: 'Constraints',
  Assumptions: 'Assumptions',
  OpenQuestions: 'Open questions',
  Risks: 'Risks',
};

export type InterventionType =
  | 'Process'
  | 'People'
  | 'Organisation'
  | 'Policy'
  | 'Information'
  | 'Technology'
  | 'Experiment'
  | 'NoAction';

export const ALL_INTERVENTION_TYPES: InterventionType[] = [
  'Process',
  'People',
  'Organisation',
  'Policy',
  'Information',
  'Technology',
  'Experiment',
  'NoAction',
];

export type GateKind = 'Discovery' | 'Shape';

export type GateCheck =
  | 'OriginalChangeRequestCaptured'
  | 'ProblemStatementDescribesBusinessProblem'
  | 'AffectedUsersNamed'
  | 'PainPointsAreConcrete'
  | 'OutcomesAreObservable'
  | 'SuccessCriteriaAreUnderstandable'
  | 'NonGoalsAreListed'
  | 'ConstraintsAreListed'
  | 'AssumptionsAreListed'
  | 'OpenQuestionsAreListed'
  | 'RisksAreListed'
  | 'NoUnresolvedSolutionLedLanguage'
  | 'SelectedTechnologyInterventionsHaveRationale'
  | 'NoActionWasConsidered';

export const GATE_CHECK_LABELS: Record<GateCheck, string> = {
  OriginalChangeRequestCaptured: 'Original change request captured',
  ProblemStatementDescribesBusinessProblem: 'Problem statement describes the business problem',
  AffectedUsersNamed: 'Affected users named',
  PainPointsAreConcrete: 'Pain points are concrete',
  OutcomesAreObservable: 'Outcomes are observable',
  SuccessCriteriaAreUnderstandable: 'Success criteria are understandable',
  NonGoalsAreListed: 'Non-goals are listed',
  ConstraintsAreListed: 'Constraints are listed',
  AssumptionsAreListed: 'Assumptions are listed',
  OpenQuestionsAreListed: 'Open questions are listed',
  RisksAreListed: 'Risks are listed',
  NoUnresolvedSolutionLedLanguage: 'No unresolved solution-led language',
  SelectedTechnologyInterventionsHaveRationale: 'Selected technology interventions have a rationale',
  NoActionWasConsidered: 'No action was considered',
};

export const CHECKS_BY_GATE: Record<GateKind, GateCheck[]> = {
  Discovery: [
    'OriginalChangeRequestCaptured',
    'ProblemStatementDescribesBusinessProblem',
    'AffectedUsersNamed',
    'PainPointsAreConcrete',
    'OutcomesAreObservable',
    'SuccessCriteriaAreUnderstandable',
    'NonGoalsAreListed',
    'ConstraintsAreListed',
    'AssumptionsAreListed',
    'OpenQuestionsAreListed',
    'RisksAreListed',
    'NoUnresolvedSolutionLedLanguage',
  ],
  Shape: ['SelectedTechnologyInterventionsHaveRationale', 'NoActionWasConsidered'],
};

export interface ParticipantDto {
  id: string;
  displayName: string;
  role: ParticipantRole;
}

export type QuestionStatus = 'Proposed' | 'Sent' | 'Rejected';

export interface QuestionDto {
  id: string;
  text: string;
  proposedBy: string;
  authorRole: ParticipantRole;
  field: InitiativeField;
  status: QuestionStatus;
}

export type ResponseStatus = 'Pending' | 'Accepted';

export interface ResponseDto {
  id: string;
  questionId: string;
  text: string;
  status: ResponseStatus;
}

export interface SelectedInterventionDto {
  id: string;
  type: InterventionType;
  description: string;
  rationale: string;
  /** Set at selection time (true only for Technology), independent of whether a workspace has
   * actually been linked yet — see Modeller.Initiative.SelectedIntervention's own remarks. */
  continuesToDesignWorkspace: boolean;
  designWorkspaceReference: string | null;
}

export interface GateCheckResultDto {
  check: GateCheck;
  passed: boolean;
  reason: string;
}

export interface GateEvaluationDto {
  kind: GateKind;
  results: GateCheckResultDto[];
  recommendedQuestionId: string | null;
  evaluatedAt: string;
  agentStatus: string;
}

export type GateOverrideType = 'Dismissed' | 'FinalizedAgainstGate';

export interface GateOverrideDto {
  id: string;
  kind: GateKind;
  overrideType: GateOverrideType;
  dismissedFinding: GateCheckResultDto | null;
  finalizedFindings: GateCheckResultDto[] | null;
  reason: string | null;
}

export type FinalizationStatus = 'Clean' | 'WithOpenGateFindings';

export interface FinalizationDto {
  status: FinalizationStatus;
  markdownSnapshot: string;
  finalizedAt: string;
}

export interface InitiativeSessionDto {
  id: string;
  originalChangeRequest: string;
  participants: ParticipantDto[];
  questions: QuestionDto[];
  responses: ResponseDto[];
  selectedInterventions: SelectedInterventionDto[];
  gateOverrides: GateOverrideDto[];
  latestDiscoveryGateEvaluation: GateEvaluationDto | null;
  latestShapeGateEvaluation: GateEvaluationDto | null;
  finalization: FinalizationDto | null;
}

export interface InitiativeErrorResponse {
  code: string;
  message: string;
}

export interface AgentInterventionSuggestionDto {
  type: InterventionType;
  description: string;
  rationale: string;
}

export interface AgentInterventionSuggestionsResponse {
  suggestions: AgentInterventionSuggestionDto[];
}

export interface AgentAdvisorStatusResponse {
  available: boolean;
  model: string | null;
  requiresApiKey: boolean;
  freeModel: string | null;
}

export type InitiativePhase = 'Discover' | 'Frame' | 'Shape' | 'Design';

function hasReachedShape(session: InitiativeSessionDto): boolean {
  return session.selectedInterventions.length > 0 || session.latestShapeGateEvaluation !== null;
}

/** Either a question targeting a Frame-phase field, or a recorded Discovery Gate evaluation, is
 * evidence Frame has been reached: Discovery Gate sits at the Frame -> Shape boundary (see
 * Phases.cs's own remarks), so its mere existence means the Initiative reached the end of Frame
 * even if every Frame-field question was later withdrawn or never asked. */
function hasReachedFrame(session: InitiativeSessionDto): boolean {
  return session.questions.some((q) => PHASE_OF_FIELD[q.field] === 'Frame') || session.latestDiscoveryGateEvaluation !== null;
}

/** Which of the four phases the Initiative is currently in — derived from what's actually happened. */
export function currentPhase(session: InitiativeSessionDto): InitiativePhase {
  if (session.finalization) return 'Design';
  if (hasReachedShape(session)) return 'Shape';
  return hasReachedFrame(session) ? 'Frame' : 'Discover';
}

/** Groups accepted responses by the field their originating question targeted — mirrors
 * InitiativeSession.BuildStructuredFields (src/Modeller.Initiative/Initiative.cs); the API returns
 * the raw questions/responses, not the pre-built structured record, so this is computed client-side. */
export function buildStructuredFields(session: InitiativeSessionDto): Record<InitiativeField, string[]> {
  const byField = Object.fromEntries(ALL_FIELDS.map((field) => [field, [] as string[]])) as Record<
    InitiativeField,
    string[]
  >;
  const questionById = new Map(session.questions.map((q) => [q.id, q]));
  for (const response of session.responses) {
    if (response.status !== 'Accepted') continue;
    const question = questionById.get(response.questionId);
    if (!question) continue;
    byField[question.field].push(response.text);
  }
  return byField;
}
