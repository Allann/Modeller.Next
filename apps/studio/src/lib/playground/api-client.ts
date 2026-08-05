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

export interface WorkspaceAnalyzeResponse {
  apiVersion: string;
  diagnostics: ApiDiagnostic[];
  roots: RootSummaryDto[];
  projections: ProjectionResponseDto[];
}

interface SupportedViewsResponse {
  apiVersion: string;
  views: string[];
}

// Every playground request is an ephemeral draft — no durable identity
// registry exists until a workspace is downloaded (#73's scope).
export async function analyzeWorkspace(
  documents: readonly WorkspaceDocumentDto[],
  configuration: ConfigurationDto,
  projections: readonly ProjectionRequestDto[] = [],
  signal?: AbortSignal,
): Promise<WorkspaceAnalyzeResponse> {
  const response = await fetch(`${API_BASE}/v1/workspace/analyze`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ documents, identity: { kind: 'ephemeral' }, configuration, projections }),
    signal,
  });
  if (!response.ok) throw new Error(`Workspace analysis failed with status ${response.status}.`);
  return (await response.json()) as WorkspaceAnalyzeResponse;
}

export async function fetchSupportedViews(): Promise<string[]> {
  const response = await fetch(`${API_BASE}/v1/workspace/supported-views`);
  if (!response.ok) throw new Error(`Failed to load supported views (status ${response.status}).`);
  const data = (await response.json()) as SupportedViewsResponse;
  return data.views;
}
