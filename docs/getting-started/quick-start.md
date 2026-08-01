---
title: Quick start
description: Build Modeller and validate the Child Care reference model.
---

# Quick start

## Prerequisites

- .NET 10 SDK or later
- Git

## Build and test

From the repository root:

```powershell
dotnet build Modeller.Next.slnx
dotnet test Modeller.Next.slnx
```

Run the CLI from source while developing:

```powershell
dotnet run --project src/Modeller.Cli -- --help
```

## Create a workspace configuration

```powershell
dotnet run --project src/Modeller.Cli -- init
```

This creates `.modeller/config.json`, a minimal versioned configuration with a
generation contract, logical output root, and default profile. Use `--force`
only when you intend to replace an existing file.

## Validate the Child Care model

The SAF fixture is the smallest executable slice of the
[Child Care reference project](/docs/reference/reference-project):

```powershell
dotnet run --project src/Modeller.Cli -- validate samples/child-care/model/accs-eligibility.modeller
```

A successful model reports `Valid: no diagnostics.` For stable machine-readable
diagnostics, add `--format json`.

The source uses the low-level format documented in the
[Semantic Assembly Format reference](/docs/reference/readable-source-language). Parsing
produces the canonical model; semantic validation and downstream workflows do
not maintain a second interpretation of the domain.

## Plan and generate output

Generation consumes an explicit JSON request containing the resolved semantic
snapshot, configuration, template-pack descriptor, and previous generation
state. This makes planning reproducible and keeps filesystem access outside the
planner.

```powershell
dotnet run --project src/Modeller.Cli -- plan path/to/plan-request.json
dotnet run --project src/Modeller.Cli -- generate path/to/workflow-request.json --dry-run
dotnet run --project src/Modeller.Cli -- generate path/to/workflow-request.json
```

Start with `--dry-run`. Apply mode writes only through the
[safe output-application contract](/docs/reference/output-application), which
uses manifest-proven ownership and reports conflicts with handwritten files.

## Next steps

- Learn all command arguments and exit codes in the
  [CLI reference](/docs/reference/modeller-cli).
- Follow the implemented pipeline through
  [Architecture 101](/docs/architecture/architecture-101).
- Explore parsing, validation, rules, projections, generation, and editor APIs
  from the [reference index](/docs/reference).
