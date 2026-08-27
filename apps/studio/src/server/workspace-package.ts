import { mkdir, readFile, rm, writeFile } from 'node:fs/promises';
import { createHash } from 'node:crypto';
import os from 'node:os';
import path from 'node:path';
import { unzipSync } from 'fflate';
import { isWorkspaceRelative } from './path-safety';

const WORKSPACE_PACKAGE_EXTENSION = '.modeller-workspace';
const WORKSPACE_PACKAGE_KIND = 'ModellerStudioWorkspace';
const SUPPORTED_PACKAGE_VERSION = '1.0';

export class WorkspacePackageOpenError extends Error {
  constructor(message: string) {
    super(message);
    this.name = 'WorkspacePackageOpenError';
  }
}

interface WorkspacePackageMetadata {
  packageVersion?: unknown;
  packageKind?: unknown;
}

export function resolveWorkspacePackageArgument(args = process.argv.slice(2)): string | undefined {
  const direct = args.find((arg) => arg.toLowerCase().endsWith(WORKSPACE_PACKAGE_EXTENSION));
  if (direct) return direct;
  const openIndex = args.findIndex((arg) => arg === '--open-workspace-package');
  return openIndex >= 0 ? args[openIndex + 1] : undefined;
}

export function defaultExtractedWorkspaceRoot(packageBytes: Buffer): string {
  const digest = createHash('sha256').update(packageBytes).digest('hex').slice(0, 16);
  const appData = process.env.LOCALAPPDATA ?? path.join(os.homedir(), 'AppData', 'Local');
  return path.join(appData, 'Modeller Studio', 'OpenedWorkspaces', digest);
}

export async function openWorkspacePackage(packagePath: string): Promise<string> {
  const bytes = await readFile(packagePath);
  return extractWorkspacePackage(bytes, defaultExtractedWorkspaceRoot(bytes));
}

export async function extractWorkspacePackage(packageBytes: Uint8Array, targetRoot: string): Promise<string> {
  let entries: Record<string, Uint8Array>;
  try {
    entries = unzipSync(packageBytes);
  } catch {
    throw new WorkspacePackageOpenError('This workspace package could not be read. Download it again, then open the new package.');
  }

  const metadata = readJsonEntry<WorkspacePackageMetadata>(entries, '.modeller/package.json');
  if (metadata.packageKind !== WORKSPACE_PACKAGE_KIND) {
    throw new WorkspacePackageOpenError('This file is not a Modeller Studio workspace package.');
  }
  if (metadata.packageVersion !== SUPPORTED_PACKAGE_VERSION) {
    throw new WorkspacePackageOpenError('This workspace package needs a newer Modeller Studio. Update Studio, then open the package again.');
  }

  const config = readJsonEntry<{ sources?: unknown }>(entries, '.modeller/config.json');
  if (!Array.isArray(config.sources)) {
    throw new WorkspacePackageOpenError('This workspace package is missing its source document list. Download it again.');
  }

  const expectedSources = config.sources.filter((source): source is string => typeof source === 'string');
  for (const source of expectedSources) {
    if (!isWorkspaceRelative(source) || !entries[source]) {
      throw new WorkspacePackageOpenError('This workspace package is missing a source document. Download it again.');
    }
  }

  await rm(targetRoot, { recursive: true, force: true });
  for (const [entryPath, content] of Object.entries(entries)) {
    if (!isWorkspaceRelative(entryPath)) continue;
    const targetPath = path.join(targetRoot, entryPath);
    await mkdir(path.dirname(targetPath), { recursive: true });
    await writeFile(targetPath, content);
  }
  return targetRoot;
}

function readJsonEntry<T>(entries: Record<string, Uint8Array>, entryPath: string): T {
  const content = entries[entryPath];
  if (!content) {
    throw new WorkspacePackageOpenError(`This workspace package is missing ${entryPath}. Download it again.`);
  }
  try {
    return JSON.parse(new TextDecoder().decode(content)) as T;
  } catch {
    throw new WorkspacePackageOpenError(`This workspace package has invalid ${entryPath}. Download it again.`);
  }
}
