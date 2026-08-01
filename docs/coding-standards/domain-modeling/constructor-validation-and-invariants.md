---
title: "Enforce Invariants in Constructors via Factory Methods"
---

# Enforce Invariants in Constructors via Factory Methods


## The Standard

Every entity must guarantee its invariants are true the instant it exists — never leave a `// TODO Populate` gap or an object in a partially-valid state after construction. Expose creation through named static factory methods (`CreateNew`, `CreateExisting`) that validate arguments and throw immediately on violation, keep the raw constructor `private`, and give the object real behavior methods (`Append`, `Remove`) that preserve those invariants for the object's whole lifetime instead of letting callers mutate internal collections directly.

## Why

The "before" version's `Book` constructor had a `// TODO Populate the AuthorsCollection` comment — the object could be constructed without ever establishing the author relationship it depends on, and there was no `Append`/`Remove` API, so nothing would have stopped a caller from leaving `AuthorsCollection` empty or manipulating it inconsistently (e.g., duplicate authors, wrong `Ordinal`). The "after" version populates `AuthorsCollection` unconditionally as part of construction, validates `id` (`id <= 0 ? throw ... : id`) and `title`/`fullName` (`string.IsNullOrWhiteSpace(...) ? throw ... : ...`) directly in the constructor bodies, and adds `Append`/`Remove` methods that maintain the `Ordinal` invariant (renumbering remaining authors on removal) so the invariant "authors are contiguously ordered, no duplicates" holds for the object's entire life, not just at construction.

## Before (Anti-pattern)

```csharp
private Book(int id, string key, string title, CultureInfo culture, IEnumerable<Author> authors, Release release)
    : this(id, key, title, culture)
{
    // TODO Populate the AuthorsCollection
    Release = release;
}
```

## After (Standard)

```csharp
public static Book CreateExisting(int id, string title, CultureInfo culture, IEnumerable<Author> authors, Release release, string key) =>
    new(id <= 0 ? throw new ArgumentException("Identity must be positive") : id, key, title, culture, authors, release);

private Book(int id, string key, string title, CultureInfo culture, IEnumerable<Author> authors, Release release)
    : this(id, key, title, culture)
{
    AuthorsCollection = authors.Select((author, index) => new BookAuthor(this, author, index + 1)).ToList();
    Release = release;
}

public (Author author, int ordinal) Append(Author author)
{
    if (TryFind(author) is BookAuthor existing) return (existing.Author, existing.Ordinal);
    var @new = new BookAuthor(this, author, AuthorsCount + 1);
    AuthorsCollection.Add(@new);
    return (@new.Author, @new.Ordinal);
}

public bool Remove(Author author)
{
    var existing = TryFind(author);
    if (existing is null) return false;
    foreach (var next in AuthorsCollection.Where(ba => ba.Ordinal > existing.Ordinal)) next.Ordinal -= 1;
    AuthorsCollection.Remove(existing);
    return true;
}
```

## Rules for LLMs / Agents

- Never leave a constructor with a `// TODO` for populating a required collection or field; either populate it correctly at construction time or make the type require it as a constructor argument.
- Keep raw constructors `private`; expose `public static` factory methods (`CreateNew` for brand-new entities without an identity, `CreateExisting` for rehydrating persisted ones) that validate and throw on invalid input using expression-bodied guard clauses (`condition ? throw new ArgumentException(...) : value`).
- Validate identity/required-field invariants (positive IDs, non-empty required strings) inside the constructor itself, not in a separate "Validate()" method callers might forget to call.
- Expose behavior methods (`Append`, `Remove`, etc.) that keep collection-based invariants (ordering, uniqueness) consistent; never expose the backing collection as a public mutable property that callers can freely mutate.
- Use a separate, clearly-marked private constructor for ORM/EF Core materialization when it must be simpler than the full domain constructor, but still validate the fields it does set.

## When NOT to apply

Plain DTOs/response records meant purely for serialization at an API boundary (no behavior, no invariants beyond shape) do not need factory methods or constructor validation — that validation belongs in the request-validation pipeline instead.
