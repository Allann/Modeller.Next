#define AppName "Modeller Studio"
#define AppVersion "0.1.0"
#define AppPublisher "Modeller"
#define AppExeName "ModellerStudio.cmd"

[Setup]
AppId={{3B80C5B5-96F0-48EF-B37F-29F73F68E70D}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
OutputBaseFilename=ModellerStudioSetup
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
ChangesAssociations=yes

[Files]
Source: "..\dist\windows\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"

[Registry]
Root: HKCR; Subkey: ".modeller-workspace"; ValueType: string; ValueName: ""; ValueData: "ModellerStudio.WorkspacePackage"; Flags: uninsdeletevalue
Root: HKCR; Subkey: ".modeller-workspace"; ValueType: string; ValueName: "Content Type"; ValueData: "application/vnd.modeller.workspace+zip"; Flags: uninsdeletevalue
Root: HKCR; Subkey: "ModellerStudio.WorkspacePackage"; ValueType: string; ValueName: ""; ValueData: "Modeller Studio workspace package"; Flags: uninsdeletekey
Root: HKCR; Subkey: "ModellerStudio.WorkspacePackage\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#AppExeName},0"
Root: HKCR; Subkey: "ModellerStudio.WorkspacePackage\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExeName}"" --open-workspace-package ""%1"""

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Start Modeller Studio"; Flags: nowait postinstall skipifsilent
