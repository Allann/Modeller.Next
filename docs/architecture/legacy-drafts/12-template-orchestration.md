---
title: "Template Orchestration System"
---

# Template Orchestration System

## Overview

This document describes the template orchestration system that enables users to generate code from domain definitions. The system provides a flexible, layered approach where users control what gets generated - from full solutions down to individual snippets.

## Design Principles

1. **External Templates** - Templates are not embedded in the tool. They live in a configurable server/folder location and are copied to the project's `.modeller` folder on initialization.

2. **User Control** - Users decide what to generate via profiles, command-line options, or interactive prompts.

3. **Composable Snippets** - Reusable template fragments (headers, properties, methods) that can be included in any template.

4. **Safe by Default** - Only files with `.g.*` suffix (e.g., `Unit.g.cs`) are overwritten. User-modified files are never touched.

5. **CLI-First with VS Code Support** - Primary interface is command-line, with VS Code extension planned.

## Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                         User Commands                                │
│  modeller init | modeller generate | modeller templates             │
└─────────────────────────────────────────────────────────────────────┘
                                   │
                                   ▼
┌─────────────────────────────────────────────────────────────────────┐
│                      Orchestration Layer                             │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐                  │
│  │   Config    │  │   Profile   │  │  Template   │                  │
│  │   Parser    │  │   Resolver  │  │  Discovery  │                  │
│  └─────────────┘  └─────────────┘  └─────────────┘                  │
└─────────────────────────────────────────────────────────────────────┘
                                   │
                                   ▼
┌─────────────────────────────────────────────────────────────────────┐
│                       Generation Engine                              │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐                  │
│  │   Domain    │  │  Template   │  │    File     │                  │
│  │   Parser    │  │   Engine    │  │   Writer    │                  │
│  └─────────────┘  └─────────────┘  └─────────────┘                  │
└─────────────────────────────────────────────────────────────────────┘
                                   │
                                   ▼
┌─────────────────────────────────────────────────────────────────────┐
│                         Output Files                                 │
│  *.g.cs (overwritable)  |  *.cs (user-owned, never overwritten)     │
└─────────────────────────────────────────────────────────────────────┘
```

## Folder Structure

### Template Server Location

Templates are stored in a configurable server location (file share, git repo, or local folder):

```
{template-server}/
  _snippets/                          # Shared reusable snippets
    csharp/
      header.scriban                  # File header with namespace
      property.scriban                # Single property generation
      properties.scriban              # Multiple properties
      constructor.scriban             # Constructor patterns
      factory-methods.scriban         # New() and CreateValid()
      validation-attributes.scriban   # [Required], [MaxLength], etc.
      audit-fields.scriban            # CreatedAt, UpdatedAt, etc.
      xml-doc.scriban                 # XML documentation comments

  csharp/
    clean-architecture/               # Template pack
      pack.yaml                       # Pack metadata
      infrastructure/
        template.yaml                 # Generation manifest
        entity.scriban
        configuration.scriban
        db-context.scriban
      sdk/
        template.yaml
        request.scriban
        response.scriban
        enum.scriban
      api/
        template.yaml
        endpoint.scriban
        validator.scriban
```

### Project `.modeller` Folder

Created by `modeller init`, contains project-specific configuration:

```
{project-root}/
  .modeller/
    config.yaml                       # Main configuration
    profiles/
      default.yaml                    # Default generation profile
    templates/                        # Copied from server on init
      _snippets/
        csharp/
          ...
      csharp/
        clean-architecture/
          ...
    variables/
      local.yaml                      # Local overrides (gitignored)
```

## Configuration Files

### Pack Manifest (`pack.yaml`)

Describes a template pack - a collection of related templates:

```yaml
name: clean-architecture
version: 1.0.0
description: Templates for Clean Architecture pattern with EF Core
author: Modeller Team
language: csharp

templates:
  - infrastructure
  - sdk
  - api

variables:
  # Default values that can be overridden
  use_nullable: true
  use_file_scoped_namespaces: true
  ef_core_version: "8.0"
```

### Template Manifest (`template.yaml`)

Describes what a single template generates:

```yaml
name: Infrastructure Layer
description: EF Core entities, configurations, and DbContext

generates:
  # Per-entity files (runs once for each entity in domain)
  - template: entity.scriban
    per: entity
    output: "Entities/{entity.name | pascal_case}.g.cs"
    description: Entity record with factory methods

  - template: configuration.scriban
    per: entity
    output: "Configurations/{entity.name | pascal_case}Configuration.g.cs"
    description: EF Core entity configuration

  # Per-enum files
  - template: enum.scriban
    per: enum
    output: "Enums/{enum.name | pascal_case}.g.cs"

  # Per-domain files (runs once for entire domain)
  - template: db-context.scriban
    per: domain
    output: "{variables.product}DbContext.g.cs"
    description: DbContext with all entity DbSets

