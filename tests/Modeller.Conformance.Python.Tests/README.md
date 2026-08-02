# Modeller.Conformance.Python.Tests

Conformance suite for the Python (`scriban`/`python`) renderer capability. It exercises the Child Care Python API
pack through the same public pipeline the CLI uses — `TemplatePackLoader` → `RendererCapabilityRegistry` →
`GenerationPlanner` → `TemplateRenderer` → `GenerationExecution` — via `GeneratedSourceTreeHarness`, against an
in-memory output filesystem. No files are written to disk except by the toolchain tests below, which use a
temp directory that is always cleaned up.

`GeneratedSourceTreeHarness` is parameterized by pack directory and renderer capability, so a future language
renderer's conformance suite can reuse it directly instead of re-deriving the pipeline wiring.

## Python toolchain tests

`PythonToolchainTests` verifies the generated package with an actual Python interpreter:

- `Generated_python_api_package_compiles` runs `python -m compileall` over the generated tree.
- `Generated_python_api_package_openapi_schema_builds` imports the generated FastAPI app and dumps its OpenAPI
  schema.

Both tests **skip** (they never silently pass, and never fail the build) when the required tooling isn't
available:

- No interpreter found under `py`, `python3` or `python` on `PATH` → both tests skip.
- `fastapi`/`pydantic` not importable in the resolved interpreter → only the OpenAPI test skips.

To run these locally, install a Python 3.13+ interpreter and:

```sh
pip install -r tests/Modeller.Conformance.Python.Tests/requirements-conformance.txt
```

This step is never invoked by the tests themselves — it's an explicit, offline-cacheable prerequisite so CI can
choose whether to provide it.
