---
title: Quick start
description: Take a Modeller definition from an empty folder to repeatable generation.
---

# Quick start

This guide is for people using Modeller in their own project. Commands that
build Modeller's source repository are intentionally kept out of this path.

## 1. Create and open a project folder

```powershell
mkdir Acme.Bookings
cd Acme.Bookings
git init
code .
```

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download) and VS Code.

## 2. Install Modeller

Install the released CLI as a .NET global tool:

```powershell
dotnet tool install --global Modeller.Cli
modeller --help
```

If it is already installed, run `dotnet tool update --global Modeller.Cli`.
See [Install Modeller](/docs/getting-started/install-modeller) for local-tool and
prerelease package-source options.

## 3. Initialize and write the definition

```powershell
modeller init
mkdir model
```

Create `model/context.modeller`:

```text
rml 1.0

context Acme Bookings
  version 1.0.0
end

entity Booking
  field Booking date
    type date
  end
end
```

Add `model/context.modeller` to the `sources` array in
`.modeller/config.json`. The complete syntax is in the
[RML schema and language reference](/docs/reference/readable-modelling-language).

## 4. Add editor support and validate

Install the **Modeller RML** VS Code extension, then reopen the folder. It
provides `.modeller` syntax highlighting and live language-server diagnostics.
See [Set up VS Code](/docs/getting-started/vscode) for Marketplace, VSIX, and
language-server setup.

Validate at the command line as well:

```powershell
modeller validate model/context.modeller
```

Success is reported as `Valid: no diagnostics.` and returns exit code `0`.
Use `--format json` in scripts and CI. See
[Verify definitions](/docs/getting-started/verify-definition) for multi-file
models and exit codes.

## 5. Configure and preview generation

Generation needs more than the minimal file produced by `init`. Configure:

- `sources`: every `.modeller` file in the context;
- `templatePack`: a project-relative path to a pinned template pack;
- `identityRegistry`: the tooling-owned identity registry supplied with the
  initialized starter or template pack;
- `parameters.projectName` and the language-specific parameter block;
- `logicalOutputRoot` and `ownershipManifest`.

The current prerelease does not download a starter template pack or create the
identity registry during `modeller init`. Until starter distribution is
available, copy those assets from a supported starter such as the
[Child Care reference project](/docs/reference/reference-project), then adapt
its configuration and definitions. Do not invent or routinely edit identity
values by hand.

Preview before writing anything:

```powershell
modeller generate --workspace . --dry-run
modeller generate --workspace .
```

The first command shows creates, changes, conflicts, and removals without
writing files. The second applies the same safe, ownership-tracked plan. See
[Run the initial generation](/docs/getting-started/initial-generation).

## 6. Keep generated output current

After manual generation succeeds, add the incremental MSBuild target from
[Automatic generation](/docs/getting-started/automatic-generation). A build
then runs `modeller generate --workspace ...` only when a `.modeller` input,
configuration file, or template changes.

The finished lifecycle is:

```text
edit RML -> editor diagnostics -> CLI validation -> dry-run -> generation -> build
```
