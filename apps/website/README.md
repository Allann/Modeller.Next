# Modeller Website (playground)

The public playground, deployed at `modeller.website`. Sibling app to
`apps/studio` (the local desktop-style Studio) and the root `modeller-docs`
site (deployed at `modeller.wiki`) — each app deploys independently.

## Current shape

This first slice is a **static, zero-backend** flagship experience: two
build-time-generated example pages (`/examples/ordering`,
`/examples/child-care`). There is no request-time .NET execution and no
editing yet — that lands with the anonymous browser-backed Studio mode
(epic #68, issue #72) and the hosted analysis API (#71).

## Build-time data

`src/data/generated/<slug>.json` holds the Lifecycle and Rule/Decision
projection graphs plus the RML source for each example, and is **committed**
— Vercel builds this app with the .NET SDK unavailable and (correctly)
without access to files outside `apps/website`, so `next build` alone must
be enough to produce a working deployment.

That data is never handwritten. Whenever a source sample under
`samples/<slug>` changes, regenerate it locally (needs `dotnet` on PATH and
the full repo checkout) and commit the result:

```powershell
npm run generate
git add src/data/generated
```

`scripts/generate-projections.mjs` does the regeneration: it shells out to
the real Modeller CLI (`dotnet run --project src/Modeller.Cli`) against each
workspace declared in `src/data/examples.json`.

## Adding an example

1. Add or grow a workspace under `samples/<slug>` (see `samples/ordering`
   and `samples/child-care` for the expected `.modeller/` + `model/` shape).
2. Add an entry to `src/data/examples.json` with the workspace path and
   narrative copy.
3. Run `npm run generate`, then `npm run dev` to preview, and commit the
   regenerated `src/data/generated/<slug>.json`.
