// Browser-side client for the hosted Modeller.Api (issue #71) — the
// playground's only backend, called directly from the browser rather than
// proxied through a Next.js server route (there is no server-side trust
// boundary to add here: the API is already public and bounded). Mirrors
// src/Modeller.Api/Contracts/WorkspaceContracts.cs; ASP.NET Core's minimal-API
// default JSON casing is camelCase, confirmed against
// tests/Modeller.Api.Tests/MalformedRequestTests.cs's request fixtures.
const API_BASE = process.env.NEXT_PUBLIC_MODELLER_API_URL ?? '';

export interface WorkspaceDocumentDto {
  path: string;
  content: string;
}

export interface EphemeralIdentityDto {
  kind: 'ephemeral';
}

export interface DurableIdentityDto {
  kind: 'durable';
  version: string;
  documents: Record<string, string[]>;
}

export type IdentityDto = EphemeralIdentityDto | DurableIdentityDto;

export const EPHEMERAL_IDENTITY: EphemeralIdentityDto = { kind: 'ephemeral' };

export interface ConfigurationDto {
  generationContractVersion: string;
  logicalOutputRoot: string;
  profile?: string;
}

export interface ProjectionRequestDto {
  id: string;
  kind: string;
  roots: string[];
}

export interface ApiSourceSpan {
  document: string;
  line: number;
  column: number;
  length: number;
}

export interface ApiDiagnostic {
  code: string;
  message: string;
  location?: ApiSourceSpan;
}

export interface ApiProjectionNode {
  id: string;
  role: string;
  label: string;
  semanticIds: string[];
}

export interface ApiProjectionEdge {
  id: string;
  role: string;
  label: string;
  sourceId: string;
  targetId: string;
  semanticIds: string[];
}

export interface ApiProjectionGraph {
  sourceRevision: number;
  kind: string;
  nodes: ApiProjectionNode[];
  edges: ApiProjectionEdge[];
}

export interface ProjectionResponseDto {
  id: string;
  succeeded: boolean;
  graph?: ApiProjectionGraph;
  diagnostics: ApiDiagnostic[];
}

export interface RootSummaryDto {
  id: string;
  kind: string;
  name: string;
  slug: string;
}

export interface SemanticOutlineItemDto {
  id: string;
  kind: string;
  name: string;
  ownerId?: string;
  location: ApiSourceSpan;
}

export interface SemanticCountDto {
  kind: string;
  count: number;
}

export interface WorkspaceAnalyzeResponse {
  apiVersion: string;
  diagnostics: ApiDiagnostic[];
  roots: RootSummaryDto[];
  outline: SemanticOutlineItemDto[];
  summary: SemanticCountDto[];
  projections: ProjectionResponseDto[];
  identity: DurableIdentityDto | null;
}

// Response of POST /v1/workspace/export (issue #73): the post-identity-application document text
// plus the durable registry harvested from it. `identity` is null only when `diagnostics` is
// non-empty (analysis/harvest failed).
export interface WorkspaceExportResponse {
  apiVersion: string;
  diagnostics: ApiDiagnostic[];
  documents: WorkspaceDocumentDto[];
  identity: DurableIdentityDto | null;
}

interface SupportedViewsResponse {
  apiVersion: string;
  views: string[];
}

// The only template pack the generation preview (issue #135) currently offers — there is no
// picker UI yet (out of scope), but `generateWorkspace` still takes `templatePackId` as a real
// parameter rather than hard-coding it into the request body, so adding a picker later is additive
// rather than a breaking change to this function's signature.
export const DEFAULT_TEMPLATE_PACK_ID = 'csharp/domain-project';

// One proposed generated artifact — mirrors Modeller.Api.Contracts.GeneratedArtifactDto.
export interface GeneratedArtifactDto {
  path: string;
  owner: string;
  packId: string;
  templateId: string;
  content: string;
  digest: string;
}

// Response of POST /v1/workspace/generate (issue #135): a read-only generation preview. Like
// analyze/export, a parse/validation/plan/render failure comes back as 200 with `diagnostics`
// populated and `artifacts` empty rather than a 400/500.
export interface WorkspaceGenerateResponse {
  apiVersion: string;
  diagnostics: ApiDiagnostic[];
  artifacts: GeneratedArtifactDto[];
}

// Deliberately no `projections` field on the request — that's analyze-specific.
export async function generateWorkspace(
  documents: readonly WorkspaceDocumentDto[],
  identity: IdentityDto,
  configuration: ConfigurationDto,
  templatePackId: string = DEFAULT_TEMPLATE_PACK_ID,
  signal?: AbortSignal,
): Promise<WorkspaceGenerateResponse> {
  const response = await fetch(`${API_BASE}/v1/workspace/generate`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ documents, identity, configuration, templatePackId }),
    signal,
  });
  if (!response.ok) throw new Error(`Workspace generation failed with status ${response.status}.`);
  return (await response.json()) as WorkspaceGenerateResponse;
}

export async function analyzeWorkspace(
  documents: readonly WorkspaceDocumentDto[],
  identity: IdentityDto,
  configuration: ConfigurationDto,
  projections: readonly ProjectionRequestDto[] = [],
  signal?: AbortSignal,
): Promise<WorkspaceAnalyzeResponse> {
  const response = await fetch(`${API_BASE}/v1/workspace/analyze`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ documents, identity, configuration, projections }),
    signal,
  });
  if (!response.ok) throw new Error(`Workspace analysis failed with status ${response.status}.`);
  return (await response.json()) as WorkspaceAnalyzeResponse;
}

// Turns the current draft into a stable snapshot: the resulting `identity` should replace the
// draft's own identity going forward so a second export (or the next analyze call) carries the
// same registry instead of an ephemeral draft minting fresh ids every time.
export async function exportWorkspace(
  documents: readonly WorkspaceDocumentDto[],
  identity: IdentityDto,
  configuration: ConfigurationDto,
): Promise<WorkspaceExportResponse> {
  const response = await fetch(`${API_BASE}/v1/workspace/export`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ documents, identity, configuration, projections: [] }),
  });
  if (!response.ok) throw new Error(`Workspace export failed with status ${response.status}.`);
  return (await response.json()) as WorkspaceExportResponse;
}

export async function fetchSupportedViews(): Promise<string[]> {
  const response = await fetch(`${API_BASE}/v1/workspace/supported-views`);
  if (!response.ok) throw new Error(`Failed to load supported views (status ${response.status}).`);
  const data = (await response.json()) as SupportedViewsResponse;
  return data.views;
}

export interface CompletionItemDto {
  label: string;
  kind: string;
  detail: string;
  insertText: string;
  replacementStartColumn: number;
}

export async function completeWorkspace(
  documents: readonly WorkspaceDocumentDto[], identity: IdentityDto, configuration: ConfigurationDto,
  path: string, line: number, column: number, signal?: AbortSignal,
): Promise<CompletionItemDto[]> {
  try {
    const response = await fetch(`${API_BASE}/v1/workspace/complete`, {
      method: 'POST', headers: { 'Content-Type': 'application/json' }, signal,
      body: JSON.stringify({ workspace: { documents, identity, configuration, projections: [] }, path, line, column }),
    });
    if (!response.ok) return [];
    return ((await response.json()) as { items: CompletionItemDto[] }).items;
  } catch {
    // Completion is an optional editor aid. A cancelled or unavailable request
    // must not interrupt editing, diagnostics, or the current browser draft.
    return [];
  }
}
