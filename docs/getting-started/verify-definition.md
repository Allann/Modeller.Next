---
title: Verify definitions
description: Validate RML from VS Code, the CLI, and CI.
---

# Verify definitions

Validate each complete RML input from the workspace root:

```powershell
modeller validate model/context.modeller
```

The command checks syntax and semantic rules. A valid file prints
`Valid: no diagnostics.` Invalid input prints stable diagnostic codes and
source locations.

For CI and other automation, use versioned JSON output:

```powershell
modeller validate model/context.modeller --format json
```

The payload contains `outputVersion`, `valid`, and ordered diagnostics. Exit
code `0` means valid; `2` means validation failed. Usage, missing-input, and
configuration failures use distinct exit codes documented in the
[CLI reference](/docs/reference/modeller-cli#exit-codes).

`validate` currently accepts one source document per invocation. A workspace
generation preview compiles all sources declared in `.modeller/config.json`
together, so use it as the integration check for cross-file references:

```powershell
modeller generate --workspace . --dry-run
```

Run both checks in CI until a workspace-level `validate` command is available.
