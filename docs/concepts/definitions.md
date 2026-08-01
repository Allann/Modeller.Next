---
title: "Definitions"
---

# Definitions

> ⚠️ **Legacy Documentation** - This describes the existing C# fluent API implementation. For the future YAML-based approach, see [Future Specification](architecture/draft/README.md).

Definitions describe your **domain model** - the entities, services, fields, and relationships that templates will transform into code.

## Structure

```
Enterprise
├── Company (string)
├── Project (Name)
├── Version (FileVersion)
├── Services[]
│   ├── Name
│   ├── Entities[]
│   └── Enumerations[]
├── Entities[]
│   ├── Name
│   ├── Key
│   ├── Fields[]
│   └── Relationships[]
└── Enumerations[]
    ├── Name
    └── Values[]
```

## Creating a Definition

Definitions inherit from `DefinitionBase` and implement the `Define()` method:

```csharp
public class MyDomainDefinition : DefinitionBase
{
    protected override EnterpriseBuilder Define()
    {
        return new EnterpriseBuilder()
            .For("MyCompany")
            .Project(new("MyProject"))
            .AddDocumentation("My domain description")
            .WithVersion(new("1.0"))
            .AddEntities()
            .AddServices()
            .AddEnumerations();
    }
}
```

## Fluent Builder API

### EnterpriseBuilder

```csharp
new EnterpriseBuilder()
    .For("CompanyName")              // Set company
    .Project(new("ProjectName"))      // Set project name
    .AddDocumentation("Summary")      // Add summary/remarks
    .WithVersion(new("1.0"))          // Set version
```

### Adding Entities

```csharp
builder
    .AddEntity(new Name("Customer"))
        .AddDocumentation("Represents a customer")
        .IsRoot()                      // Mark as aggregate root
        .SupportCrud(CrudSupport.All)  // Enable CRUD operations
        .AddKey(new Name("Customer"), 1)
            .AddDocumentation("Primary key")
            .AddField(new("CustomerId"))
                .WithDataType(DataType.Int32)
                .Generated(ValueGeneratedTypes.Add)
            .And
        .And
        .AddField(new("Name"))
            .AsBusinessKey()
            .WithDataType(DataType.String.WithMaxLength(100))
            .AddDocumentation("Customer name")
        .And
    .And
```

### Adding Services

```csharp
builder
    .AddService(new("CustomerManagement", true))
        .AddDocumentation("Customer management service")
        .AddEntities([CustomerName, AddressName, ContactName])
        .AddEnums([CustomerStatusEnum])
    .And
```

### Field Data Types

```csharp
// Primitives
DataType.String.WithMaxLength(50)
DataType.Int32
DataType.Int64
DataType.Bool
DataType.Decimal.WithPrecision(18, 2)
DataType.DateTimeOffset
DataType.UniqueIdentifier

// Complex types
DataType.Entity(new Name("Address"))
DataType.Enum.Of(new Name("Status"))
DataType.String.AsCollection(CollectionTypes.List)
```

## Definition Metadata

Each definition project needs a metadata class:

```csharp
public class DefinitionData : IDefinitionMetaData
{
    public FileVersion Version => new("1.0");
    public string Name => "MyDomain";
    public string Description => "My domain definition.";
    public Type EntryPoint => typeof(MyDomainDefinition);
}
```

## Project Structure

```
MyDefinition/
├── MyDefinition.csproj
├── DefinitionData.cs         # IDefinitionMetaData implementation
├── Definition.cs             # Main definition class
├── Entities/                 # Entity extension methods
│   ├── CustomerExtensions.cs
│   └── OrderExtensions.cs
└── GlobalUsings.cs
```

## Data Models

Definitions produce `EnterpriseData` which is mapped to runtime models:

| Data Class | Runtime Model | Purpose |
|------------|---------------|---------|
| `EnterpriseData` | `Enterprise` | Top-level container |
| `ServiceData` | `Service` | Bounded context |
| `EntityData` | `Entity` | Domain object |
| `FieldData` | `Field` | Entity property |
| `EnumerationData` | `Enumeration` | Enum definition |

