# Modeller RML for VS Code

The extension provides immediate RML and SAF syntax highlighting and starts the
Modeller language server for diagnostics, semantic completion, hover,
definition, references, rename, symbols, and semantic tokens.

From this repository:

```powershell
dotnet build src/Modeller.LanguageServer/Modeller.LanguageServer.csproj
cd editors/vscode-modeller
npm install
npm run bundle
```

Open the repository root in the Extension Development Host. The extension finds
the language-server project automatically. Packaged installations may configure
`modeller.languageServer.path` or bundle the published server under `server/`.
