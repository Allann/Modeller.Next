---
title: "Architecture"
---

# Architecture

> ⚠️ **Legacy Documentation** - This describes the existing plugin-based C# implementation. For the future architecture, see [Future Specification](architecture/draft/README.md).

Modeller uses a **plugin-based code generation architecture** where both definitions and templates are loaded dynamically at runtime.

## System Components

```
┌─────────────────────────────────────────────────────────────────────┐
│                           Modeller.Tool                             │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌────────────┐  │
│  │ BuildCommand│  │ ListCommand │  │ValidateCmd  │  │SettingsCmd │  │
│  └──────┬──────┘  └─────────────┘  └─────────────┘  └────────────┘  │
│         │                                                           │
│         ▼                                                           │
│  ┌─────────────────────────────────────────────────────────────┐    │
│  │                         Builder                             │    │
│  │  ┌─────────┐  ┌──────────────┐  ┌────────────────────────┐  │    │
│  │  │ Context │  │ CodeGenerator│  │    OutputStrategy      │  │    │
│  │  └────┬────┘  └──────┬───────┘  └───────────┬────────────┘  │    │
│  └───────┼──────────────┼──────────────────────┼───────────────┘    │
└──────────┼──────────────┼──────────────────────┼────────────────────┘
           │              │                      │
           ▼              ▼                      ▼
┌──────────────────┐ ┌──────────────────┐ ┌──────────────────┐
│DefinitionLoader  │ │ GeneratorLoader  │ │   FileWriter     │
│                  │ │                  │ │                  │
│ Loads definition │ │ Loads template   │ │ Writes output    │
│ DLLs             │ │ DLLs             │ │ to disk          │
└────────┬─────────┘ └────────┬─────────┘ └──────────────────┘
         │                    │
         ▼                    ▼
┌──────────────────┐ ┌──────────────────┐
│   Definitions/   │ │    Templates/    │
│   *.dll          │ │    *.dll         │
└──────────────────┘ └──────────────────┘
```

## Core Interfaces

### IGenerator
The fundamental interface for all code generators:

```csharp
public interface IGenerator
{
    IOutput Create();
}
```

### IMetadata
Provides metadata about a template for discovery:

```csharp
public interface IMetadata
{
    FileVersion Version { get; }
    string Name { get; }
    string Description { get; }
    Type EntryPoint { get; }
    IEnumerable<Type> ChildItems { get; }
}
```

### IOutput
Marker interface for all output types:

```csharp
public interface IOutput
{
    string Name { get; }
}
```

## Output Hierarchy

```
IOutput
├── File              # Single generated file
├── FileGroup         # Collection of files (folder)
├── Project           # .NET project with file groups
└── Solution          # Solution containing projects
    └── ISolution extends IFileGroup
```

## Generation Pipeline

1. **Command Parsing**: CLI parses arguments and creates settings
2. **Context Initialization**: Loads and validates definition and template
3. **Definition Loading**: `DefinitionLoader` loads the definition DLL
4. **Template Loading**: `GeneratorLoader` finds and loads the template
5. **Code Generation**: `CodeGenerator` invokes the template's `Create()` method
6. **Output Processing**: `OutputStrategy` routes output to appropriate `IFileCreator`
7. **File Writing**: `FileWriter` persists generated files to disk

## Plugin Loading

Both definitions and templates are loaded using `McMaster.NETCore.Plugins`:

```csharp
public sealed class AssemblyLoader
{
    public static Assembly Load(string filePath)
    {
        using var loader = PluginLoader.CreateFromAssemblyFile(
            filePath, 
            sharedTypes: new[] { typeof(Settings), typeof(ServiceData) });
        return loader.LoadDefaultAssembly();
    }
}
```

## Settings

The `Settings` record carries configuration through the pipeline:

```csharp
public sealed record Settings
{
    public required string TemplateFolder { get; init; }
    public required string DefinitionFolder { get; init; }
    public required string OutputFolder { get; init; }
    public required string Target { get; init; }
    public string? TemplateName { get; init; }
    public string? DefinitionName { get; init; }
    public bool SupportRegen { get; init; }
    
    // Runtime context (not serialized)
    public Enterprise? Enterprise { get; set; }
    public Service? DomainService { get; set; }
    public Entity? Entity { get; set; }
}
```

