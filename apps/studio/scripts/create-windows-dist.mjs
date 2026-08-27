import { copyFileSync, cpSync, existsSync, mkdirSync, rmSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const studioRoot = path.resolve(here, '..');
const distRoot = path.join(studioRoot, 'packaging', 'dist', 'windows');

for (const requiredPath of [
  '.next',
  'node_modules',
  'server-bin/Modeller.Cli.dll',
  'server-bin/Modeller.LanguageServer.dll',
]) {
  if (!existsSync(path.join(studioRoot, requiredPath))) {
    throw new Error(`Missing ${requiredPath}. Run npm run build and npm run server:build before packaging Studio.`);
  }
}
const vendorNode = path.join(studioRoot, 'packaging', 'vendor', 'node', 'node.exe');
const runtimeNode = existsSync(vendorNode) ? vendorNode : process.execPath;

rmSync(distRoot, { recursive: true, force: true });
mkdirSync(distRoot, { recursive: true });
copyFileSync(path.join(studioRoot, 'package.json'), path.join(distRoot, 'package.json'));
copyFileSync(path.join(studioRoot, 'package-lock.json'), path.join(distRoot, 'package-lock.json'));
copyFileSync(path.join(studioRoot, 'server.ts'), path.join(distRoot, 'server.ts'));
cpSync(path.join(studioRoot, '.next'), path.join(distRoot, '.next'), { recursive: true });
cpSync(path.join(studioRoot, 'node_modules'), path.join(distRoot, 'node_modules'), { recursive: true });
cpSync(path.join(studioRoot, 'public'), path.join(distRoot, 'public'), { recursive: true });
cpSync(path.join(studioRoot, 'server-bin'), path.join(distRoot, 'server-bin'), { recursive: true });
cpSync(path.join(studioRoot, 'src'), path.join(distRoot, 'src'), { recursive: true });
mkdirSync(path.join(distRoot, 'runtime'), { recursive: true });
copyFileSync(runtimeNode, path.join(distRoot, 'runtime', 'node.exe'));

writeFileSync(
  path.join(distRoot, 'ModellerStudio.cmd'),
  [
    '@echo off',
    'setlocal',
    'set "PORT=3100"',
    'set "NODE_ENV=production"',
    'pushd "%~dp0"',
    'start "" /min powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Sleep -Milliseconds 1500; Start-Process \'http://localhost:3100\'"',
    'runtime\\node.exe node_modules\\tsx\\dist\\cli.mjs server.ts %*',
    'popd',
  ].join('\r\n'),
);

writeFileSync(
  path.join(distRoot, 'ModellerStudio.vbs'),
  [
    'Set shell = CreateObject("WScript.Shell")',
    'Set args = WScript.Arguments',
    'command = Chr(34) & Replace(WScript.ScriptFullName, "ModellerStudio.vbs", "ModellerStudio.cmd") & Chr(34)',
    'For Each arg In args',
    '  command = command & " " & Chr(34) & arg & Chr(34)',
    'Next',
    'shell.Run command, 0, False',
  ].join('\r\n'),
);

writeFileSync(
  path.join(distRoot, 'InstallModellerStudio.cmd'),
  [
    '@echo off',
    'setlocal',
    'set "APPDIR=%~dp0"',
    'set "COMMAND=wscript.exe \\"%APPDIR%ModellerStudio.vbs\\" \\"%%1\\""',
    'reg add HKCU\\Software\\Classes\\.modeller-workspace /ve /d ModellerStudio.Workspace /f >nul',
    'reg add HKCU\\Software\\Classes\\ModellerStudio.Workspace /ve /d "Modeller Studio workspace" /f >nul',
    'reg add HKCU\\Software\\Classes\\ModellerStudio.Workspace\\DefaultIcon /ve /d "\\"%APPDIR%ModellerStudio.cmd\\",0" /f >nul',
    'reg add HKCU\\Software\\Classes\\ModellerStudio.Workspace\\shell\\open\\command /ve /d "%COMMAND%" /f >nul',
    'echo Modeller Studio is installed for this Windows user.',
    'echo You can now double-click .modeller-workspace files to open them locally.',
    'pause',
  ].join('\r\n'),
);

writeFileSync(
  path.join(distRoot, 'ModellerStudioSetup.iss'),
  [
    '#define MyAppName "Modeller Studio"',
    '#define MyAppVersion "0.1.0"',
    '#define MyAppPublisher "Modeller"',
    '',
    '[Setup]',
    'AppId={{7B2B48FA-A7CE-48F0-845B-4B3F4826D795}',
    'AppName={#MyAppName}',
    'AppVersion={#MyAppVersion}',
    'AppPublisher={#MyAppPublisher}',
    'DefaultDirName={localappdata}\\Modeller Studio',
    'DefaultGroupName={#MyAppName}',
    'OutputDir=.',
    'OutputBaseFilename=ModellerStudioSetup',
    'Compression=lzma2',
    'SolidCompression=yes',
    'PrivilegesRequired=lowest',
    '',
    '[Files]',
    'Source: "*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "ModellerStudioSetup.iss"',
    '',
    '[Registry]',
    'Root: HKCU; Subkey: "Software\\Classes\\.modeller-workspace"; ValueType: string; ValueName: ""; ValueData: "ModellerStudio.Workspace"; Flags: uninsdeletevalue',
    'Root: HKCU; Subkey: "Software\\Classes\\ModellerStudio.Workspace"; ValueType: string; ValueName: ""; ValueData: "Modeller Studio workspace"; Flags: uninsdeletekey',
    'Root: HKCU; Subkey: "Software\\Classes\\ModellerStudio.Workspace\\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\\ModellerStudio.cmd,0"',
    'Root: HKCU; Subkey: "Software\\Classes\\ModellerStudio.Workspace\\shell\\open\\command"; ValueType: string; ValueName: ""; ValueData: "wscript.exe ""{app}\\ModellerStudio.vbs"" ""%1"""',
    '',
    '[Icons]',
    'Name: "{group}\\Modeller Studio"; Filename: "{app}\\ModellerStudio.vbs"',
    '',
    '[Run]',
    'Filename: "{app}\\ModellerStudio.vbs"; Description: "Start Modeller Studio"; Flags: nowait postinstall skipifsilent',
  ].join('\r\n'),
);

writeFileSync(
  path.join(distRoot, 'README.txt'),
  [
    'Modeller Studio for Windows',
    '',
    'Install once from the public reader path:',
    '1. Run ModellerStudioSetup.exe.',
    '2. Windows registers .modeller-workspace files for this user.',
    '',
    'Open a downloaded playground workspace:',
    '1. Go to the wiki landing page.',
    '2. Open the playground.',
    '3. Download the workspace.',
    '4. Double-click the downloaded .modeller-workspace file.',
    '5. Modeller Studio starts and opens the workspace.',
    '',
    'For local packaging checks, InstallModellerStudio.cmd registers the same file association without building the setup executable.',
  ].join('\r\n'),
);

console.log(`Prepared Windows Studio distribution in ${distRoot}`);
