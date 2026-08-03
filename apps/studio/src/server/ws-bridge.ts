import type { WebSocket } from 'ws';
import { spawnLanguageServer } from './lsp-process';

// Forwards raw bytes both ways between a browser WebSocket and a spawned
// Modeller.LanguageServer process's stdio. The LSP wire format is already
// self-framed (Content-Length headers), so the bridge never parses messages —
// reassembly is the client library's job, not this proxy's.
export function bridgeLanguageServer(socket: WebSocket): void {
  const child = spawnLanguageServer();

  socket.on('message', (data) => {
    child.stdin.write(data as Buffer);
  });
  child.stdout.on('data', (chunk: Buffer) => {
    if (socket.readyState === socket.OPEN) socket.send(chunk);
  });
  child.stderr.on('data', (chunk: Buffer) => {
    console.error(`[Modeller.LanguageServer] ${chunk.toString('utf-8')}`);
  });

  socket.on('close', () => {
    child.kill();
  });
  child.on('exit', () => {
    if (socket.readyState === socket.OPEN) socket.close();
  });
}
