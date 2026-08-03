// Registers plain monaco-editor's own provider APIs — the same
// "IntelliSense" surface any Monaco-based tool uses (TypeScript Playground,
// etc.), no VS Code workbench involved — against an already-initialized
// LspConnection. Providers are registered once per language (see session.ts,
// which calls registerLanguageProviders at most once per languageId for the
// session's single shared connection) rather than once per document/model
// instance, so every provider here is keyed off the `model` argument Monaco
// passes into each provide* callback instead of a single closed-over
// document — the same provider instance answers hover/completion/etc. for
// whichever open document Monaco is currently asking about.
import type * as Monaco from 'monaco-editor';
import { LspConnection } from './protocol';

export function registerLanguageProviders(
  monaco: typeof Monaco,
  connection: LspConnection,
  languageId: string,
  semanticTokensLegend: Monaco.languages.SemanticTokensLegend | undefined,
  semanticTokensChanged: Monaco.Emitter<void>,
): Monaco.IDisposable[] {
  const disposables: Monaco.IDisposable[] = [];

  disposables.push(
    monaco.languages.registerHoverProvider(languageId, {
      provideHover: async (model, position) => {
        const result = await connection.request<{ contents: string; range?: Monaco.IRange } | null>('textDocument/hover', {
          textDocument: { uri: model.uri.toString() },
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
      provideCompletionItems: async (model, position) => {
        type CompletionItem = { label: string; kind?: number; detail?: string };
        const result = await connection.request<CompletionItem[] | { items: CompletionItem[] } | null>('textDocument/completion', {
          textDocument: { uri: model.uri.toString() },
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
      provideDefinition: async (model, position) => {
        const result = await connection.request<{ uri: string; range: Monaco.IRange }[] | null>('textDocument/definition', {
          textDocument: { uri: model.uri.toString() },
          position: toLspPosition(position),
        });
        return (result ?? []).map((location) => ({ uri: monaco.Uri.parse(location.uri), range: location.range }));
      },
    }),
  );

  disposables.push(
    monaco.languages.registerReferenceProvider(languageId, {
      provideReferences: async (model, position) => {
        const result = await connection.request<{ uri: string; range: Monaco.IRange }[] | null>('textDocument/references', {
          textDocument: { uri: model.uri.toString() },
          position: toLspPosition(position),
          context: { includeDeclaration: true },
        });
        return (result ?? []).map((location) => ({ uri: monaco.Uri.parse(location.uri), range: location.range }));
      },
    }),
  );

  disposables.push(
    monaco.languages.registerRenameProvider(languageId, {
      provideRenameEdits: async (model, position, newName) => {
        const uri = model.uri.toString();
        const result = await connection.request<{
          changes?: Record<string, { range: Monaco.IRange; newText: string }[]>;
        } | null>('textDocument/rename', { textDocument: { uri }, position: toLspPosition(position), newName });
        const edits = (result?.changes?.[uri] ?? []).map((edit) => ({
          resource: model.uri,
          textEdit: { range: edit.range, text: edit.newText },
          versionId: undefined,
        }));
        return { edits };
      },
    }),
  );

  disposables.push(
    monaco.languages.registerDocumentSymbolProvider(languageId, {
      provideDocumentSymbols: async (model) => {
        const result = await connection.request<Monaco.languages.DocumentSymbol[] | null>('textDocument/documentSymbol', {
          textDocument: { uri: model.uri.toString() },
        });
        return result ?? [];
      },
    }),
  );

  if (semanticTokensLegend) {
    disposables.push(
      monaco.languages.registerDocumentSemanticTokensProvider(languageId, {
        onDidChange: semanticTokensChanged.event,
        getLegend: () => semanticTokensLegend,
        provideDocumentSemanticTokens: async (model) => {
          const result = await connection.request<{ data: number[] } | null>('textDocument/semanticTokens/full', {
            textDocument: { uri: model.uri.toString() },
          });
          if (!result) return null;
          return { data: Uint32Array.from(result.data), resultId: undefined };
        },
        releaseDocumentSemanticTokens: () => {},
      }),
    );
  }

  return disposables;
}

export function toLspPosition(position: Monaco.Position): { line: number; character: number } {
  return { line: position.lineNumber - 1, character: position.column - 1 };
}

export function toMonacoSeverity(monaco: typeof Monaco, severity: number | undefined): Monaco.MarkerSeverity {
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
