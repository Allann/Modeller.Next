import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import test from 'node:test';

const root = path.resolve(import.meta.dirname, '..');
const manifest = JSON.parse(fs.readFileSync(path.join(root, 'package.json'), 'utf8'));

test('registers separate RML and SAF language modes', () => {
  const languages = new Map(manifest.contributes.languages.map(language => [language.id, language]));
  assert.deepEqual(languages.get('modeller-rml').extensions, ['.modeller']);
  assert.deepEqual(languages.get('modeller-saf').extensions, ['.saf']);
});

test('ships grammars and deterministic language configuration', () => {
  for (const grammar of manifest.contributes.grammars) assert.ok(fs.existsSync(path.join(root, grammar.path)));
  const configuration = JSON.parse(fs.readFileSync(path.join(root, 'language-configuration.json'), 'utf8'));
  assert.equal(configuration.comments.lineComment, '#');
  assert.match(configuration.indentationRules.increaseIndentPattern, /lifecycle/);
});
