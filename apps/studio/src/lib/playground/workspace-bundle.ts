// Builds the downloadable local Modeller workspace (issue #73) — a zip a visitor can hand off to
// the CLI, local Studio, or VS Code. Assembled entirely client-side from the export response
// (see api-client.ts's exportWorkspace) plus the bundled template pack (template-pack.ts); nothing
// here reads local disk or calls a server beyond the one /v1/workspace/export round trip.
import { strToU8, zipSync } from 'fflate';
import { TEMPLATE_PACK_FILES, TEMPLATE_PACK_ROOT } from './template-pack';
import type { ConfigurationDto, DurableIdentityDto, WorkspaceDocumentDto } from './api-client';

const HANDOFF_README = `# Modeller workspace

Downloaded from the Modeller playground. This is a complete local workspace — RML documents,
configuration, a durable identity registry, and a C# generation template pack.

## Command line

\`\`\`
dotnet run --project src/Modeller.Cli -- validate model/context.modeller
dotnet run --project src/Modeller.Cli -- project --workspace . --view Lifecycle
dotnet run --project src/Modeller.Cli -- generate --workspace . --dry-run
\`\`\`

(run from inside a checkout of https://github.com/Allann/Modeller.Next, with this folder as the
workspace — see the CLI's own \`--help\` for every command.)

## Local Studio

Set \`MODELLER_STUDIO_WORKSPACE\` to this folder's path and run \`npm run dev\` in \`apps/studio\`.

## VS Code

Install the Modeller VS Code extension (\`editors/vscode-modeller\`) and open this folder.
`;

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
    },
    null,
    2,
  );
}

function buildIdentitiesJson(identity: DurableIdentityDto): string {
  return JSON.stringify({ version: identity.version, documents: identity.documents }, null, 2);
}

export function buildWorkspaceZip(
  documents: readonly WorkspaceDocumentDto[],
  identity: DurableIdentityDto,
  configuration: ConfigurationDto,
): Uint8Array {
  const files: Record<string, Uint8Array> = {
    '.modeller/config.json': strToU8(buildConfigJson(configuration, documents.map((document) => document.path))),
    '.modeller/identities.json': strToU8(buildIdentitiesJson(identity)),
    'README.md': strToU8(HANDOFF_README),
  };
  for (const document of documents) files[document.path] = strToU8(document.content);
  for (const template of TEMPLATE_PACK_FILES) files[template.path] = strToU8(template.content);

  return zipSync(files);
}

export function downloadWorkspaceZip(bytes: Uint8Array, fileName = 'modeller-workspace.zip'): void {
  const blob = new Blob([bytes as BlobPart], { type: 'application/zip' });
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
