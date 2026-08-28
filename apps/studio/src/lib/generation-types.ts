// Shared between the playground's HTTP generation-preview client (lib/playground/api-client.ts,
// backed by Modeller.Api's WorkspaceGenerationPreviewPipeline) and local Studio's CLI-subprocess
// generation client (server/generation-process.ts, backed by `modeller generate --dry-run
// --format json`) — both ultimately render the same GenerationPreview component
// (components/workbench/GenerationPreview.tsx), so both sides speak this one artifact/diagnostic
// shape rather than each defining their own.

// One proposed generated artifact — mirrors Modeller.Api.Contracts.GeneratedArtifactDto. `digest`
// is only ever populated by the playground's API response; the CLI's JSON output has no equivalent
// field, and nothing reads it (GenerationPreview.tsx diffs on `content`, not `digest`).
export interface GeneratedArtifactDto {
  path: string;
  owner: string;
  packId: string;
  templateId: string;
  content: string;
  digest?: string;
}

export interface ApiDiagnostic {
  code: string;
  message: string;
}
