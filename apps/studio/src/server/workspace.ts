import { readFile } from 'node:fs/promises';
import path from 'node:path';
import { isWorkspaceRelative } from './path-safety';

export interface ModellerConfig {
  version: string;
  sources: string[];
  [key: string]: unknown;
}

export interface Workspace {
  root: string;
  sources: string[];
}

// Thrown for a directory that doesn't have a `.modeller/config.json` — distinguished from other
// I/O failures so callers can tell "not a recognised workspace" (expected user input, worth a
// friendly message) apart from an unexpected read/parse error.
export class WorkspaceNotFoundError extends Error {
  constructor(root: string) {
    super(`"${root}" isn't a recognised Modeller workspace (no .modeller/config.json found there).`);
    this.name = 'WorkspaceNotFoundError';
  }
}

// The active workspace, switchable at runtime (see setWorkspaceRoot) rather than fixed for the
// life of the process — a local developer can point Studio at a different sample, or any other
// workspace directory, without restarting.
let current: Workspace | undefined;

export function resolveWorkspaceRoot(): string {
  const configured = process.env.MODELLER_STUDIO_WORKSPACE;
  if (configured) return path.resolve(configured);
  return path.resolve(process.cwd(), '..', '..', 'samples', 'child-care');
}

async function readWorkspace(root: string): Promise<Workspace> {
  const configPath = path.join(root, '.modeller', 'config.json');
  let raw: string;
  try {
    raw = await readFile(configPath, 'utf-8');
  } catch {
    throw new WorkspaceNotFoundError(root);
  }
  const config = JSON.parse(raw) as ModellerConfig;
  const sources = (config.sources ?? []).filter((source) => isWorkspaceRelative(source));
  return { root, sources };
}

export async function loadWorkspace(): Promise<Workspace> {
  if (current) return current;
  current = await readWorkspace(resolveWorkspaceRoot());
  return current;
}

// Points the active workspace at a different local directory. `requestedRoot` names the
// workspace's own root, not a path within one, so it is resolved and read as given rather than
// checked with isWorkspaceRelative (that check guards paths *inside* an already-selected
// workspace — see isKnownSource/resolveSourcePath below). Throws WorkspaceNotFoundError if the
// directory has no `.modeller/config.json`, leaving the previously active workspace untouched.
export async function setWorkspaceRoot(requestedRoot: string): Promise<Workspace> {
  const root = path.resolve(requestedRoot);
  const workspace = await readWorkspace(root);
  current = workspace;
  return workspace;
}

export function isKnownSource(workspace: Workspace, relativePath: string): boolean {
  return isWorkspaceRelative(relativePath) && workspace.sources.includes(relativePath);
}

export function resolveSourcePath(workspace: Workspace, relativePath: string): string {
  return path.join(workspace.root, relativePath);
}
