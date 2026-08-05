# Modeller Studio

Local definition-design tool for Modeller — workspace explorer, readable-source editor with live
LSP diagnostics, and diagram projections. Run with `npm run dev` (see `package.json`); the server
(`server.ts`) binds to `localhost` only, spawns `Modeller.LanguageServer`/`Modeller.Cli` locally,
and reads/writes the workspace directly off disk (`MODELLER_STUDIO_WORKSPACE`, default
`samples/child-care` — see `src/server/workspace.ts`).

## Playground mode

Set at build/deploy time, not toggled at runtime — a playground deployment has no local .NET
toolchain or filesystem to fall back to, so a build is either local Studio or the playground,
never both (see `docs/architecture/decisions/hosted-workspace-api.mdx` and issue #72):

- `NEXT_PUBLIC_MODELLER_STUDIO_MODE=playground` — renders `PlaygroundWorkbench` instead of the
  local `WorkbenchShell`, and `server.ts` skips starting/bridging to a language-server child
  process entirely.
- `NEXT_PUBLIC_MODELLER_API_URL` — base URL of the hosted `Modeller.Api` the playground calls
  (e.g. `https://modeller-next.vercel.app`).

The playground opens directly into the `samples/ordering` example (bundled at
`src/lib/playground/example-ordering.ts`, not read off disk), keeps its draft in
`sessionStorage` only, and re-analyzes the whole document set through
`POST /v1/workspace/analyze` on a 500ms debounce after each edit — there is no per-keystroke
diagnostics endpoint to bridge to, unlike local Studio's LSP connection.

The local-mode filesystem/CLI-subprocess API routes (`/api/document`, `/api/workspace`,
`/api/projection*`) return `404` in playground mode (`src/server/playground-guard.ts`) — the
playground UI never calls them, but a public deployment can't rely on that alone.
