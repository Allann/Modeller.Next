// Owns the single LspConnection (and single spawned Modeller.LanguageServer
// process, via the /lsp WebSocket bridge) for the whole browser session,
// multiplexing every open document onto it via textDocument/didOpen instead
// of the earlier one-connection-per-document approach — see wayfinder
// decision #52's "one process per session" intent, and issue #56.
//
// Modeller.LanguageServer has no per-connection workspace concept (see
// src/Modeller.LanguageServer/Program.cs — documents are tracked purely by
// didOpen/didChange/didClose, in one dictionary shared across whatever's
// open), so multiplexing is safe to do entirely client-side.
import type * as Monaco from 'monaco-editor';
import { LspConnection } from './protocol';
import { registerLanguageProviders, toMonacoSeverity } from './monaco-bridge';

interface InitializeResult {
  capabilities?: {
    semanticTokensProvider?: { legend?: Monaco.languages.SemanticTokensLegend };
  };
}

interface OpenDocument {
  model: Monaco.editor.ITextModel;
  version: number;
}

interface SessionState {
  monaco: typeof Monaco;
  connection: LspConnection;
  initializeResult: InitializeResult;
  semanticTokensChanged: Monaco.Emitter<void>;
}

let sessionPromise: Promise<SessionState> | undefined;
const registeredLanguages = new Set<string>();
const openDocuments = new Map<string, OpenDocument>();

function getSession(monaco: typeof Monaco): Promise<SessionState> {
  sessionPromise ??= (async () => {
    const connection = new LspConnection(lspSocketUrl());
    await connection.whenReady();

    const initializeResult = await connection.request<InitializeResult>('initialize', {
      processId: null,
      rootUri: null,
      capabilities: {
        textDocument: {
          synchronization: { didSave: false },
          hover: { contentFormat: ['plaintext', 'markdown'] },
          completion: { completionItem: { snippetSupport: false } },
          publishDiagnostics: {},
          semanticTokens: { requests: { full: true } },
        },
      },
    });
    connection.notify('initialized', {});

    connection.onNotification('textDocument/publishDiagnostics', (params) => {
      const { uri, diagnostics } = params as {
        uri: string;
        diagnostics: { range: Monaco.IRange; message: string; severity?: number; code?: string }[];
      };
      const document = openDocuments.get(uri);
      if (!document) return;
      monaco.editor.setModelMarkers(
        document.model,
        'modeller-lsp',
        diagnostics.map((diagnostic) => ({
          startLineNumber: diagnostic.range.startLineNumber,
          startColumn: diagnostic.range.startColumn,
          endLineNumber: diagnostic.range.endLineNumber,
          endColumn: diagnostic.range.endColumn,
          message: diagnostic.message,
          severity: toMonacoSeverity(monaco, diagnostic.severity),
          code: diagnostic.code,
        })),
      );
    });

    return { monaco, connection, initializeResult, semanticTokensChanged: new monaco.Emitter<void>() };
  })();
  return sessionPromise;
}

/**
 * Opens `uri` on the session's shared connection (sending textDocument/didOpen,
 * registering this language's providers the first time it's seen, and wiring
 * didChange/didClose) and returns a handle whose `dispose()` closes just this
 * document — the connection and process it multiplexed onto keep running for
 * whatever else is still open.
 */
export async function openDocument(
  monaco: typeof Monaco,
  languageId: string,
  uri: string,
  model: Monaco.editor.ITextModel,
): Promise<{ dispose: () => void }> {
  const session = await getSession(monaco);

  const document: OpenDocument = { model, version: 1 };
  openDocuments.set(uri, document);
  session.connection.notify('textDocument/didOpen', {
    textDocument: { uri, languageId, version: document.version, text: model.getValue() },
  });

  if (!registeredLanguages.has(languageId)) {
    registeredLanguages.add(languageId);
    registerLanguageProviders(
      monaco,
      session.connection,
      languageId,
      session.initializeResult.capabilities?.semanticTokensProvider?.legend,
      session.semanticTokensChanged,
    );
  }

  const changeSubscription = model.onDidChangeContent(() => {
    document.version += 1;
    session.connection.notify('textDocument/didChange', {
      textDocument: { uri, version: document.version },
      contentChanges: [{ text: model.getValue() }],
    });
    session.semanticTokensChanged.fire();
  });

  return {
    dispose: () => {
      changeSubscription.dispose();
      openDocuments.delete(uri);
      session.connection.notify('textDocument/didClose', { textDocument: { uri } });
    },
  };
}

function lspSocketUrl(): string {
  const { protocol, host } = window.location;
  const wsProtocol = protocol === 'https:' ? 'wss:' : 'ws:';
  return `${wsProtocol}//${host}/lsp`;
}
