import { type ChildProcessWithoutNullStreams, spawn } from 'node:child_process';
import { dotnetArgsFor, resolveDotnetTool } from './dotnet-tool';

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
  return spawn('dotnet', dotnetArgsFor(location), { stdio: ['pipe', 'pipe', 'pipe'] });
}
