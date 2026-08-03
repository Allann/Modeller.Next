// Build-time data step: shells out to the real Modeller CLI so every example
// page renders projection data Modeller actually produced, never handwritten
// JSON. Run before dev/build (see package.json predev/prebuild).
import { spawnSync } from 'node:child_process';
import { readFileSync, writeFileSync, mkdirSync, existsSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const appDir = path.resolve(scriptDir, '..');
const repoRoot = path.resolve(appDir, '..', '..');
const outDir = path.resolve(appDir, 'src', 'data', 'generated');
const examples = JSON.parse(readFileSync(path.resolve(appDir, 'src', 'data', 'examples.json'), 'utf8'));

const VIEWS = ['Lifecycle', 'RuleDecision'];

function runCli(args) {
  const result = spawnSync(
    'dotnet',
    ['run', '--project', 'src/Modeller.Cli', '--', ...args, '--format', 'json'],
    { cwd: repoRoot, encoding: 'utf8' },
  );
  if (result.status !== 0) {
    return { ok: false, error: (result.stdout || result.stderr || '').trim() };
  }
  try {
    return { ok: true, value: JSON.parse(result.stdout) };
  } catch {
    return { ok: false, error: `Could not parse CLI output: ${result.stdout}` };
  }
}

function projectView(workspace, view) {
  const roots = runCli(['project', '--workspace', workspace, '--view', view]);
  if (!roots.ok) return { supported: false, error: roots.error };
  const rootList = roots.value.roots ?? [];
  if (rootList.length === 0) return { supported: true, roots: [], graph: null };
  const graph = runCli(['project', '--workspace', workspace, '--view', view, '--root', rootList[0].id]);
  return {
    supported: true,
    roots: rootList,
    graph: graph.ok ? graph.value.graph : null,
  };
}

function readSource(workspace) {
  const configPath = path.resolve(repoRoot, workspace, '.modeller', 'config.json');
  const config = JSON.parse(readFileSync(configPath, 'utf8'));
  return config.sources.map((relativePath) => ({
    path: relativePath,
    content: readFileSync(path.resolve(repoRoot, workspace, relativePath), 'utf8'),
  }));
}

if (!existsSync(outDir)) mkdirSync(outDir, { recursive: true });

for (const example of examples) {
  console.log(`Generating projection data for ${example.slug}...`);
  const views = Object.fromEntries(VIEWS.map((view) => [view, projectView(example.workspace, view)]));
  const data = {
    slug: example.slug,
    generatedAt: new Date().toISOString(),
    source: readSource(example.workspace),
    views,
  };
  writeFileSync(path.resolve(outDir, `${example.slug}.json`), JSON.stringify(data, null, 2) + '\n');
}

console.log(`Wrote projection data for ${examples.length} example(s) to ${path.relative(repoRoot, outDir)}.`);
