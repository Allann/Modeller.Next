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

The RML file is the smallest executable slice of the
[Child Care reference project](/docs/reference/reference-project):

```powershell
dotnet run --project src/Modeller.Cli -- generate --workspace samples/child-care --dry-run
```

A successful workspace preview reports its deterministic changes with no
diagnostics and writes nothing. For stable machine-readable output, add
`--format json`.

The source uses the business-facing language documented in the
[RML reference](/docs/reference/readable-modelling-language). Parsing
produces the canonical model; semantic validation and downstream workflows do
not maintain a second interpretation of the domain.

Open the repository in VS Code and run the extension from
`editors/vscode-modeller` to receive syntax highlighting, diagnostics, semantic
completion, hover, definition, references, and safe rename. The extension starts
the repository language-server project automatically.

## Plan and generate output

The Child Care workspace declares its RML sources, the reusable pinned C# Domain
Project pack, pack parameters, output root, and ownership manifest in
`.modeller/config.json`. The pack expands over the complete context; the
workspace does not list Booking or ACCS-specific output files. Preview the
exact proposed changes before applying them:

```powershell
dotnet run --project src/Modeller.Cli -- generate --workspace samples/child-care --dry-run
dotnet run --project src/Modeller.Cli -- generate --workspace samples/child-care
dotnet build samples/child-care/generated/ChildCare.slnx
```

The second generation is deterministic and reports every artifact as
`Unchanged`. Apply mode writes only through the
[safe output-application contract](/docs/reference/output-application), which
uses manifest-proven ownership and reports conflicts with handwritten files.
The lower-level request-based `plan` and `generate` forms remain available for
automation and contract testing.

## Next steps

- Learn all command arguments and exit codes in the
  [CLI reference](/docs/reference/modeller-cli).
- Follow the implemented pipeline through
  [Architecture 101](/docs/architecture/architecture-101).
- Explore parsing, validation, rules, projections, generation, and editor APIs
  from the [reference index](/docs/reference).
