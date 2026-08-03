import type { WebSocket } from 'ws';
import { spawnLanguageServer } from './lsp-process';

// Forwards raw bytes both ways between a browser WebSocket and a spawned
// Modeller.LanguageServer process's stdio. The LSP wire format is already
// self-framed (Content-Length headers), so the bridge never parses messages —
// reassembly is the client library's job, not this proxy's.
export function bridgeLanguageServer(socket: WebSocket): void {
  let child: ReturnType<typeof spawnLanguageServer>;
  try {
    child = spawnLanguageServer();
  } catch (error) {
    // resolveDotnetTool(requireBundledDll: true) throws when no bundled dll
    // is available — fail the connection loudly instead of falling back to
    // `dotnet run --project`, which would corrupt the LSP wire protocol (see
    // dotnet-tool.ts). Closing here (rather than leaving the socket open with
    // no server behind it) lets LspConnection's close handler reject any
    // pending requests immediately instead of waiting out the request timeout.
    console.error('[Modeller.LanguageServer]', error instanceof Error ? error.message : error);
    socket.close(1011, 'Modeller.LanguageServer is not available');
    return;
  }

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