snippets:
  - csharp/header
  - csharp/property
  - csharp/factory-methods
  - csharp/validation-attributes
```

### Project Configuration (`.modeller/config.yaml`)

Main configuration for the project:

```yaml
# Schema version
version: 1

# Where domain definitions live
domain: ./samples/child-care

# Where templates were copied from (for updates)
template_source: file://C:/templates
# template_source: https://github.com/org/modeller-templates

# Global variables available to all templates
variables:
  company: Acme
  product: ChildCare
  copyright: "© 2024 Acme"
  root_namespace: Acme.ChildCare

# Output configuration
output:
  root: ./src
  # Pattern for project folders: {company}.{product}.{layer}
  project_pattern: "{variables.company}.{variables.product}.{layer | pascal_case}"

# Default profile to use
default_profile: default

# File handling
files:
  # Only .g.* files are overwritten, others are skipped
  generated_suffix: ".g"
  line_ending: lf          # lf | crlf | auto
  encoding: utf-8
```

### Profile Configuration (`.modeller/profiles/default.yaml`)

Defines what gets generated:

```yaml
name: Full Stack
description: Generates Infrastructure, SDK, and API layers

# Which template pack to use
pack: csharp/clean-architecture

# Layers to generate
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

# What to include (default: all)
include:
  entities: all              # or list: [Unit, SyncJob]
  enums: all
  commands: all
  queries: all
  projections: all

# What to exclude
exclude:
  entities: []

# Layer-specific variable overrides
layer_variables:
  Infrastructure:
    use_audit_fields: true
  Sdk:
    include_validation: true
```


## Snippet System

Snippets are reusable Scriban template fragments that can be included in any template. They act as "functions" for common code patterns.

### Snippet Location

Snippets live in `_snippets/{language}/` and are referenced by path:

```
_snippets/
  csharp/
    header.scriban              # File header
    property.scriban            # Single property
    properties.scriban          # Loop over properties
    constructor.scriban         # Constructor generation
    factory-methods.scriban     # New() and CreateValid()
    validation-attributes.scriban
    audit-fields.scriban
    xml-doc.scriban
