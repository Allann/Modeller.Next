---
title: "Templates"
---

# Templates

> ⚠️ **Legacy Documentation** - This describes the existing C# template implementation. For the future text-based templating approach using Scriban, see [Future Templates](architecture/draft/10-templates.md).

Templates are **code generators** that transform definitions into source code. Each template is a compiled C# DLL that implements the `IGenerator` interface.

## Template Structure

```
MyTemplate.v1.0/
├── MyTemplate.v1.0.csproj
├── Generator.cs              # Main entry point (IGenerator)
├── GeneratorDetails.cs       # Metadata (IMetadata)
├── GlobalUsings.cs
├── ProjectFile.cs            # Generates .csproj
├── EntityFile.cs             # Generates entity classes
└── SubFolder/
    └── FeatureGroup.cs       # Groups related files
```

## Creating a Template

### 1. Generator Entry Point

```csharp
public class Generator : IGenerator
{
    private readonly Settings _settings;
    private readonly Enterprise _enterprise;

    public Generator(Settings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(settings.Enterprise);

        _settings = settings;
        _enterprise = settings.Enterprise.Value;
    }

    public IOutput Create()
    {
        var project = (IProject)new ProjectFile(_settings).Create();
        
        project.FileGroups.First().AddFile(new ProgramFile(_settings).Create());
        project.AddFileGroup((IFileGroup)new EntitiesGroup(_settings).Create());

        return project;
    }
}
```

### 2. Generator Metadata

```csharp
public class GeneratorDetails : MetadataBase
{
    public static GeneratorDetails Instance { get; } = new();

    public GeneratorDetails() : base("1.0.0") { }

    public override string Name => "My Template";
    public override string Description => "Generates my project structure";
    public override Type EntryPoint => typeof(Generator);
    public override IEnumerable<Type> ChildItems => [typeof(Header.Generator)];
}
```

## Output Types

### File
A single generated file:

```csharp
var content = "public class MyClass { }";
return new File("MyClass.cs", content, Path: "src", CanOverwrite: true);
```

### FileGroup
A folder containing files:

```csharp
var group = new FileGroup("Entities");
group.AddFile(new EntityFile(settings).Create());
group.AddFile(new AnotherFile(settings).Create());
return group;
```

### Project
A .NET project with file groups:

```csharp
var project = new Project("MyProject", "MyProject.csproj");
project.AddFileGroup(new FileGroup("Models"));
project.AddFileGroup((IFileGroup)new ServicesGroup(settings).Create());
return project;
```

### Solution
A solution containing projects:

```csharp
var solution = new Solution();
solution.AddProject(new ApiProjectGenerator(settings).Create());
solution.AddProject(new DomainProjectGenerator(settings).Create());
solution.AddFile(new SolutionFile(settings).Create());
return solution;
```

## StringBuilder Extensions

Templates use helper extensions for building code:

```csharp
var sb = new StringBuilder();
sb.Al("namespace MyNamespace;");     // Append line
sb.B();                               // Blank line
sb.A("public ");                      // Append (no newline)
sb.I(1).Al("private int _id;");      // Indent + append line
sb.I(2, 4).Al("// comment");         // Indent 2 levels, 4 spaces each
sb.AppendIf("text", () => condition); // Conditional append
sb.TrimEnd(Environment.NewLine);      // Remove trailing newlines
```

## Composing Templates

Templates can use other templates as snippets:

```csharp
// Use Header template
sb.Al(((ISnippet)new Header.Generator(settings.SupportRegen, metadata).Create()).Content);

// Use Property template for each field
foreach (var field in entity.Fields)
{
    sb.Al(((ISnippet)new Property.Generator(field, indent: 1).Create()).Content);
}
```

## Available Context

Templates receive context through `Settings`:

```csharp
settings.Enterprise      // The full domain model
settings.DomainService   // Current service being processed
settings.Entity          // Current entity being processed
settings.SupportRegen    // Whether to generate regeneratable code
settings.Target          // Target framework (e.g., "net8.0")
settings.Version         // Template version
```

## Built-in Templates

| Template | Description |
|----------|-------------|
| `ApiSolution` | Complete API solution structure |
| `ApiProject` | Web API project |
| `ApiDomainProject` | Domain layer project |
| `ApiInfrastructureProject` | Infrastructure layer |
| `DatabaseProject` | Database schema (DBML) |
| `SdkProject` | Client SDK |
| `Header` | File header snippet |
| `Property` | Property generation snippet |

