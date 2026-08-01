---
title: "API Reference"
---

# API Reference

> ⚠️ **Legacy Documentation** - This describes the existing C# API. The future implementation will have a different API structure.

Core interfaces and classes in the Modeller library.

## Interfaces

### IGenerator

The fundamental interface for all code generators.

```csharp
namespace Modeller.Interfaces;

public interface IGenerator
{
    IOutput Create();
}
```

---

### IMetadata

Provides metadata about a template for discovery and documentation.

```csharp
namespace Modeller.Interfaces;

public interface IMetadata
{
    FileVersion Version { get; }
    string Name { get; }
    string Description { get; }
    Type EntryPoint { get; }
    IEnumerable<Type> ChildItems { get; }
}
```

---

### IDefinitionMetaData

Provides metadata about a definition.

```csharp
namespace Modeller.Interfaces;

public interface IDefinitionMetaData
{
    FileVersion Version { get; }
    string Name { get; }
    string Description { get; }
    Type EntryPoint { get; }
}
```

---

### IOutput

Marker interface for all output types.

```csharp
namespace Modeller.Interfaces;

public interface IOutput
{
    string Name { get; }
}
```

---

### IFile

Represents a generated file.

```csharp
namespace Modeller.Interfaces;

public interface IFile : IOutput
{
    string Content { get; }
    string Path { get; }
    string FullName { get; }
    bool CanOverwrite { get; }
}
```

---

### IFileGroup

Represents a collection of files (folder).

```csharp
namespace Modeller.Interfaces;

public interface IFileGroup : IOutput
{
    IEnumerable<IFile> Files { get; }
    IEnumerable<IFileGroup> FileGroups { get; }
    void SetPath(string path);
    void AddFile(IOutput output);
    void AddFileGroup(IOutput output);
}
```

---

### IProject

Represents a .NET project.

```csharp
namespace Modeller.Interfaces;

public interface IProject : IOutput
{
    Guid Id { get; }
    string Path { get; set; }
    string Filename { get; }
    IEnumerable<IFileGroup> FileGroups { get; }
    IFileGroup AddFileGroup(IFileGroup? output);
}
```

---

### ISnippet

Represents a reusable code snippet.

```csharp
namespace Modeller.Interfaces;

public interface ISnippet : IOutput
{
    string Content { get; }
}
```

---

## Classes

### Settings

Configuration record passed to generators.

```csharp
namespace Modeller;

public sealed record Settings
{
    public required string TemplateFolder { get; init; }
    public required string DefinitionFolder { get; init; }
    public required string OutputFolder { get; init; }
    public required string Target { get; init; }
    public string? TemplateName { get; init; }
    public string? DefinitionName { get; init; }
    public bool SupportRegen { get; init; } = true;
    public string Version { get; init; } = "1.0.0";
    
    // Runtime context (not serialized)
    public Enterprise? Enterprise { get; set; }
    public Service? DomainService { get; set; }
    public Entity? Entity { get; set; }
    public IList<Package> Packages { get; init; } = [];
}
```

---

### File

A generated file record.

```csharp
namespace Modeller.Generator;

public record File(
    string Name, 
    string Content, 
    string Path = "", 
    bool CanOverwrite = false
) : IFile
{
    public string FullName => Outputs.Path.Combine(Path, Name);
}
```

---

### MetadataBase

Base class for template metadata.

```csharp
namespace Modeller.Generator;

public abstract class MetadataBase : IMetadata
{
    protected MetadataBase();
    protected MetadataBase(string vers);
    
    public FileVersion Version { get; }
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract Type EntryPoint { get; }
    public abstract IEnumerable<Type> ChildItems { get; }
}
```

---

### DefinitionBase

Base class for definitions.

```csharp
namespace Modeller.Fluent;

public abstract class DefinitionBase : IDefinition
{
    protected abstract EnterpriseBuilder Define();
    public EnterpriseData Create();
}
```

---

## Models

### Enterprise

```csharp
public readonly record struct Enterprise(string Company, Name Project, string Summary)
{
    public FileVersion? Version { get; init; }
    public required ImmutableList<Service> Services { get; init; }
    public string? Remarks { get; init; }
}
```

### Service

```csharp
public readonly record struct Service(Name Name, string Summary)
{
    public string? Remarks { get; init; }
    public required ImmutableList<Entity> Entities { get; init; }
    public ImmutableList<Enumeration> Enumerations { get; init; }
}
```

### Entity

```csharp
public readonly record struct Entity(Name Name, string Summary)
{
    public EntityKey? Key { get; init; }
    public required ImmutableList<Field> Fields { get; init; }
    public ImmutableList<Relationship> Relationships { get; init; }
    public bool IsRoot { get; init; }
    public bool ShouldAudit { get; init; }
    public CrudSupport SupportCrud { get; init; }
}
```

### Field

```csharp
public readonly record struct Field(Name Name, DataType DataType, string Summary)
{
    public bool Nullable { get; init; }
    public bool BusinessKey { get; init; }
    public string? DefaultValue { get; init; }
    public ValueGeneratedTypes Generated { get; init; }
}
```

