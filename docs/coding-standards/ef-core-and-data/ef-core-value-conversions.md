---
title: "EF Core Value Conversions for Rich Domain Types"
---

# EF Core Value Conversions for Rich Domain Types


## The Standard

Persist domain value objects and small discriminated unions (`CultureInfo`, `IEdition`, `PublicationDate`) as their real domain types on the model, and use `HasConversion(...)` (optionally combined with shadow properties) to translate to/from a primitive column type. Do NOT `Ignore()` these properties and do NOT weaken the domain model to a primitive (`string`, `int`) just to satisfy the ORM.

## Why

The initial configuration used `entityBuilder.Ignore(book => book.Culture)` and `entityBuilder.Ignore(release => release.Edition)` because EF Core didn't know how to map `CultureInfo` or the `IEdition` union — again silently dropping domain data. The final version keeps `Culture` typed as `CultureInfo` and `Edition` typed as `IEdition` on the C# model, while `HasConversion` supplies a pure two-way mapping function to a `varchar` column. For values that don't have a single natural column (a discriminated union of `Year`/`YearMonth`/`FullDate`), a shadow property (`entityBuilder.Property<PublicationDate?>("PublicationDate")`) captures the value alongside a discriminator shadow property (`"PublicationKind"`), so the whole union round-trips through a small number of conversion functions instead of forcing extra tables or nullable columns per case.

## Before (Anti-pattern)

```csharp
entityBuilder.Ignore(book => book.Culture);     // CultureInfo not persisted at all
entityBuilder.Ignore(release => release.Edition); // IEdition not persisted at all
entityBuilder.Ignore("PublicationDate");
```

## After (Standard)

```csharp
entityBuilder.Property(book => book.Culture)
    .HasConversion(culture => culture.Name, name => new CultureInfo(name))
    .HasColumnType("varchar(20)");

entityBuilder.Property(release => release.Edition)
    .HasConversion(edition => EditionToString(edition), formatted => StringToEdition(formatted))
    .HasColumnType("varchar(11)")
    .HasColumnName("Edition");

private string EditionToString(IEdition edition) => edition switch
{
    OrdinalEdition ordinal => $"{ordinal.Number}",
    SeasonalEdition seasonal => $"{Enum.GetName(seasonal.Season)} {seasonal.Year}",
    _ => throw new ArgumentException($"Edition type not supported ({edition.GetType().Name})")
};
```

## Rules for LLMs / Agents

- Never `Ignore()` a domain property to work around a mapping problem; that deletes the data. Reach for `HasConversion` (single column), a shadow property (`Property<T>("Name")`), or `ComplexProperty` (multiple columns) instead.
- Write conversion functions as small, pure, total functions (exhaustive `switch` over the union's cases with a `throw` for genuinely unsupported/impossible values).
- Keep the C# domain model expressed in real domain types (`CultureInfo`, `IEdition`, `PublicationDate`) — never downgrade a property's type to `string`/`int` purely for persistence convenience.
- When a value object is a discriminated union (multiple mutually-exclusive shapes), encode all cases into one column with a documented, reversible format (e.g. packed integer, delimited string) or a discriminator shadow property, rather than adding a nullable column per case.
- Pair each conversion function with round-trip coverage (unit test or property test) since these are hand-written serialization formats.

## When NOT to apply

If a value object naturally decomposes into several independent, always-required columns, prefer `ComplexProperty`/owned types over cramming it into one converted column — that keeps the schema queryable. Reserve single-column `HasConversion` for values that are conceptually atomic or when column-per-case would multiply nullable columns for a union.
