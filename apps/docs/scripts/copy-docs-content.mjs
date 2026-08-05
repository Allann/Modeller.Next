// Copies the repo-root docs/ folder (the shared knowledge base: coding
// standards, architecture, agent docs) into this app's own directory so
// fumadocs-mdx's dir option never has to reach outside apps/docs. Reaching
// outside the app's Root Directory via outputFileTracingRoot/turbopack.root
// instead of copying broke Vercel's deployment packaging (see the sibling
// fix to apps/studio's copy-grammars.mjs, which hit and reverted the same
// class of issue for editors/vscode-modeller/) — the build step compiled
// and generated pages successfully, but "Deploying outputs" failed.
import { cpSync, mkdirSync, rmSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const source = path.resolve(here, '..', '..', '..', 'docs');
const target = path.resolve(here, '..', 'docs');

rmSync(target, { recursive: true, force: true });
mkdirSync(target, { recursive: true });
cpSync(source, target, { recursive: true });

console.log(`Copied docs content into ${target}`);
