---
title: Modeller CLI reference
description: Commands, output formats, and exit codes for the implemented CLI.
---

# Modeller CLI reference

The `modeller` CLI exposes the same parsing, planning, rendering, and safe-output
contracts used by other integrations. It is implemented with
`System.CommandLine`; usage errors and cancellation are handled consistently.

## Run from source

```powershell
dotnet run --project src/Modeller.Cli -- --help
```

When installed as a .NET tool, replace the prefix with `modeller`.

## Commands

### `init`

Create a minimal versioned JSON configuration.

```text
modeller init [--path <path>] [--force]
```

| Option | Default | Meaning |
| --- | --- | --- |
| `--path` | `.modeller/config.json` | Workspace-relative configuration path |
| `--force` | `false` | Replace an existing configuration |

The generated configuration declares version `1.0`, generation contract `1.0`,
logical output root `generated`, and profile `default`.

### `validate`

Compile and validate one RML or SAF document. RML is the normal user input; SAF
is retained for engineering and conformance workflows.

```text
modeller validate <source> [--format human|json]
```

`source` must be workspace-relative and cannot traverse above the workspace.
Human output is concise. JSON output has `outputVersion`, `valid`, and ordered
diagnostics with code, message, document, line, column, and length.

```powershell
modeller validate path/to/a-complete-context.modeller
modeller validate path/to/a-complete-context.modeller --format json
```

### `plan`

Create a deterministic generation plan without rendering or writing files.

```text
modeller plan <request> [--format human|json]
```

The JSON request is a serialized `GenerationPlanningRequest`: it contains the
resolved semantic snapshot, validated configuration, validated template-pack
descriptor, and previous generation state. Machine output includes the plan and
stable diagnostics.

### `generate`

Plan, render, and preview or apply generated output.

```text
modeller generate --workspace <path> [--dry-run] [--format human|json]
modeller generate <request> [--dry-run] [--format human|json]
```

The normal user workflow reads only inputs declared by the workspace's
`.modeller/config.json`: RML sources, one pinned local template-pack descriptor,
pack parameters, the logical output root, and the ownership manifest. Template content is
verified against its declared SHA-256 digest before planning. The parsed
canonical package is matched against the pack's reusable output recipes; the
workspace does not enumerate definition-specific output files.

The request form is the lower-level automation interface. Its JSON contains a
`GenerationPlanningRequest`, template content keyed by template ID, and an
optional ownership manifest.

`--dry-run` selects preview mode and writes nothing. Without it, output is
applied through the safe output contract and the workspace ownership manifest
is updated. Both modes report each path as create, change, unchanged, conflict,
stale, or remove.

Always preview a new request before apply:

```powershell
modeller generate --workspace samples/child-care --dry-run
modeller generate --workspace samples/child-care
dotnet build samples/child-care/generated/ChildCare.slnx
```

## Output format

`--format` accepts only `human` or `json` and defaults to `human`. JSON payloads
carry `outputVersion: "1.0"` so automation can reject incompatible changes.
Diagnostics use stable codes; exception text and secrets are not exposed.

## Exit codes

| Code | Name | Meaning |
| --- | --- | --- |
| `0` | Success | The requested workflow completed |
| `2` | Validation failed | RML or SAF source contains diagnostics |
| `64` | Usage | Arguments, options, or paths are invalid |
| `66` | Input unavailable | A requested input does not exist or cannot be read |
| `78` | Configuration | Configuration, planning, rendering, or output application failed |
| `130` | Cancelled | Cancellation was requested |

## Related contracts

- [Readable Modelling Language](/docs/reference/readable-modelling-language)
- [Semantic Assembly Format](/docs/reference/readable-source-language)
- [Configuration](/docs/reference/configuration)
- [Generation plans](/docs/reference/generation-plans)
- [Template rendering](/docs/reference/template-rendering)
- [Output application](/docs/reference/output-application)
