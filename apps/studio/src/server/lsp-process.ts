import { type ChildProcessWithoutNullStreams, spawn } from 'node:child_process';
import { dotnetArgsFor, resolveDotnetTool } from './dotnet-tool';
import { resolveWorkspaceRoot } from './workspace';

const LANGUAGE_SERVER = {
  envVar: 'MODELLER_LANGUAGE_SERVER_PATH',
  bundledDllRelativePath: 'server-bin/Modeller.LanguageServer.dll',
  projectRelativePath: 'src/Modeller.LanguageServer/Modeller.LanguageServer.csproj',
  // See DotnetToolConfig.requireBundledDll: this process's stdout is the LSP
  // wire protocol itself, so `dotnet run --project`'s stdout-sharing MSBuild
  // chatter is never an acceptable fallback here.
  requireBundledDll: true,
};

export function spawnLanguageServer(): ChildProcessWithoutNullStreams {
  const location = resolveDotnetTool(LANGUAGE_SERVER);
  // MODELLER_WORKSPACE_ROOT gives the server (which runs locally, on the same
  // filesystem as the workspace — see lsp-process.ts's own spawn) real
  // workspace/multi-file awareness: it reads .modeller/config.json's sources
  // itself instead of only ever seeing whichever document a client happened
  // to didOpen. See wayfinder decision #59.
  return spawn('dotnet', dotnetArgsFor(location), {
    stdio: ['pipe', 'pipe', 'pipe'],
    env: { ...process.env, MODELLER_WORKSPACE_ROOT: resolveWorkspaceRoot() },
  });
}
