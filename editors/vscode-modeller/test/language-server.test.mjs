import assert from 'node:assert/strict';
import path from 'node:path';
import { spawn } from 'node:child_process';
import test from 'node:test';

const repository = path.resolve(import.meta.dirname, '..', '..', '..');

test('language server advertises the RML semantic capabilities', { timeout: 15000 }, async () => {
  const child = spawn('dotnet', ['run', '--project', path.join(repository, 'src', 'Modeller.LanguageServer'), '-c', 'Release', '--no-build'], { stdio: ['pipe', 'pipe', 'pipe'] });
  try {
    const response = responseFor(child, 1);
    const payload = JSON.stringify({ jsonrpc: '2.0', id: 1, method: 'initialize', params: { capabilities: {} } });
    child.stdin.write(`Content-Length: ${Buffer.byteLength(payload)}\r\n\r\n${payload}`);
    const message = await response;
    const capabilities = message.result.capabilities;
    assert.equal(capabilities.textDocumentSync, 2);
    assert.equal(capabilities.hoverProvider, true);
    assert.equal(capabilities.definitionProvider, true);
    assert.equal(capabilities.referencesProvider, true);
    assert.equal(capabilities.renameProvider.prepareProvider, false);
    assert.deepEqual(capabilities.semanticTokensProvider.legend.tokenTypes, ['keyword', 'comment']);
  } finally {
    child.kill();
  }
});

function responseFor(child, id) {
  return new Promise((resolve, reject) => {
    let buffer = Buffer.alloc(0);
    child.stderr.on('data', data => reject(new Error(data.toString())));
    child.on('exit', code => code === 0 || code === null ? undefined : reject(new Error(`server exited ${code}`)));
    child.stdout.on('data', data => {
      buffer = Buffer.concat([buffer, data]);
      while (true) {
        const boundary = buffer.indexOf('\r\n\r\n');
        if (boundary < 0) return;
        const header = buffer.subarray(0, boundary).toString();
        const length = Number(/Content-Length:\s*(\d+)/i.exec(header)?.[1]);
        if (buffer.length < boundary + 4 + length) return;
        const message = JSON.parse(buffer.subarray(boundary + 4, boundary + 4 + length).toString());
        buffer = buffer.subarray(boundary + 4 + length);
        if (message.id === id) { resolve(message); return; }
      }
    });
  });
}
