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
