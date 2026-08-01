---
title: "Modeller CLI Reference"
---

# Modeller CLI Reference

Complete reference for the Modeller command-line interface.

> **Successor implementation status:** the executable successor provides
> `init`, `validate`, `plan`, and `generate`. The command tree,
> required arguments, options, validation, help, and invocation are implemented
> with System.CommandLine 2.0.10; command handlers remain thin adapters over the
> stable Modeller interfaces. Some remaining sections below retain legacy or
> intended workflow details and are not syntax authority.

Commands that produce reports accept `--format human|json`. Machine output uses the
versioned deterministic JSON contract and never contains ANSI presentation.

Current executable syntax:

```bash
modeller init [--path .modeller/config.json] [--force]
modeller validate <source> [--format human|json]
modeller plan <request> [--format human|json]
modeller generate <request> [--dry-run] [--format human|json]
```

`generate` consumes a versioned workflow request containing the resolved
snapshot, generation configuration, validated pack descriptor, pinned template
content, and optional ownership manifest. `--dry-run` performs no writes.

## Global Usage

```bash
modeller [command] [options]
```

## Commands

### init

Initialize a new `.modeller` configuration in the current directory.

```bash
modeller init [options]
```

**Options:**

| Option | Alias | Required | Description |
|--------|-------|----------|-------------|
| `--template-source` | `-t` | Yes | Source path/URL for templates (e.g., `file://C:/templates`) |
| `--pack` | `-p` | Yes | Template pack to use (e.g., `csharp/clean-architecture`) |
| `--domain` | `-d` | No | Path to domain definitions folder (default: `.`) |
| `--force` | `-f` | No | Overwrite existing configuration |

**Examples:**

```bash
# Initialize with local templates
modeller init -t file://C:/templates -p csharp/clean-architecture

# Initialize with specific domain path
modeller init -t file://C:/templates -p csharp/clean-architecture -d ./domain

# Force overwrite existing configuration
modeller init -t file://C:/templates -p csharp/clean-architecture -f
```

**Creates:**
- `.modeller/config.yaml` - Main configuration file
- `.modeller/profiles/default.yaml` - Default generation profile
- `.modeller/templates/` - Copy of templates from source

---

### generate

Generate code from domain definitions using templates.

```bash
modeller generate [options]
```

**Options:**

| Option | Alias | Required | Description |
|--------|-------|----------|-------------|
| `--profile` | `-p` | No | Profile to use (default: from `config.yaml`) |
| `--layer` | `-l` | No | Generate only a specific layer |
| `--var` | `-v` | No | Override variables (format: `key=value`). Can be repeated. |
| `--dry-run` | `-n` | No | Preview without writing files |

**Examples:**

```bash
# Generate using default profile
modeller generate

# Preview what would be generated
modeller generate --dry-run

# Generate specific layer only
modeller generate --layer Infrastructure

# Generate with variable overrides
modeller generate --var company=Acme --var product=Sales

# Use a different profile
modeller generate --profile infrastructure-only

# Combine options
modeller generate -p infrastructure-only -l Domain -n
```

**Output Symbols:**

| Symbol | Color | Meaning |
|--------|-------|---------|
| `+` | Green | File created |
| `~` | Yellow | File overwritten |
| `.` | Gray | File skipped (user-owned) |
| `X` | Red | Generation failed |
| `?` | Gray | Dry run preview |

---

### validate

Validate project configuration and domain definitions.

```bash
modeller validate
```

**Checks:**
- `.modeller/` folder exists
- `config.yaml` parses correctly
- Template source is accessible
- Domain folder exists and contains valid definitions
- All profiles are valid

**Output:**
- ✓ Green - Check passed
- ⚠ Yellow - Warning (non-fatal)
- ✗ Red - Error (fatal)

---

### templates list

List available template packs from a source.

```bash
modeller templates list [options]
```

**Options:**

| Option | Alias | Required | Description |
|--------|-------|----------|-------------|
| `--source` | `-s` | No | Template source path (default: project templates) |

**Example:**

```bash
# List packs from project templates
modeller templates list

# List packs from external source
modeller templates list --source file://C:/templates
```

---

### templates info

Show details about a specific template pack.

```bash
modeller templates info <pack> [options]
```

**Arguments:**

| Argument | Required | Description |
|----------|----------|-------------|
| `<pack>` | Yes | Pack name (e.g., `csharp/clean-architecture`) |

**Options:**

| Option | Alias | Required | Description |
|--------|-------|----------|-------------|
| `--source` | `-s` | No | Template source path (default: project templates) |

**Example:**

```bash
modeller templates info csharp/clean-architecture
```

---

### snippet list

List available template snippets.

```bash
modeller snippet list [options]
```

**Options:**

| Option | Alias | Required | Description |
|--------|-------|----------|-------------|
| `--source` | `-s` | No | Template source path (default: project templates) |

**Example:**

```bash
modeller snippet list
```

---

### snippet show

Display the content of a specific snippet.

```bash
modeller snippet show <name> [options]
```

**Arguments:**

| Argument | Required | Description |
|----------|----------|-------------|
| `<name>` | Yes | Snippet name (e.g., `header`, `property`) |

**Options:**

| Option | Alias | Required | Description |
|--------|-------|----------|-------------|
| `--source` | `-s` | No | Template source path (default: project templates) |

**Example:**

```bash
modeller snippet show header
modeller snippet show property --source file://C:/templates
```

---

## Configuration Files

### config.yaml

Main project configuration in `.modeller/config.yaml`:

```yaml
version: 1

domain: ./domain                    # Path to domain definitions
template_source: file://C:/templates  # Where templates came from

variables:                          # Global variables for templates
  company: Acme
  product: Sales
  copyright: "© 2024 Acme Corp"
  root_namespace: Acme.Sales

output:
  root: ./src                       # Output directory
  project_pattern: "{variables.company}.{variables.product}.{layer}"

default_profile: default            # Profile to use when none specified

files:
  generated_suffix: ".g"            # Suffix for regeneratable files
  line_ending: auto                 # lf | crlf | auto
  encoding: utf-8
```

### Profile Configuration

Profile files in `.modeller/profiles/*.yaml`:

```yaml
name: Full Stack
description: Generates all layers
pack: csharp/clean-architecture

layers:
  - name: Infrastructure
    template: infrastructure
    output: "{variables.company}.{variables.product}.Infrastructure"

  - name: Sdk
    template: sdk
    output: "{variables.company}.{variables.product}.Sdk"

  - name: Api
    template: api
    output: "{variables.company}.{variables.product}.Api"

include:
  entities: all          # or list: [Customer, Order]
  enums: all
  commands: all
  queries: all

exclude:
  entities: []

layer_variables:         # Per-layer variable overrides
  Infrastructure:
    use_audit_fields: true
```

---

## Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Success |
| 1 | Error occurred |

---

## Environment

The CLI operates in the current working directory. It looks for `.modeller/` folder to determine if a project is initialized.

## See Also

- [Quick Start Guide](quick-start.md)
- [Deep Dive](deep-dive-instruction.md)
- [Definitions DSL](definitions.md)
