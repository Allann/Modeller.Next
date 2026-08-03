import { type ChildProcessWithoutNullStreams, spawn } from 'node:child_process';
import { dotnetArgsFor, resolveDotnetTool } from './dotnet-tool';

const LANGUAGE_SERVER = {
  envVar: 'MODELLER_LANGUAGE_SERVER_PATH',
  bundledDllRelativePath: 'server-bin/Modeller.LanguageServer.dll',
  projectRelativePath: 'src/Modeller.LanguageServer/Modeller.LanguageServer.csproj',
};

export function spawnLanguageServer(): ChildProcessWithoutNullStreams {
  const location = resolveDotnetTool(LANGUAGE_SERVER);
  return spawn('dotnet', dotnetArgsFor(location), { stdio: ['pipe', 'pipe', 'pipe'] });
}
