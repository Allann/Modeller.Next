# Python api-project pack

This reusable pack projects a complete canonical context into a compiling
FastAPI service. It is the Python counterpart to
`../../csharp/api-project/`: same scopes, same shape of output, idiomatic to
Python instead of C#.

- **context** (once): `pyproject.toml` and `main.py`. `main.py` never sees
  the entity/rule/behaviour list — like the C# pack's `Program.cs`, it
  discovers routers by scanning the `apis` subpackage at import time
  (`pkgutil.iter_modules`) and including every module that exposes a
  `router` attribute, rather than hand-listing routes.
- **entity** (per Entity): a Pydantic `BaseModel` with a synthetic `id`,
  an in-memory repository class exposing a module-level singleton, a
  `Create`/`Update`/`Delete` command module, a `Get`/`List` query module, and
  a FastAPI `APIRouter` exposing CRUD over `/api/{entity}`.
- **enumeration** (per Enumeration): an `IntEnum`.
- **rule** (per Rule): a facts `BaseModel` and an evaluator function, plus a
  router evaluating the rule over posted facts at `/api/rules/{rule}`.
- **behaviour** (per Behaviour): a lifecycle-stage `IntEnum` and a transition
  function, plus a router applying the transition at
  `/api/behaviours/{behaviour}`.

## Layout and naming

Output uses a `src/` layout: `{projectName}/pyproject.toml` at the package
root, with importable modules under `{projectName}/src/{projectName}/`. Set
`parameters.projectName` in the workspace config to a valid Python
identifier (e.g. `child_care`) — it names both the repository root and the
importable package, since template-pack output paths only expand
`{projectName}` and `{definitionName}` tokens. `parameters.namespace` is used
as the pip distribution name in `pyproject.toml` (free-form, e.g.
`child-care-api`) and `parameters.targetFramework` is read as the minimum
Python version (e.g. `3.13`).

Every `{definitionName}` path segment and generated identifier is
`snake_case`, computed by `PythonTemplateNaming` in
`Modeller.Rendering` — the Python analogue of `CSharpTemplateNaming`.
Cross-file references (an entity field typed as an enumeration, a behaviour
calling its guarding rule) are resolved as explicit relative imports rather
than a blanket global-using, since Python packages don't auto-aggregate
symbols the way a C# namespace does; each entity's `imports` list and each
rule/behaviour's `module_name`/`function_name` are precomputed by
`PythonTemplateGlobalsProvider` for exactly this reason.

## Selecting this pack

Set `"language": "python"` in `pack.json` — `Modeller.Cli`'s
`WorkspaceGeneration` reads it to choose `PythonTemplateGlobalsProvider` over
`CSharpTemplateGlobalsProvider` and to snake_case `{definitionName}` path
segments. Packs that omit `language` default to `csharp` for backward
compatibility.
