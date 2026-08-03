// Minimal LSP transport over the server's /lsp WebSocket bridge (server.ts,
// src/server/ws-bridge.ts forward raw bytes verbatim from
// Modeller.LanguageServer's stdio). The wire format is standard LSP framing:
// `Content-Length: <n>\r\n\r\n<n bytes of UTF-8 JSON>`. Frame boundaries don't
// align with WebSocket message boundaries, so this accumulates bytes itself.
export type JsonRpcId = number;

interface PendingRequest {
  resolve: (value: unknown) => void;
  reject: (reason: unknown) => void;
}

export class LspConnection {
  private socket: WebSocket;
  private buffer = '';
  private nextId = 1;
  private pending = new Map<JsonRpcId, PendingRequest>();
  private notificationHandlers = new Map<string, ((params: unknown) => void)[]>();
  private ready: Promise<void>;

  constructor(url: string) {
    this.socket = new WebSocket(url);
    // Browsers default binaryType to 'blob' for binary frames; this class
    // assumes ArrayBuffer (see onMessage), so it must be set explicitly.
    this.socket.binaryType = 'arraybuffer';
    this.ready = new Promise((resolve, reject) => {
      this.socket.addEventListener('open', () => resolve(), { once: true });
      this.socket.addEventListener('error', () => reject(new Error('LSP WebSocket failed to connect')), { once: true });
    });
    this.socket.addEventListener('message', (event) => this.onMessage(event));
  }

  async whenReady(): Promise<void> {
    await this.ready;
  }

  onNotification(method: string, handler: (params: unknown) => void): void {
    const handlers = this.notificationHandlers.get(method) ?? [];
    handlers.push(handler);
    this.notificationHandlers.set(method, handlers);
  }

  async request<T>(method: string, params: unknown): Promise<T> {
    const id = this.nextId++;
    const promise = new Promise<T>((resolve, reject) => {
      this.pending.set(id, { resolve: resolve as (value: unknown) => void, reject });
    });
    this.send({ jsonrpc: '2.0', id, method, params });
    return promise;
  }

  notify(method: string, params: unknown): void {
    this.send({ jsonrpc: '2.0', method, params });
  }

  close(): void {
    this.socket.close();
  }

  private send(message: object): void {
    const body = JSON.stringify(message);
    const framed = `Content-Length: ${new TextEncoder().encode(body).length}\r\n\r\n${body}`;
    if (this.socket.readyState === this.socket.OPEN) this.socket.send(framed);
  }

  private onMessage(event: MessageEvent): void {
    const chunk = typeof event.data === 'string' ? event.data : new TextDecoder().decode(event.data as ArrayBuffer);
    this.buffer += chunk;
    this.drainBuffer();
  }

  // FLAG: string-based buffering with per-message encode/decode round-trips to
  // count UTF-8 bytes against Content-Length is correct but not efficient —
  // fine for readable-source-sized documents; revisit with binary WebSocket
  // frames (ArrayBuffer accumulation) if this becomes a bottleneck.
  private drainBuffer(): void {
    for (;;) {
      const headerEnd = this.buffer.indexOf('\r\n\r\n');
      if (headerEnd === -1) return;
      const header = this.buffer.slice(0, headerEnd);
      const match = /Content-Length: (\d+)/i.exec(header);
      if (!match) {
        this.buffer = this.buffer.slice(headerEnd + 4);
        continue;
      }
      const length = Number(match[1]);
      const bodyStart = headerEnd + 4;
      const bodyBytes = new TextEncoder().encode(this.buffer.slice(bodyStart));
      if (bodyBytes.length < length) return;

      const bodyText = new TextDecoder().decode(bodyBytes.slice(0, length));
      const consumedChars = bodyStart + new TextDecoder().decode(bodyBytes.slice(0, length)).length;
      this.buffer = this.buffer.slice(consumedChars);
      this.dispatch(JSON.parse(bodyText));
    }
  }

  private dispatch(message: { id?: JsonRpcId; method?: string; params?: unknown; result?: unknown; error?: unknown }): void {
    if (message.id !== undefined && message.method === undefined) {
      const pending = this.pending.get(message.id);
      if (!pending) return;
      this.pending.delete(message.id);
      if (message.error) pending.reject(message.error);
      else pending.resolve(message.result);
      return;
    }
    if (message.method) {
      for (const handler of this.notificationHandlers.get(message.method) ?? []) handler(message.params);
    }
  }
}
