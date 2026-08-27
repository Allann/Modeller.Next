import { copyFileSync, cpSync, existsSync, mkdirSync, rmSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const studioRoot = path.resolve(here, '..');
const distRoot = path.join(studioRoot, 'packaging', 'dist', 'windows');

for (const requiredPath of [
  '.next',
  'node_modules',
  'server-bin/Modeller.Cli.dll',
  'server-bin/Modeller.LanguageServer.dll',
]) {
  if (!existsSync(path.join(studioRoot, requiredPath))) {
    throw new Error(`Missing ${requiredPath}. Run npm run build and npm run server:build before packaging Studio.`);
  }
}
const vendorNode = path.join(studioRoot, 'packaging', 'vendor', 'node', 'node.exe');
const runtimeNode = existsSync(vendorNode) ? vendorNode : process.execPath;

rmSync(distRoot, { recursive: true, force: true });
mkdirSync(distRoot, { recursive: true });
copyFileSync(path.join(studioRoot, 'package.json'), path.join(distRoot, 'package.json'));
copyFileSync(path.join(studioRoot, 'package-lock.json'), path.join(distRoot, 'package-lock.json'));
copyFileSync(path.join(studioRoot, 'server.ts'), path.join(distRoot, 'server.ts'));
cpSync(path.join(studioRoot, '.next'), path.join(distRoot, '.next'), { recursive: true });
cpSync(path.join(studioRoot, 'node_modules'), path.join(distRoot, 'node_modules'), { recursive: true });
cpSync(path.join(studioRoot, 'public'), path.join(distRoot, 'public'), { recursive: true });
cpSync(path.join(studioRoot, 'server-bin'), path.join(distRoot, 'server-bin'), { recursive: true });
cpSync(path.join(studioRoot, 'src'), path.join(distRoot, 'src'), { recursive: true });
mkdirSync(path.join(distRoot, 'runtime'), { recursive: true });
copyFileSync(runtimeNode, path.join(distRoot, 'runtime', 'node.exe'));

writeFileSync(
  path.join(distRoot, 'ModellerStudio.cmd'),
  [
    '@echo off',
    'setlocal',
    'set "PORT=3100"',
    'set "NODE_ENV=production"',
    'pushd "%~dp0"',
    'runtime\\node.exe node_modules\\tsx\\dist\\cli.mjs server.ts %*',
    'popd',
  ].join('\r\n'),
);

console.log(`Prepared Windows Studio distribution in ${distRoot}`);
