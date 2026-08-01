import * as fs from 'node:fs';
import * as path from 'node:path';
import * as vscode from 'vscode';
import { LanguageClient, LanguageClientOptions, ServerOptions } from 'vscode-languageclient/node';

let client: LanguageClient | undefined;

export async function activate(context: vscode.ExtensionContext): Promise<void> {
  const output = vscode.window.createOutputChannel('Modeller');
  context.subscriptions.push(output);
  const server = resolveServer(context);
  if (!server) {
    const message = 'Modeller language server was not found. Build Modeller.LanguageServer or configure modeller.languageServer.path.';
    output.appendLine(message);
    void vscode.window.showErrorMessage(message);
    return;
  }
  const serverOptions: ServerOptions = { command: 'dotnet', args: server.kind === 'dll' ? [server.path] : ['run', '--project', server.path, '--no-launch-profile'] };
  const clientOptions: LanguageClientOptions = {
    documentSelector: [{ scheme: 'file', language: 'modeller-rml' }, { scheme: 'file', language: 'modeller-saf' }],
    outputChannel: output,
    synchronize: { fileEvents: vscode.workspace.createFileSystemWatcher('**/*.{modeller,saf}') }
  };
  client = new LanguageClient('modeller-rml', 'Modeller RML Language Server', serverOptions, clientOptions);
  context.subscriptions.push(client);
  await client.start();
}

export async function deactivate(): Promise<void> {
  await client?.stop();
}

function resolveServer(context: vscode.ExtensionContext): { kind: 'dll' | 'project'; path: string } | undefined {
  const configured = vscode.workspace.getConfiguration('modeller').get<string>('languageServer.path');
  if (configured && fs.existsSync(configured)) return { kind: 'dll', path: configured };
  const bundled = path.join(context.extensionPath, 'server', 'Modeller.LanguageServer.dll');
  if (fs.existsSync(bundled)) return { kind: 'dll', path: bundled };
  for (const folder of vscode.workspace.workspaceFolders ?? []) {
    const project = path.join(folder.uri.fsPath, 'src', 'Modeller.LanguageServer', 'Modeller.LanguageServer.csproj');
    if (fs.existsSync(project)) return { kind: 'project', path: project };
  }
  return undefined;
}
