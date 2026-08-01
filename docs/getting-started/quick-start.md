---
title: "Quick Start Guide"
---

# Quick Start Guide

Get up and running with Modeller in minutes. This guide walks you through generating C# code from domain definitions.

## Prerequisites

- .NET 10.0 SDK or later
- A template pack (e.g., `csharp/clean-architecture`)

## Installation

### Option 1: Install as Global Tool (Recommended)

Install the Modeller CLI as a global .NET tool:

```bash
# From the repository root
dotnet pack src/Modeller.Cli/Modeller.Cli.csproj -c Release
dotnet tool install --global --add-source src/Modeller.Cli/bin/Release Modeller.Cli
```

Now `modeller` is available from any directory:

```bash
modeller --help
```

To update later:

```bash
dotnet tool update --global --add-source src/Modeller.Cli/bin/Release Modeller.Cli
```

To uninstall:

```bash
dotnet tool uninstall --global Modeller.Cli
```

### Option 2: Run from Source

Run directly using `dotnet run`:

```bash
dotnet run --project src/Modeller.Cli/Modeller.Cli.csproj -- --help
```

## Step 1: Initialize Your Project

Navigate to your project folder and initialize Modeller:

```bash
cd my-project

modeller init \
  --template-source file://C:/templates \
  --pack csharp/clean-architecture \
  --domain ./domain
```

This creates a `.modeller/` folder with:
- `config.yaml` - Main configuration
- `profiles/default.yaml` - Generation profile
- `templates/` - Copied template files

## Step 2: Create Domain Definitions

Create domain definition files in your domain folder:

**`domain/entities/customer.entity`**
```
entity Customer
  "A customer in the system"
  
  Name: text(100) "Customer name"
  Email: text(255) "Email address"
  Active: boolean, default(true) "Is customer active"
  CreatedAt: datetime "When created"
end

key Customer
  CustomerId: guid, generated
  index [Email] unique
  index [Active]
end
```

**`domain/enums/customer-status.enum`**
```
enum CustomerStatus
  "Customer account status"
  
  Pending = 0 "Awaiting verification"
  Active = 1 "Active customer"
  Suspended = 2 "Account suspended"
  Closed = 3 "Account closed"
end
```

**`domain/behaviours/create-customer.command`**
```
command CreateCustomer
  "Creates a new customer"
  
  Name: text(100), required
  Email: text(255), required
end
```

## Step 3: Configure Your Project

Edit `.modeller/config.yaml`:

```yaml
version: 1

domain: ./domain
template_source: file://C:/templates

variables:
  company: Acme
  product: CustomerManagement
  copyright: "© 2024 Acme Corp"
  root_namespace: Acme.CustomerManagement

output:
  root: ./src
  project_pattern: "{variables.company}.{variables.product}.{layer}"

default_profile: default

files:
  generated_suffix: ".g"
  line_ending: auto
  encoding: utf-8
```

## Step 4: Preview Generation

Use `--dry-run` to preview what will be generated:

```bash
modeller generate --dry-run
```

Output shows the files that would be created:
```
Generating with profile: Default
  Pack: csharp/clean-architecture
  Domain: ./domain
  Output: ./src

[DRY RUN - No files will be written]

  [Domain]
    Files to generate: 3
    [?] Acme.CustomerManagement.Domain/Entities/Customer.g.cs
    [?] Acme.CustomerManagement.Domain/Enums/CustomerStatus.g.cs
    [?] Acme.CustomerManagement.Domain/Commands/CreateCustomer.g.cs
```

## Step 5: Generate Code

Run without `--dry-run` to generate files:

```bash
modeller generate
```

Generated files have `.g.` in their name (e.g., `Customer.g.cs`) and are **safe to overwrite** on regeneration. User-modified files without `.g.` are never touched.

## Step 6: Customize and Extend

The generated code uses partial classes, so you can extend without modifying generated files:

**`Customer.g.cs`** (generated - don't edit):
```csharp
public sealed partial record Customer
{
    public Guid CustomerId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public bool Active { get; init; } = true;
}
```

**`Customer.cs`** (your file - never overwritten):
```csharp
public sealed partial record Customer
{
    public string DisplayName => $"{Name} ({Email})";
}
```

## Common Commands

| Command | Description |
|---------|-------------|
| `modeller init` | Initialize a new project |
| `modeller generate` | Generate code |
| `modeller generate --dry-run` | Preview generation |
| `modeller generate --layer Domain` | Generate specific layer |
| `modeller validate` | Validate configuration and domain |
| `modeller templates list` | List available template packs |
| `modeller snippet list` | List available snippets |

## Next Steps

- See [CLI Reference](cli-reference.md) for all command options
- See [Deep Dive](deep-dive-instruction.md) for architecture details
- See [Definitions](definitions.md) for DSL syntax reference

