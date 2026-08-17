import type {
  AgentInterventionSuggestionsResponse,
  GateCheckResultDto,
  GateKind,
  InitiativeErrorResponse,
  InitiativeSessionDto,
  InterventionType,
  ParticipantRole,
} from './initiativeTypes';
import { analyticsId, isInternalVisitor } from './productAnalytics';

// next.config.mjs resolves this for every Next build (and keeps the CSP's connect-src in step with
// it), so the fallback only applies outside a Next build — it matches Modeller.Api's own PORT
// default (src/Modeller.Api/Program.cs) for a locally running API.
const API_BASE_URL = process.env.NEXT_PUBLIC_MODELLER_API_URL ?? 'http://localhost:8080';

export class InitiativeApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly body: InitiativeErrorResponse,
  ) {
    super(body.message);
  }
}

async function send<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      'X-Analytics-Id': analyticsId(),
      'X-Modeller-Internal': isInternalVisitor() ? '1' : '0',
      ...init?.headers,
    },
  });
  const body = await response.json();
  if (!response.ok) throw new InitiativeApiError(response.status, body as InitiativeErrorResponse);
  return body as T;
}

const post = <T>(path: string, body?: unknown) =>
  send<T>(path, { method: 'POST', body: body === undefined ? undefined : JSON.stringify(body) });

export const initiativeApi = {
  create: (originalChangeRequest: string, facilitatorName: string, domainExpertName: string) =>
    post<InitiativeSessionDto>('/v1/initiative', { originalChangeRequest, facilitatorName, domainExpertName }),

  get: (id: string, viewerRole?: 'DomainExpert') =>
    send<InitiativeSessionDto>(`/v1/initiative/${id}${viewerRole ? `?viewerRole=${viewerRole}` : ''}`),

  proposeQuestion: (id: string, proposedBy: string, authorRole: ParticipantRole, field: string, text: string | null) =>
    post<InitiativeSessionDto>(`/v1/initiative/${id}/questions`, { proposedBy, authorRole, field, text }),

  sendQuestion: (id: string, questionId: string) =>
    post<InitiativeSessionDto>(`/v1/initiative/${id}/questions/${questionId}/send`),

  rejectQuestion: (id: string, questionId: string) =>
    post<InitiativeSessionDto>(`/v1/initiative/${id}/questions/${questionId}/reject`),

  submitResponse: (id: string, questionId: string, text: string) =>
    post<InitiativeSessionDto>(`/v1/initiative/${id}/questions/${questionId}/responses`, { text }),

  acceptResponse: (id: string, responseId: string) =>
    post<InitiativeSessionDto>(`/v1/initiative/${id}/responses/${responseId}/accept`),

  getInterventionSuggestions: (id: string) =>
    send<AgentInterventionSuggestionsResponse>(`/v1/initiative/${id}/interventions/suggestions`),

  selectIntervention: (id: string, type: InterventionType, description: string, rationale: string, continuesToDesignWorkspace: boolean) =>
    post<InitiativeSessionDto>(`/v1/initiative/${id}/interventions`, { type, description, rationale, continuesToDesignWorkspace }),

  withdrawIntervention: (id: string, interventionId: string) =>
    post<InitiativeSessionDto>(`/v1/initiative/${id}/interventions/${interventionId}/withdraw`),

  linkDesignWorkspace: (id: string, interventionId: string, reference: string) =>
    post<InitiativeSessionDto>(`/v1/initiative/${id}/interventions/${interventionId}/design-workspace`, { reference }),

  recordGateEvaluation: (id: string, kind: GateKind, manualResults: GateCheckResultDto[] | null) =>
    post<InitiativeSessionDto>(`/v1/initiative/${id}/gate-evaluations`, { kind, manualResults }),

  dismissGateFinding: (id: string, kind: GateKind, check: string, reason: string | null) =>
    post<InitiativeSessionDto>(`/v1/initiative/${id}/gate-evaluations/${kind}/dismiss`, { check, reason }),

  finalize: (id: string, reason: string | null) =>
    post<InitiativeSessionDto>(`/v1/initiative/${id}/finalize`, { reason }),

  reopen: (id: string) => post<InitiativeSessionDto>(`/v1/initiative/${id}/reopen`),
};

export { API_BASE_URL };
