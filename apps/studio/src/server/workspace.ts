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

let cached: Workspace | undefined;

export function resolveWorkspaceRoot(): string {
  const configured = process.env.MODELLER_STUDIO_WORKSPACE;
  if (configured) return path.resolve(configured);
  return path.resolve(process.cwd(), '..', '..', 'samples', 'child-care');
}

export async function loadWorkspace(): Promise<Workspace> {
  if (cached) return cached;
  const root = resolveWorkspaceRoot();
  const configPath = path.join(root, '.modeller', 'config.json');
  const raw = await readFile(configPath, 'utf-8');
  const config = JSON.parse(raw) as ModellerConfig;
  const sources = (config.sources ?? []).filter((source) => isWorkspaceRelative(source));
  cached = { root, sources };
  return cached;
}

export function isKnownSource(workspace: Workspace, relativePath: string): boolean {
  return isWorkspaceRelative(relativePath) && workspace.sources.includes(relativePath);
}

export function resolveSourcePath(workspace: Workspace, relativePath: string): string {
  return path.join(workspace.root, relativePath);
}