```

### Snippet Definition

Each snippet is a Scriban template that accepts parameters:

**`_snippets/csharp/header.scriban`**:
```liquid
{{~##
  Generates a C# file header with optional copyright and usings.
  Parameters:
    - namespace: string (required) - The namespace for the file
    - usings: string[] (optional) - Using statements to include
    - copyright: string (optional) - Copyright notice
##~}}
{{~ if copyright ~}}
// {{ copyright }}
// Auto-generated - do not modify directly
{{~ end ~}}

{{~ for using in usings ~}}
using {{ using }};
{{~ end ~}}
{{~ if usings && usings.size > 0 ~}}

{{~ end ~}}
namespace {{ namespace }};
```

**`_snippets/csharp/property.scriban`**:
```liquid
{{~##
  Generates a single C# property with optional XML docs and validation.
  Parameters:
    - attr: object - Attribute definition with name, type, required, description, etc.
    - indent: string (optional) - Indentation prefix, default "    "
##~}}
{{~ indent = indent ?? "    " ~}}
{{~ if attr.description ~}}
{{ indent }}/// <summary>{{ attr.description }}</summary>
{{~ end ~}}
{{~ if attr.required ~}}
{{ indent }}[Required]
{{~ end ~}}
{{~ if attr.max_length ~}}
{{ indent }}[MaxLength({{ attr.max_length }})]
{{~ end ~}}
{{ indent }}public {{ attr | to_csharp_type }} {{ attr.name | pascal_case }} { get; init; }
```

### Using Snippets in Templates

Templates include snippets using Scriban's `include` statement:

**`infrastructure/entity.scriban`**:
```liquid
{{~ include '_snippets/csharp/header'
      namespace: namespace
      copyright: variables.copyright
      usings: ['System', 'System.ComponentModel.DataAnnotations'] ~}}

/// <summary>{{ entity.description }}</summary>
public sealed record {{ entity.name | pascal_case }}
{
    internal {{ entity.name | pascal_case }}() { }

{{~ for attr in entity.attributes ~}}
{{ include '_snippets/csharp/property' attr: attr }}

{{~ end ~}}
}

{{ include '_snippets/csharp/factory-methods' entity: entity namespace: namespace }}
```

## File Naming Convention

### Generated Files (`.g.*`)

Files with `.g.` in their name are **always overwritten** on regeneration:

```
Entities/Unit.g.cs           ✓ Overwritten
Entities/UnitConfiguration.g.cs  ✓ Overwritten
```

### User Files (no `.g.`)

Files without `.g.` are **never overwritten** - they belong to the user:

```
Entities/Unit.cs             ✗ Never touched (user owns this)
Entities/UnitExtensions.cs   ✗ Never touched
```

### Partial Class Pattern

This enables the partial class pattern where generated and user code coexist:

```csharp
// Unit.g.cs - Generated, always overwritten
public sealed partial record Unit
{
    public Guid Id { get; init; }
    public string TruckNumber { get; init; } = string.Empty;
    // ... generated properties
}

// Unit.cs - User file, never touched
public sealed partial record Unit
{
    // User's custom methods, computed properties, etc.
    public string DisplayName => $"{TruckNumber} - {Description}";
}
```


## CLI Commands

### `modeller init`

Initializes a project with `.modeller` folder and copies templates:

```bash
# Interactive - prompts for template source and options
modeller init

# Specify template source
modeller init --source=file://C:/templates
modeller init --source=https://github.com/org/modeller-templates

# Specify domain location
modeller init --domain=./samples/child-care
```

Creates:
- `.modeller/config.yaml` with defaults
- `.modeller/profiles/default.yaml`
- `.modeller/templates/` copied from source
- `.modeller/variables/local.yaml` (gitignored)

### `modeller generate`

Generates code based on configuration:

```bash
# Generate using default profile
modeller generate

# Generate specific profile
modeller generate --profile=infrastructure

# Generate specific layer
modeller generate --layer=sdk

# Generate specific entity
modeller generate --entity=Unit

# Combine filters
modeller generate --entity=Unit --layer=infrastructure

# Preview mode - show what would be generated without writing
modeller generate --dry-run

# Force regeneration even if files exist
modeller generate --force
```

### `modeller templates`

Manage templates:

```bash
# List available template packs
modeller templates list

# Show details about a specific template
modeller templates info clean-architecture/infrastructure

# Update templates from source
modeller templates update

# Validate templates
modeller templates validate
```

### `modeller validate`

Validate domain definitions:

```bash
# Validate domain in configured location
modeller validate

# Validate specific location
modeller validate ./samples/child-care
```

### `modeller snippet`

Output a rendered snippet to stdout (useful for testing):

```bash
# Render a snippet with parameters
modeller snippet csharp/header --namespace=Acme.Units

# Render property snippet
modeller snippet csharp/property --name=TruckNumber --type=string --required
```

## Generation Flow

```
┌─────────────────────────────────────────────────────────────────────┐
│  1. Load Configuration                                               │
│     - Read .modeller/config.yaml                                     │
│     - Load profile (default or specified)                           │
│     - Merge variables (config → profile → layer → CLI args)        │
└─────────────────────────────────────────────────────────────────────┘
                                   │
                                   ▼
┌─────────────────────────────────────────────────────────────────────┐
│  2. Parse Domain                                                     │
│     - Load all .entity, .enum, .command, .query, .projection files │
│     - Build domain model                                             │
│     - Validate references                                            │
└─────────────────────────────────────────────────────────────────────┘
                                   │
                                   ▼
┌─────────────────────────────────────────────────────────────────────┐
│  3. Resolve Templates                                                │
│     - Load pack.yaml for selected pack                              │
│     - Load template.yaml for each layer                             │
│     - Resolve snippet dependencies                                   │
└─────────────────────────────────────────────────────────────────────┘
                                   │
                                   ▼
┌─────────────────────────────────────────────────────────────────────┐
│  4. Plan Generation                                                  │
│     - For each layer in profile                                      │
│       - For each generates entry in template.yaml                   │
│         - Determine iterations (per: entity/enum/domain)            │
│         - Calculate output paths                                     │
│         - Check if file is .g.* (can overwrite) or not              │
└─────────────────────────────────────────────────────────────────────┘
                                   │
                                   ▼
┌─────────────────────────────────────────────────────────────────────┐
│  5. Execute Generation                                               │
│     - If --dry-run: display plan and exit                           │
│     - For each planned file:                                         │
│       - Skip if not .g.* and file exists                            │
│       - Render template with Scriban                                 │
│       - Write to output path                                         │
└─────────────────────────────────────────────────────────────────────┘
                                   │
                                   ▼
┌─────────────────────────────────────────────────────────────────────┐
│  6. Report Results                                                   │
│     - Files generated                                                │
│     - Files skipped (user-owned)                                     │
│     - Errors encountered                                             │
└─────────────────────────────────────────────────────────────────────┘
```


## Implementation Components

### Core Classes

| Class | Responsibility |
|-------|----------------|
| `PackManifest` | Model for `pack.yaml` - pack metadata and template list |
| `TemplateManifest` | Model for `template.yaml` - what a template generates |
| `GenerationEntry` | Single file generation: template, per, output pattern |
| `ProjectConfig` | Model for `.modeller/config.yaml` |
| `ProfileConfig` | Model for `.modeller/profiles/*.yaml` |
| `TemplateDiscovery` | Scans template folder, loads manifests |
| `SnippetLoader` | Loads snippets from `_snippets/`, makes available to Scriban |
| `ConfigLoader` | Loads and merges configuration from `.modeller/` |
| `GenerationPlanner` | Plans what files to generate based on config + domain |
| `GenerationExecutor` | Executes the plan - renders templates, writes files |
| `FileWriter` | Handles `.g.*` logic, encoding, line endings |

### TemplateEngine Enhancements

The existing `TemplateEngine` needs to support:

1. **Include paths** - Resolve `include '_snippets/csharp/header'`
2. **Custom functions** - `to_csharp_type`, `pascal_case`, etc.
3. **Template context** - Variables from config, profile, layer

```csharp
public class TemplateEngine
{
    private readonly string _templatesRoot;
    private readonly SnippetLoader _snippetLoader;

    public TemplateEngine(string templatesRoot)
    {
        _templatesRoot = templatesRoot;
        _snippetLoader = new SnippetLoader(Path.Combine(templatesRoot, "_snippets"));
    }

    public string Render(string templatePath, TemplateContext context)
    {
        var template = LoadTemplate(templatePath);
        var scribanContext = BuildContext(context);

        // Register snippet include handler
        scribanContext.TemplateLoader = _snippetLoader;

        return template.Render(scribanContext);
    }
}
```

### Directory Structure (Source)

```
src/
  Modeller.Generator/
    Configuration/
      PackManifest.cs
      TemplateManifest.cs
      GenerationEntry.cs
      ProjectConfig.cs
      ProfileConfig.cs
      ConfigLoader.cs
    Templates/
      TemplateEngine.cs          # Enhanced
      SnippetLoader.cs
      TemplateDiscovery.cs
    Generation/
      GenerationPlanner.cs
      GenerationPlan.cs
      PlannedFile.cs
      GenerationExecutor.cs
      FileWriter.cs
    Commands/                    # CLI handlers
      InitCommand.cs
      GenerateCommand.cs
      TemplatesCommand.cs
      ValidateCommand.cs
      SnippetCommand.cs
```

## Implementation Phases

### Phase 1: Foundation ✅
- [x] Define `PackManifest` and `TemplateManifest` models
- [x] Create YAML parsing for manifests
- [x] Create `SnippetLoader` for loading snippets
- [x] Enhance `TemplateEngine` with include support
- [x] Create `TemplateDiscovery` to scan template folder

### Phase 2: Configuration ✅
- [x] Define `ProjectConfig` and `ProfileConfig` models
- [x] Create `ConfigLoader` to read `.modeller/` folder
- [x] Implement variable merging (config → profile → layer → CLI)
- [x] Create sample configuration files

### Phase 3: CLI Commands ✅
- [x] Create CLI project structure with System.CommandLine
- [x] Implement `modeller init` command
- [x] Implement `modeller generate` command
- [x] Implement `modeller templates list/info` commands
- [x] Implement `modeller generate --dry-run`

### Phase 4: Generation ✅
- [x] Create `GenerationPlanner` - builds list of files to generate
- [x] Create `GenerationExecutor` - renders and writes files
- [x] Implement `.g.*` file handling logic
- [x] Add progress reporting and error handling

### Phase 5: Polish ✅
- [x] Comprehensive error messages
- [x] `modeller validate` command
- [x] `modeller snippet` command
- [x] Documentation and examples

## VS Code Extension (Future)

The VS Code extension will provide:

### Features
- **Domain Explorer** - Tree view showing entities, enums, commands, queries
- **Generate Context Menu** - Right-click → Generate → Select layer/profile
- **Preview Pane** - See generated code before writing
- **IntelliSense** - Autocomplete for `.entity`, `.command`, etc. files
- **Validation** - Diagnostics for invalid domain definitions
- **Snippet Palette** - Quick insert of common snippets

### Architecture
```
vscode-modeller/
  src/
    extension.ts              # Entry point
    domainExplorer.ts         # Tree view provider
    generateCommand.ts        # Generate command handler
    previewProvider.ts        # WebView for previews
    languageSupport/
      entityLanguage.ts       # .entity file support
      commandLanguage.ts      # .command file support
      ...
```

The extension will shell out to the `modeller` CLI for actual generation, ensuring consistency between CLI and VS Code usage.
