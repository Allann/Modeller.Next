# Modeller Studio

Local definition-design tool for Modeller — workspace explorer, readable-source editor with live
LSP diagnostics, and diagram projections. Run in a browser with `npm run dev`, or run the local
Windows app with `npm run electron:dev` (see `package.json`). The local app command builds its
bundled server before it starts Electron. The server
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

## Publishing a diagram

Local Studio can publish a static diagram snapshot. The snapshot is calculated by the local CLI;
the hosted Studio app only stores it and shows it. Configure the hosted Studio Vercel project with
the existing Upstash Redis connection and a long random `MODELLER_PUBLISH_TOKEN`. Configure local Studio with
`NEXT_PUBLIC_MODELLER_PUBLISH_URL` when the default `https://modeller.website/studio/api/publish`
is not the target deployment. The Publish button asks for the token once and stores it in the
local browser profile. Published diagrams are public at `/studio/published/<slug>`.

### Deploy setup

Do this in the **Studio Vercel project**, not the root `modeller` project. Studio and the public
website are separate Vercel projects.

1. In Vercel, open the Studio project. Add the existing Upstash Redis connection variables:
   `UPSTASH_REDIS_REST_URL` and `UPSTASH_REDIS_REST_TOKEN`. The token must allow writes.
   You can instead use `KV_REST_API_URL` and `KV_REST_API_TOKEN` if those are the names on the
   existing Vercel integration.
2. Make a publish token on your computer. In PowerShell, run:

   ```powershell
   $bytes = [byte[]]::new(32)
   [Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
   [Convert]::ToHexString($bytes)
   ```

3. In **Settings → Environment Variables**, add the output as `MODELLER_PUBLISH_TOKEN`.
   Add it to Production. Add the same value to Preview too if you test publishing from preview
   deployments.
4. Deploy the Studio project. Then deploy the website project. The website rewrite sends
   `/studio/published/<slug>` and `/studio/api/publish` to Studio.

Use the same generated token in the local Studio Publish dialog. Do not add either secret to a
repository file or a `NEXT_PUBLIC_` variable.

The CLI can set the token after you explicitly link `apps/studio` to the correct Studio project:

```powershell
cd apps/studio
vercel link
$token = '<the generated value>'
$token | vercel env add MODELLER_PUBLISH_TOKEN production
```

Use `vercel env add MODELLER_PUBLISH_TOKEN preview` if required. Do not run `vercel link` until
the CLI shows the intended team and Studio project.
