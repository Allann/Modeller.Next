---
title: Run the initial generation
description: Configure a template pack, preview output, and apply it safely.
---

# Run the initial generation

Before generating, `.modeller/config.json` must declare all sources, a pinned
local template pack, the identity registry, output root, parameters, and
ownership manifest. The [Child Care reference project](/docs/reference/reference-project)
is the canonical working configuration to adapt.

Always begin with a preview:

```powershell
modeller generate --workspace . --dry-run
```

Review the output paths and statuses. A preview writes nothing. Resolve any
diagnostics or conflicts, then apply the plan:

```powershell
modeller generate --workspace .
```

Generated output is recorded in the configured ownership manifest. Commit the
configuration, RML sources, identity registry, pinned templates, and ownership
manifest so other developers and CI reproduce the same result.

Run generation again without changing inputs. Deterministic output should be
reported as `Unchanged`. Build or test the generated project before enabling
automatic generation.
