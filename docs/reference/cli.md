---
title: "CLI Reference"
---

# CLI Reference

> ⚠️ **Legacy Documentation** - This describes the existing CLI tool. The future implementation may have a different command structure.

The Modeller CLI (`model`) provides commands for code generation, conversion, and management.

## Installation

The tool is built from `src/Modeller.Tool`:

```bash
dotnet build src/Modeller.Tool
```

## Commands

### build

Generate code from a definition using a template.

```bash
model build <template> <definition> [options]
```

**Arguments:**
- `<template>` - Name of the template to use
- `<definition>` - Name of the definition to use

**Options:**
| Option | Description | Default |
|--------|-------------|---------|
| `--templates` | Folder where templates are located | `../src/Modeller.Templates` |
| `--definitions` | Folder where definitions are located | `../src/Modeller.Definitions` |
| `--output` | Folder where output is generated | Current directory |
| `--regen` | Allow existing files to be overwritten | `true` |
| `--target` | Target framework version | `net8.0` |
| `--version` | Specific version of generator | `1.0.0` |

**Example:**
```bash
model build ApiSolution CaseDomain \
  --definitions ../src/Modeller.Definitions \
  --templates ../src/Modeller.Templates \
  --output ./generated \
  --target net8.0
```

---

### list definitions

List available definitions.

```bash
model list definitions [options]
```

**Options:**
| Option | Description |
|--------|-------------|
| `--folder` | Folder containing definitions |

**Example:**
```bash
model list definitions --folder ../src/Modeller.Definitions
```

---

### list templates

List available templates.

```bash
model list templates [options]
```

**Options:**
| Option | Description | Default |
|--------|-------------|---------|
| `--folder` | Folder containing templates | |
| `--target` | Target framework | `net8.0` |

**Example:**
```bash
model list templates --folder ../src/Modeller.Templates --target net9.0
```

---

### validate

Validate a definition without generating code.

```bash
model validate <definition> [options]
```

**Options:**
| Option | Description |
|--------|-------------|
| `--definitions` | Folder containing definitions |

**Example:**
```bash
model validate CaseDomain --definitions ../src/Modeller.Definitions
```

---

### definition

Write a definition to YAML/JSON files.

```bash
model definition <definition> [options]
```

**Options:**
| Option | Description |
|--------|-------------|
| `--definitions` | Folder containing definitions |
| `--output` | Output folder for definition files |

**Example:**
```bash
model definition NewBranch --definitions ../src/Modeller.Definitions
```

---

### convert

Convert an existing folder structure to a template project.

```bash
model convert <path> <namespace> [options]
```

**Arguments:**
- `<path>` - Source folder to convert
- `<namespace>` - Root namespace for the template

**Options:**
| Option | Description |
|--------|-------------|
| `--output` | Output folder for the template |

**Example:**
```bash
model convert ./MyProject MyProject --output ./templates/MyProjectTemplate
```

---

### dbconvert

Convert a database schema to a definition.

```bash
model dbconvert <connection-string> [options]
```

**Options:**
| Option | Description |
|--------|-------------|
| `--output` | Output folder for the definition |

**Example:**
```bash
model dbconvert "Server=.;Database=MyDb;Trusted_Connection=True;" --output ./definitions
```

---

### settings

Manage tool settings.

```bash
model settings [options]
```

**Options:**
| Option | Description |
|--------|-------------|
| `--name` | Settings file name |
| `--type` | Persistence type (Json, Yaml, Xml) |

---

## Logging

All commands support logging options:

| Option | Description |
|--------|-------------|
| `-v`, `--verbosity` | Log verbosity (Quiet, Normal, Verbose) |

Log files are written to `Modeller.log` in the tool directory.

## Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Success |
| 1 | Error occurred |

