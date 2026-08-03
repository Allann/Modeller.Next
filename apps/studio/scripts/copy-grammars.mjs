// Copies the existing TextMate grammars from editors/vscode-modeller into
// public/grammars so the browser can fetch them — avoids duplicating grammar
// content by hand, keeps this app's syntax highlighting in sync with the
// VS Code extension's grammars automatically.
import { copyFileSync, mkdirSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const extensionRoot = path.resolve(here, '..', '..', '..', 'editors', 'vscode-modeller');
const target = path.resolve(here, '..', 'public', 'grammars');

mkdirSync(target, { recursive: true });
for (const file of ['syntaxes/rml.tmLanguage.json', 'syntaxes/saf.tmLanguage.json', 'language-configuration.json']) {
  copyFileSync(path.join(extensionRoot, file), path.join(target, path.basename(file)));
}

// onigasm's WASM regex engine, needed by monaco-textmate for grammar tokenization.
const onigasmWasm = path.resolve(here, '..', 'node_modules', 'onigasm', 'lib', 'onigasm.wasm');
copyFileSync(onigasmWasm, path.join(target, 'onigasm.wasm'));

console.log(`Copied grammars into ${target}`);
