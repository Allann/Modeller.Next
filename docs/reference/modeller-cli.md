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

Compile and validate one readable-source document.

```text
modeller validate <source> [--format human|json]
```

`source` must be workspace-relative and cannot traverse above the workspace.
Human output is concise. JSON output has `outputVersion`, `valid`, and ordered
diagnostics with code, message, document, line, column, and length.

```powershell
modeller validate tests/Modeller.Parsing.Tests/Fixtures/child-care-accs.modeller
modeller validate tests/Modeller.Parsing.Tests/Fixtures/child-care-accs.modeller --format json
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
modeller generate <request> [--dry-run] [--format human|json]
```

The workflow request contains a `GenerationPlanningRequest`, template content
keyed by template ID, and an optional ownership manifest. `--dry-run` selects
preview mode; without it, output is applied atomically through the safe output
contract. Both modes report each path as create, change, unchanged, conflict,
stale, or remove.

Always preview a new request before apply:

```powershell
modeller generate child-care-generation.json --dry-run
modeller generate child-care-generation.json
```

## Output format

`--format` accepts only `human` or `json` and defaults to `human`. JSON payloads
carry `outputVersion: "1.0"` so automation can reject incompatible changes.
Diagnostics use stable codes; exception text and secrets are not exposed.

## Exit codes

| Code | Name | Meaning |
| --- | --- | --- |
| `0` | Success | The requested workflow completed |
| `2` | Validation failed | Readable source contains diagnostics |
| `64` | Usage | Arguments, options, or paths are invalid |
| `66` | Input unavailable | A requested input does not exist or cannot be read |
| `78` | Configuration | Configuration, planning, rendering, or output application failed |
| `130` | Cancelled | Cancellation was requested |

## Related contracts

- [Readable source](/docs/reference/readable-source-language)
- [Configuration](/docs/reference/configuration)
- [Generation plans](/docs/reference/generation-plans)
- [Template rendering](/docs/reference/template-rendering)
- [Output application](/docs/reference/output-application)
