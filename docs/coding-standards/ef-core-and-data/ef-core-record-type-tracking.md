---
title: "Tracking Rich Domain Types (Records, Discriminated Unions) With EF Core"
---

# Tracking Rich Domain Types (Records, Discriminated Unions) With EF Core


## The Standard

When a domain model contains rich value types that EF Core cannot map directly (interfaces such as `IEdition`, closed hierarchies of records, discriminated unions), do NOT flatten the model or `Ignore()` the property to make EF Core happy. Instead, add a private shadow "representation" property (a plain record with a type discriminator) that converts to/from the domain type, and map that representation with `ComplexProperty(...).Configure(...)`.

## Why

The initial version could not persist `Release.Edition`/`Release.Publication` because they are polymorphic (`IEdition`, `PublicationInfo`), so the naive fix was `entityBuilder.Ignore(...)` — silently dropping real domain state from the database. That is a data-loss bug hiding behind a passing build. The final version keeps the public API fully domain-typed (`IEdition Edition { get; }`) while a private `EditionRepresentation` property (backed by a `Type Discriminator` + flattened fields) does the round-trip conversion, and a small reusable `IComplexPropertyConfiguration<T>` + `ComplexPropertyBuilderConfiguration` extension keeps each configuration class composable and testable in isolation.

## Before (Anti-pattern)

```csharp
class ReleaseConfiguration : IComplexPropertyConfiguration<Release>
{
    public ComplexPropertyBuilder<Release> Configure(ComplexPropertyBuilder<Release> entityBuilder)
    {
        entityBuilder.Ignore(release => release.Publisher);
        entityBuilder.Ignore(release => release.Edition);      // domain state silently dropped
        entityBuilder.Ignore(release => release.Publication);  // domain state silently dropped
        return entityBuilder;
    }
}
```

## After (Standard)

```csharp
// Domain type stays polymorphic and public.
public class Release
{
    public IEdition Edition { get; private set; }

    // Shadow representation used only for persistence, never exposed publicly.
    private EditionRepresentation EditionRepresentation
    {
        get => Edition.ToRepresentation();
        set => Edition = value.ToEdition();
    }
}

public record EditionRepresentation(Type Discriminator, YearSeason? Season, int Number);

class ReleaseConfiguration : IComplexPropertyConfiguration<Release>
{
    public ComplexPropertyBuilder<Release> Configure(ComplexPropertyBuilder<Release> entityBuilder)
    {
        entityBuilder.Ignore(release => release.Edition);
        entityBuilder.ComplexProperty<EditionRepresentation>("EditionRepresentation")
            .Configure(new EditionRepresentationConfiguration())
            .UsePropertyAccessMode(PropertyAccessMode.Property);
        return entityBuilder;
    }
}
```

## Rules for LLMs / Agents

- Never use `entityBuilder.Ignore(...)` on a property purely to make the model compile/migrate — it deletes that data from persistence. Only `Ignore` properties that are genuinely computed/transient.
- When a domain property's type is polymorphic or otherwise unmappable, introduce a private "representation" record with an explicit discriminator and `ToRepresentation()`/`ToXxx()` conversion methods, and map the representation via `ComplexProperty`.
- Keep the representation type private to the entity/aggregate; the public domain API must stay expressed in real domain types (`IEdition`), never leak the persistence shape.
- Factor `IComplexPropertyConfiguration<T>` + a `Configure` extension so each nested value object gets its own small, independently readable configuration class instead of one giant `OnModelCreating`.
- Use `UsePropertyAccessMode(PropertyAccessMode.Property)` on shadow representation properties so EF goes through the conversion getter/setter rather than trying to reach a backing field directly.

## When NOT to apply

If the type is naturally flat and EF Core's built-in value converters (`HasConversion`) or owned types already express it correctly, do not add a needless shadow representation — that's needless indirection. Reserve this pattern for genuinely polymorphic/discriminated domain state.
