import { createServer } from 'node:http';
import next from 'next';
import { WebSocketServer } from 'ws';
import { bridgeLanguageServer } from './src/server/ws-bridge';

const dev = process.env.NODE_ENV !== 'production';
const port = Number(process.env.PORT ?? 3100);
// The playground build never spawns a language-server child process or
// bridges to it over /lsp — Modeller.Api (issue #71) is its only backend, and
// a public deployment has no local .NET toolchain to spawn against anyway.
// See docs/architecture/decisions/hosted-workspace-api.mdx and issue #72.
const isPlayground = process.env.NEXT_PUBLIC_MODELLER_STUDIO_MODE === 'playground';
// Playground deployments (e.g. Vercel) must accept connections from the
// platform's own proxy, not just loopback; local Studio keeps binding to
// localhost only, since it has raw filesystem access for the workspace it's
// pointed at and is never meant to be reachable off-box.
const hostname = isPlayground ? '0.0.0.0' : 'localhost';

const app = next({ dev, hostname, port });
const handle = app.getRequestHandler();

app.prepare().then(() => {
  const handleUpgrade = app.getUpgradeHandler();
  const server = createServer((req, res) => {
    handle(req, res);
  });

  const wss = isPlayground ? undefined : new WebSocketServer({ noServer: true });

  server.on('upgrade', (req, socket, head) => {
    const url = new URL(req.url ?? '/', `http://${hostname}`);
    if (!wss || url.pathname !== '/lsp') {
      // Next's own dev-mode HMR WebSocket (and anything else Next upgrades)
      // needs to go through Next's handler, not be destroyed.
      handleUpgrade(req, socket, head);
      return;
    }
    wss.handleUpgrade(req, socket, head, (ws) => {
      bridgeLanguageServer(ws);
    });
  });

  server.listen(port, hostname, () => {
    console.log(`> Modeller Studio ready on http://${hostname}:${port} (${isPlayground ? 'playground' : 'local'} mode)`);
  });
});
