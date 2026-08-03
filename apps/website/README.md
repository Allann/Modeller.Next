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

`scripts/generate-projections.mjs` runs before `dev`/`build` and shells out
to the real Modeller CLI (`dotnet run --project src/Modeller.Cli`) against
each workspace declared in `src/data/examples.json`, writing the Lifecycle
and Rule/Decision projection graphs plus the RML source into
`src/data/generated/<slug>.json`. Projection data is always Modeller-produced,
never handwritten — regenerate it any time the source samples change by
re-running `npm run build` or `npm run dev`.

## Adding an example

1. Add or grow a workspace under `samples/<slug>` (see `samples/ordering`
   and `samples/child-care` for the expected `.modeller/` + `model/` shape).
2. Add an entry to `src/data/examples.json` with the workspace path and
   narrative copy.
3. Run `npm run dev` — the new example is generated and routed automatically.
