// Builds the downloadable local Modeller workspace (issue #73) — a zip a visitor can hand off to
// the CLI, local Studio, or VS Code. Assembled entirely client-side from the export response
// (see api-client.ts's exportWorkspace) plus the bundled template pack (template-pack.ts); nothing
// here reads local disk or calls a server beyond the one /v1/workspace/export round trip.
import { strToU8, zipSync, type Zippable } from 'fflate';
import { TEMPLATE_PACK_FILES, TEMPLATE_PACK_ROOT } from './template-pack';
import type { ConfigurationDto, DurableIdentityDto, WorkspaceDocumentDto } from './api-client';

export const MODELLER_WORKSPACE_FILE_NAME = 'modeller-workspace.modeller-workspace';
export const MODELLER_WORKSPACE_MEDIA_TYPE = 'application/vnd.modeller.workspace+zip';

const DETERMINISTIC_PACKAGE_MTIME = new Date('1980-01-01T00:00:00.000Z');
const DETERMINISTIC_PACKAGE_OPTIONS = { mtime: DETERMINISTIC_PACKAGE_MTIME } as const;

const HANDOFF_README = `# Modeller Studio workspace

Downloaded from the Modeller playground. This is a complete local workspace — RML documents,
configuration, a durable identity registry, and generation templates.

## Open locally

1. Install Modeller Studio for Windows from the reader path.
2. Open this package.
3. Studio opens the workspace and shows diagnostics and generation locally.

The primary reader path is: try the playground, download the workspace, install Studio for
Windows, open the package, and see the workspace.
`;

function buildPackageJson(): string {
  return JSON.stringify(
    {
      packageVersion: '1.0',
      packageKind: 'ModellerStudioWorkspace',
      displayName: 'Modeller Studio workspace package',
      windowsFileExtension: '.modeller-workspace',
      opensWith: 'Modeller Studio',
      createdBy: 'Modeller Playground',
    },
    null,
    2,
  );
}

function buildConfigJson(configuration: ConfigurationDto, sources: readonly string[]): string {
  return JSON.stringify(
    {
      version: '1.0',
      generationContractVersion: configuration.generationContractVersion,
      logicalOutputRoot: configuration.logicalOutputRoot,
      profile: configuration.profile,
      sources,
      templatePack: `${TEMPLATE_PACK_ROOT}/pack.json`,
      identityRegistry: '.modeller/identities.json',
      // Required by the CLI's own workspace loader (src/Modeller.Cli/WorkspaceLoader.cs
      // IsValidConfiguration/HasValidParameters) — a config.json without a non-empty
      // parameters.projectName fails to load at all via `project --workspace`/`generate
      // --workspace`, even though it's optional for the hosted API's own Analyze/Export.
      // Hardcoded to the Ordering example's own values (matches samples/ordering/.modeller/config.json)
      // since that's the only example the playground currently supports.
      parameters: { projectName: 'Ordering', csharp: { namespace: 'Ordering', targetFramework: 'net10.0' } },
    },
    null,
    2,
  );
}

function buildIdentitiesJson(identity: DurableIdentityDto): string {
  return JSON.stringify({ version: identity.version, documents: identity.documents }, null, 2);
}

function zipTextEntry(content: string): [Uint8Array, typeof DETERMINISTIC_PACKAGE_OPTIONS] {
  return [strToU8(content), DETERMINISTIC_PACKAGE_OPTIONS];
}

export function buildWorkspaceZip(
  documents: readonly WorkspaceDocumentDto[],
  identity: DurableIdentityDto,
  configuration: ConfigurationDto,
): Uint8Array {
  const files: Zippable = {
    '.modeller/config.json': zipTextEntry(buildConfigJson(configuration, documents.map((document) => document.path))),
    '.modeller/identities.json': zipTextEntry(buildIdentitiesJson(identity)),
    '.modeller/package.json': zipTextEntry(buildPackageJson()),
    'README.md': zipTextEntry(HANDOFF_README),
  };
  for (const document of documents) files[document.path] = zipTextEntry(document.content);
  for (const template of TEMPLATE_PACK_FILES) files[template.path] = zipTextEntry(template.content);

  return zipSync(files, DETERMINISTIC_PACKAGE_OPTIONS);
}

export function downloadWorkspaceZip(bytes: Uint8Array, fileName = MODELLER_WORKSPACE_FILE_NAME): void {
  const blob = new Blob([bytes as BlobPart], { type: MODELLER_WORKSPACE_MEDIA_TYPE });
  const url = URL.createObjectURL(blob);
  try {
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    anchor.click();
  } finally {
    URL.revokeObjectURL(url);
  }
}
