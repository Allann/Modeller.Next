---
title: "Immutable Domain Models"
---

# Immutable Domain Models


## The Standard

Model domain entities as immutable `record` types with value-object members, built through validating factory methods, rather than as mutable classes with public setters. State transitions MUST return a new instance (`with` expressions) instead of mutating fields in place.

## Why

The mutable version keeps title/keyword invariants alive by re-deriving `_titleKeywords` in every setter, which means any future field or code path that forgets to call the setter (e.g. field assignment, reflection, deserialization) silently corrupts the object's invariants. Validation logic (`ValidTitle`) is duplicated across the constructor and the setter. The immutable version pushes validation into a single factory (`Title.Create`, `Keyword.Create`) that can fail closed (return `null`/`Option`) instead of throwing, and once constructed the object can never enter an invalid state because there are no setters to bypass. It also makes the model trivially thread-safe and safe to share/cache, and equality/`ToString` come for free from `record`.

## Before (Anti-pattern)

```csharp
public class Book(string title, string[] keywords)
{
    private string _title = ValidTitle(title);
    private string[] _titleKeywords = ExtractKeywords(title);

    public string Title
    {
        get => _title;
        set
        {
            _title = ValidTitle(value);          // invariant only enforced if setter is used
            _titleKeywords = ExtractKeywords(value);
        }
    }

    public void AddKeyword(string keyword) => _externalKeywords.Add(keyword); // mutates in place
}
```

## After (Standard)

```csharp
public record Book(Title Title, ImmutableList<Keyword> BookKeywords)
{
    public Book(Title title) : this(title, []) { }

    public IEnumerable<Keyword> Keywords => Title.Keywords.Concat(BookKeywords);

    public Book Add(Keyword? keyword) =>
        keyword is null ? this
        : this with { BookKeywords = BookKeywords.Add(keyword) };
}

public record Keyword(string Value)
{
    public static Keyword? Create(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : new(value.Trim());
}
```

## Rules for LLMs / Agents

- Prefer `record`/`record struct` over `class` for domain models and value objects; do not add public setters to domain state.
- Validate at construction through a static factory (`Create`) that returns the value or a failure indicator (`null`, `Option<T>`), never a partially-valid object.
- Represent state changes as pure methods returning a new instance via `with { ... }`, never by mutating a field/list belonging to an already-constructed instance.
- Use immutable collection types (`ImmutableList<T>`, `IReadOnlyList<T>`) for collection-valued members instead of `List<T>`/arrays exposed by reference.
- Do not derive/cache a value in a private field that must be kept in sync by every mutator; instead compute it as a property/expression from the immutable state.

## When NOT to apply

Performance-critical hot paths with proven allocation pressure (e.g. tight loops mutating large buffers) may justify mutable structures; document the tradeoff at the call site. EF Core entity types that must support change tracking may need settable properties for materialization, but should still expose behavior-driven methods for domain transitions rather than letting callers set properties directly (see `ef-core-record-type-tracking.md`).
