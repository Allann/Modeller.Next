import { createServer } from 'node:http';
import next from 'next';
import { WebSocketServer } from 'ws';
import { bridgeLanguageServer } from './src/server/ws-bridge';

const dev = process.env.NODE_ENV !== 'production';
const port = Number(process.env.PORT ?? 3100);
const hostname = 'localhost';

const app = next({ dev, hostname, port });
const handle = app.getRequestHandler();

app.prepare().then(() => {
  const handleUpgrade = app.getUpgradeHandler();
  const server = createServer((req, res) => {
    handle(req, res);
  });

  const wss = new WebSocketServer({ noServer: true });

  server.on('upgrade', (req, socket, head) => {
    const url = new URL(req.url ?? '/', `http://${hostname}`);
    if (url.pathname !== '/lsp') {
      // Next's own dev-mode HMR WebSocket (and anything else Next upgrades)
      // needs to go through Next's handler, not be destroyed.
      handleUpgrade(req, socket, head);
      return;
    }
    wss.handleUpgrade(req, socket, head, (ws) => {
      bridgeLanguageServer(ws);
    });
  });

  // Bind to localhost only — this server has raw filesystem access for the
  // workspace it's pointed at, and is never meant to be reachable off-box.
  server.listen(port, hostname, () => {
    console.log(`> Modeller Studio ready on http://${hostname}:${port}`);
  });
});
