import type {
  AgentInterventionSuggestionsResponse,
  AgentAdvisorStatusResponse,
  CreateInitiativeResponseDto,
  GateCheckResultDto,
  GateKind,
  InitiativeErrorResponse,
  InitiativeSessionDto,
  InterventionType,
} from './initiativeTypes';
import { analyticsId, isInternalVisitor } from './productAnalytics';

// next.config.mjs resolves this for every Next build (and keeps the CSP's connect-src in step with
// it), so the fallback only applies outside a Next build — it matches Modeller.Api's own PORT
// default (src/Modeller.Api/Program.cs) for a locally running API.
const API_BASE_URL = process.env.NEXT_PUBLIC_MODELLER_API_URL ?? 'http://localhost:8080';

// Issue #146: every request against a specific session now carries this header — the server judges
// the caller's role from it, never from a client-supplied role string. Mirrors how the unrelated
// Agent API key already travels as a header (X-Agent-Api-Key), but this one is per-participant.
const CREDENTIAL_HEADER = 'X-Initiative-Credential';

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

function headersFor(credential: string, apiKey?: string): HeadersInit {
  return {
    [CREDENTIAL_HEADER]: credential,
    ...(apiKey ? { 'X-Agent-Api-Key': apiKey } : {}),
  };
}

const post = <T>(path: string, credential: string, body?: unknown, apiKey?: string) =>
  send<T>(path, {
    method: 'POST',
    body: body === undefined ? undefined : JSON.stringify(body),
    headers: headersFor(credential, apiKey),
  });

export const initiativeApi = {
  getAgentStatus: () => send<AgentAdvisorStatusResponse>('/v1/initiative/agent-status'),

  create: (originalChangeRequest: string, facilitatorName: string, domainExpertName: string) =>
    send<CreateInitiativeResponseDto>('/v1/initiative', {
      method: 'POST',
      body: JSON.stringify({ originalChangeRequest, facilitatorName, domainExpertName }),
    }),

  get: (id: string, credential: string) =>
    send<InitiativeSessionDto>(`/v1/initiative/${id}`, { headers: headersFor(credential) }),

  proposeQuestion: (id: string, credential: string, field: string, text: string | null, apiKey?: string) =>
    post<InitiativeSessionDto>(`/v1/initiative/${id}/questions`, credential, { field, text }, apiKey),

  sendQuestion: (id: string, credential: string, questionId: string) =>
    post<InitiativeSessionDto>(`/v1/initiative/${id}/questions/${questionId}/send`, credential),

  rejectQuestion: (id: string, credential: string, questionId: string) =>
    post<InitiativeSessionDto>(`/v1/initiative/${id}/questions/${questionId}/reject`, credential),

  submitResponse: (id: string, credential: string, questionId: string, text: string) =>
    post<InitiativeSessionDto>(`/v1/initiative/${id}/questions/${questionId}/responses`, credential, { text }),

  acceptResponse: (id: string, credential: string, responseId: string) =>
    post<InitiativeSessionDto>(`/v1/initiative/${id}/responses/${responseId}/accept`, credential),

  getInterventionSuggestions: (id: string, credential: string, apiKey: string) =>
    send<AgentInterventionSuggestionsResponse>(`/v1/initiative/${id}/interventions/suggestions`, { headers: headersFor(credential, apiKey) }),

  selectIntervention: (id: string, credential: string, type: InterventionType, description: string, rationale: string, continuesToDesignWorkspace: boolean) =>
    post<InitiativeSessionDto>(`/v1/initiative/${id}/interventions`, credential, { type, description, rationale, continuesToDesignWorkspace }),

  withdrawIntervention: (id: string, credential: string, interventionId: string) =>
    post<InitiativeSessionDto>(`/v1/initiative/${id}/interventions/${interventionId}/withdraw`, credential),

  linkDesignWorkspace: (id: string, credential: string, interventionId: string, reference: string) =>
    post<InitiativeSessionDto>(`/v1/initiative/${id}/interventions/${interventionId}/design-workspace`, credential, { reference }),

  recordGateEvaluation: (id: string, credential: string, kind: GateKind, manualResults: GateCheckResultDto[] | null, apiKey?: string) =>
    post<InitiativeSessionDto>(`/v1/initiative/${id}/gate-evaluations`, credential, { kind, manualResults }, apiKey),

  dismissGateFinding: (id: string, credential: string, kind: GateKind, check: string, reason: string | null) =>
    post<InitiativeSessionDto>(`/v1/initiative/${id}/gate-evaluations/${kind}/dismiss`, credential, { check, reason }),

  finalize: (id: string, credential: string, reason: string | null) =>
    post<InitiativeSessionDto>(`/v1/initiative/${id}/finalize`, credential, { reason }),

  reopen: (id: string, credential: string) => post<InitiativeSessionDto>(`/v1/initiative/${id}/reopen`, credential),
};

export { API_BASE_URL };
