// Registers the modeller-rml/modeller-saf languages and their existing
// TextMate grammars (copied into public/grammars by scripts/copy-grammars.mjs
// from editors/vscode-modeller) against plain monaco-editor, via
// monaco-textmate + onigasm (the standard "TextMate grammars on plain Monaco"
// bridge, since Monaco has no native TextMate support of its own).
import type * as Monaco from 'monaco-editor';
import { loadWASM } from 'onigasm';
import { Registry } from 'monaco-textmate';
import { wireTmGrammars } from 'monaco-editor-textmate';

const LANGUAGES = [
  { id: 'modeller-rml', extensions: ['.modeller'], scopeName: 'source.modeller.rml', grammarFile: 'rml.tmLanguage.json' },
  { id: 'modeller-saf', extensions: ['.saf'], scopeName: 'source.modeller.saf', grammarFile: 'saf.tmLanguage.json' },
] as const;

let registered: Promise<void> | undefined;

export function registerModellerLanguages(monaco: typeof Monaco): Promise<void> {
  if (!registered) registered = doRegister(monaco);
  return registered;
}

async function doRegister(monaco: typeof Monaco): Promise<void> {
  await loadWASM('/grammars/onigasm.wasm');

  const languageConfiguration = await fetch('/grammars/language-configuration.json').then((response) => response.json());

  for (const language of LANGUAGES) {
    monaco.languages.register({ id: language.id, extensions: [...language.extensions] });
    monaco.languages.setLanguageConfiguration(language.id, languageConfiguration as Monaco.languages.LanguageConfiguration);
  }

  const registry = new Registry({
    getGrammarDefinition: async (scopeName) => {
      const language = LANGUAGES.find((entry) => entry.scopeName === scopeName);
      const content = await fetch(`/grammars/${language?.grammarFile}`).then((response) => response.text());
      return { format: 'json', content };
    },
  });

  const grammars = new Map(LANGUAGES.map((language) => [language.id, language.scopeName]));
  await wireTmGrammars(monaco, registry, grammars);
}

export function languageIdForPath(path: string): 'modeller-rml' | 'modeller-saf' | 'plaintext' {
  if (path.endsWith('.modeller')) return 'modeller-rml';
  if (path.endsWith('.saf')) return 'modeller-saf';
  return 'plaintext';
}
