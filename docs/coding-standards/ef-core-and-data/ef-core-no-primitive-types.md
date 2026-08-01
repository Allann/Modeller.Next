---
title: "No Primitive Obsession in EF Core Entities"
---

# No Primitive Obsession in EF Core Entities


## The Standard

Domain entities MUST NOT expose raw primitives (`string`, `int`, `decimal`, `CultureInfo`, etc.) for concepts that carry validation rules or behavior (titles, ISBNs, names, dates, keys). Wrap them in small, validating value objects (e.g. `BookTitle`, `Isbn`), and map those value objects back to plain columns with EF Core `ValueConverter`s and `ComplexProperty`/owned-type configuration, so the database schema stays flat while the C# model stays rich.

## Why

In the "before" version, `Book.Title`, `Book.TitleCulture`, and `Book.Isbn` are plain `string`/`string?` properties. Nothing stops an empty title, a malformed ISBN, or a mismatched culture from being constructed — validation, if it exists at all, is scattered in endpoint/request code far from the entity, producing an anemic model that is just a data bag. In the "after" version, `BookTitle` and `Isbn` are constructed only through validating constructors (throwing `ArgumentException` on empty titles or invalid ISBN formats), so an instance of `Book` can never hold an invalid title or ISBN — illegal states become unrepresentable at the type level. EF Core's `ValueConverter` and `ComplexProperty` mapping let this happen without denormalizing the schema: `Title`/`TitleCulture` remain two plain columns on the `Books` table, but in code they are one cohesive `BookTitle` value object.

## Before (Anti-pattern)

```csharp
public class Book
{
    public string Title { get; private set; }
    public string TitleCulture { get; private set; }
    public string? Isbn { get; private set; }
    public string Culture { get; private set; }

    public static Book CreateNew(
        string title, string titleCulture, string culture, string? isbn,
        IEnumerable<Author> authors, Release release, string key) =>
        new(0, key, title, titleCulture, culture, isbn, authors, release);
}

// Configuration: plain string columns, no invariants enforced anywhere
entityBuilder.ToTable("Books");
entityBuilder.HasIndex(book => book.Key).IsUnique();
```

## After (Standard)

```csharp
public class BookTitle
{
    public string Value { get; }
    public CultureInfo Culture { get; }

    public BookTitle(string value, CultureInfo culture)
    {
        Value = !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("Title is required");
        Culture = culture;
    }
}

public class Isbn
{
    public string Value { get; }

    public Isbn(string value)
    {
        if (value.Length != 13 || !long.TryParse(value, out _)) throw new ArgumentException("Invalid ISBN");
        Value = value;
    }
}

public class Book
{
    public BookTitle Title { get; private set; }
    public Isbn? Isbn { get; private set; }
    public CultureInfo Culture { get; private set; }
    // ...
}

// Configuration: value objects map to the same flat columns
entityBuilder.Property(book => book.Isbn).HasConversion(new NullableIsbnConverter());
entityBuilder.ComplexProperty(book => book.Title, builder =>
{
    builder.Property(t => t.Value).HasColumnName("Title");
    builder.Property(t => t.Culture).HasColumnName("TitleCulture")
        .HasConversion(new CultureInfoConverter());
});
```

## Rules for LLMs / Agents

- Never declare an entity property as a bare `string`/`int`/`decimal`/`Guid` when that value has format rules, a valid range, or meaning beyond "any value of that CLR type" (titles, codes, identifiers, money, ISBNs, dates-with-precision, etc.). Introduce a value object instead.
- Validate inputs in the value object's constructor and throw (e.g. `ArgumentException`) on invalid input — never let an entity be constructed in an invalid state, and never re-validate the same rule again downstream in endpoint/handler code.
- Give value objects a private/EF-only parameterless or reduced-arg constructor path only where EF Core materialization requires it; keep the public constructor validating.
- Map value objects to existing flat columns using `ValueConverter<TModel, TProvider>` (for single-column, e.g. `Isbn <-> string`) or `ComplexProperty`/owned types (for multi-column objects, e.g. `BookTitle` spanning `Title` + `TitleCulture`). Do not change the table shape just to accommodate the richer C# type.
- Keep behavior that belongs to the concept (comparisons, formatting, parsing) on the value object itself, not scattered across services/endpoints that consume the raw primitive.
- When a property can be legitimately absent, model it as a nullable value object (`Isbn?`) plus a nullable converter, not a nullable primitive with ad-hoc empty-string/sentinel checks.

## When NOT to apply

Genuinely meaningless-beyond-its-type primitives — e.g. a `boolean` flag, or a simple counter/ordinal like `BookAuthor.Ordinal` in this codebase — do not need a wrapper type; wrapping every primitive indiscriminately just adds ceremony without adding invariants worth enforcing.
