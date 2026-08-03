// Wires an LspConnection to plain monaco-editor's own provider APIs — the
// same "IntelliSense" surface any Monaco-based tool uses (TypeScript
// Playground, etc.), no VS Code workbench involved. Registers providers for
// every LSP method Modeller.LanguageServer implements: hover, completion,
// definition, references, rename, document symbols, diagnostics, and
// semantic tokens (full only — the server only implements
// textDocument/semanticTokens/full, not the delta variant, so there's no
// delta complexity to handle client-side either).
import type * as Monaco from 'monaco-editor';
import { LspConnection } from './protocol';

interface AttachOptions {
  monaco: typeof Monaco;
  connection: LspConnection;
  languageId: string;
  uri: string;
  model: Monaco.editor.ITextModel;
}

export async function attachLanguageServer({ monaco, connection, languageId, uri, model }: AttachOptions): Promise<{ dispose: () => void }> {
  await connection.whenReady();

  const initializeResult = await connection.request<{
    capabilities?: { semanticTokensProvider?: { legend?: Monaco.languages.SemanticTokensLegend } };
  }>('initialize', {
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
  connection.notify('textDocument/didOpen', {
    textDocument: { uri, languageId, version: 1, text: model.getValue() },
  });

  const disposables: Monaco.IDisposable[] = [];

  connection.onNotification('textDocument/publishDiagnostics', (params) => {
    const { uri: diagnosticUri, diagnostics } = params as {
      uri: string;
      diagnostics: { range: Monaco.IRange; message: string; severity?: number; code?: string }[];
    };
    if (diagnosticUri !== uri) return;
    monaco.editor.setModelMarkers(
      model,
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

  // Monaco only re-invokes provideDocumentSemanticTokens when this fires — it
  // has no other way to learn that an edit made the previous tokens stale.
  const semanticTokensChanged = new monaco.Emitter<void>();
  disposables.push(semanticTokensChanged);

  let version = 1;
  disposables.push(
    model.onDidChangeContent(() => {
      version += 1;
      connection.notify('textDocument/didChange', {
        textDocument: { uri, version },
        contentChanges: [{ text: model.getValue() }],
      });
      semanticTokensChanged.fire();
    }),
  );

  disposables.push(
    monaco.languages.registerHoverProvider(languageId, {
      provideHover: async (_, position) => {
        const result = await connection.request<{ contents: string; range?: Monaco.IRange } | null>('textDocument/hover', {
          textDocument: { uri },
          position: toLspPosition(position),
        });
        if (!result) return null;
        return { contents: [{ value: typeof result.contents === 'string' ? result.contents : JSON.stringify(result.contents) }], range: result.range };
      },
    }),
  );

  disposables.push(
    monaco.languages.registerCompletionItemProvider(languageId, {
      triggerCharacters: ['"', ' '],
      provideCompletionItems: async (_, position) => {
        type CompletionItem = { label: string; kind?: number; detail?: string };
        const result = await connection.request<CompletionItem[] | { items: CompletionItem[] } | null>('textDocument/completion', {
          textDocument: { uri },
          position: toLspPosition(position),
        });
        const items = Array.isArray(result) ? result : (result?.items ?? []);
        const word = model.getWordUntilPosition(position);
        const range = {
          startLineNumber: position.lineNumber,
          endLineNumber: position.lineNumber,
          startColumn: word.startColumn,
          endColumn: word.endColumn,
        };
        return {
          suggestions: items.map((item) => ({
            label: item.label,
            kind: item.kind === 14 ? monaco.languages.CompletionItemKind.Keyword : monaco.languages.CompletionItemKind.Variable,
            detail: item.detail,
            insertText: item.label,
            range,
          })),
        };
      },
    }),
  );

  disposables.push(
    monaco.languages.registerDefinitionProvider(languageId, {
      provideDefinition: async (_, position) => {
        const result = await connection.request<{ uri: string; range: Monaco.IRange }[] | null>('textDocument/definition', {
          textDocument: { uri },
          position: toLspPosition(position),
        });
        return (result ?? []).map((location) => ({ uri: monaco.Uri.parse(location.uri), range: location.range }));
      },
    }),
  );

  disposables.push(
    monaco.languages.registerReferenceProvider(languageId, {
      provideReferences: async (_, position) => {
        const result = await connection.request<{ uri: string; range: Monaco.IRange }[] | null>('textDocument/references', {
          textDocument: { uri },
          position: toLspPosition(position),
          context: { includeDeclaration: true },
        });
        return (result ?? []).map((location) => ({ uri: monaco.Uri.parse(location.uri), range: location.range }));
      },
    }),
  );

  disposables.push(
    monaco.languages.registerRenameProvider(languageId, {
      provideRenameEdits: async (_, position, newName) => {
        const result = await connection.request<{
          changes?: Record<string, { range: Monaco.IRange; newText: string }[]>;
        } | null>('textDocument/rename', { textDocument: { uri }, position: toLspPosition(position), newName });
        const edits = (result?.changes?.[uri] ?? []).map((edit) => ({
          resource: monaco.Uri.parse(uri),
          textEdit: { range: edit.range, text: edit.newText },
          versionId: undefined,
        }));
        return { edits };
      },
    }),
  );

  disposables.push(
    monaco.languages.registerDocumentSymbolProvider(languageId, {
      provideDocumentSymbols: async () => {
        const result = await connection.request<Monaco.languages.DocumentSymbol[] | null>('textDocument/documentSymbol', {
          textDocument: { uri },
        });
        return result ?? [];
      },
    }),
  );

  const legend = initializeResult.capabilities?.semanticTokensProvider?.legend;
  if (legend) {
    disposables.push(
      monaco.languages.registerDocumentSemanticTokensProvider(languageId, {
        onDidChange: semanticTokensChanged.event,
        getLegend: () => legend,
        provideDocumentSemanticTokens: async () => {
          const result = await connection.request<{ data: number[] } | null>('textDocument/semanticTokens/full', {
            textDocument: { uri },
          });
          if (!result) return null;
          return { data: Uint32Array.from(result.data), resultId: undefined };
        },
        releaseDocumentSemanticTokens: () => {},
      }),
    );
  }

  return {
    dispose: () => {
      for (const disposable of disposables) disposable.dispose();
    },
  };
}

function toLspPosition(position: Monaco.Position): { line: number; character: number } {
  return { line: position.lineNumber - 1, character: position.column - 1 };
}

function toMonacoSeverity(monaco: typeof Monaco, severity: number | undefined): Monaco.MarkerSeverity {
  switch (severity) {
    case 2:
      return monaco.MarkerSeverity.Warning;
    case 3:
      return monaco.MarkerSeverity.Info;
    case 4:
      return monaco.MarkerSeverity.Hint;
    default:
      return monaco.MarkerSeverity.Error;
  }
}
