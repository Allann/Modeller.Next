import type { GenerationResult } from './generation-process';

export interface GenerateResponseBody {
  outputVersion: string;
  changes: GenerationResult['changes'];
  artifacts: GenerationResult['artifacts'];
  diagnostics: GenerationResult['diagnostics'];
  before: Record<string, string>;
}

/**
 * Isolated from the /api/generate route handler so the one piece of real branching logic here —
 * detecting a conflict and explaining what that means — is unit-testable without spawning a CLI
 * subprocess. OutputApplication.ExecuteAsync (src/Modeller.Output/OutputApplication.cs) applies
 * atomically, all-or-nothing: a single conflicting artifact (a generated file edited outside
 * Studio, so its on-disk digest no longer matches the ownership manifest) blocks every write in the
 * batch, not just that one file. `artifacts`/`before` still describe what *would* be written —
 * useful as a preview — but nothing was actually written when this fires.
 */
export function buildGenerateResponseBody(applied: GenerationResult, before: Record<string, string>): GenerateResponseBody {
  const conflictedPaths = applied.changes.filter((change) => change.status === 'conflict').map((change) => change.path);
  const diagnostics = conflictedPaths.length > 0
    ? [
        {
          code: 'workspace.generate.conflict',
          message: `Nothing was written — ${conflictedPaths.length} generated file(s) were changed outside Studio: ${conflictedPaths.join(', ')}. Resolve those files, then generate again.`,
        },
        ...applied.diagnostics,
      ]
    : applied.diagnostics;

  return { ...applied, diagnostics, before };
}
