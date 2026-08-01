---
title: "Add Record Factories and Fluent Operations via Extension Members, Not Record Body Bloat"
---

# Add Record Factories and Fluent Operations via Extension Members, Not Record Body Bloat


## The Standard

Keep a `record`'s primary declaration limited to its positional data (`public record Author(Guid Id, string FirstName, string LastName);`). Put named creation logic (`New`, `Restore`) and fluent, record-returning operations (`WithAuthor`) in a separate `static class` using C# 14 extension members (`extension(Author) { ... }` for statics, `extension(Book book) { ... }` for instance-like members), grouped by the concept they implement rather than crammed into the record body or a same-named partial.

## Why

`Author` and `Book` stay one-line declarations that show their shape at a glance. `AuthorCreation`/`BookCreation` add `New(...)` (generates an ID, validates input) and `Restore(...)` (reconstructs from a known ID, e.g. from persistence) as *static extension members on the record type itself* — callers write `Author.New(first, last)` and `Book.Restore(id, title, authors)`, exactly as if these were real static factory methods, without the record's own declaration ever mentioning validation or ID generation. `BookManagement.WithAuthor` adds an instance-like fluent operation (`book.WithAuthor(author)`) that returns a new `Book` via `with`, again without touching `Book`'s declaration. This separates "what the record is" from "how instances of it get created and combined," and lets the extension file group construction/behavior logic by responsibility (`AuthorCreation`, `BookCreation`, `BookManagement`) instead of one growing record body.

## Before (Anti-pattern)

```csharp
// Validation, ID generation, and fluent helpers crammed into the record itself
public record Book(Guid Id, string Title, ImmutableList<Author> Authors)
{
    public static Book New(string title) =>
        string.IsNullOrWhiteSpace(title) ? throw new ArgumentException("Title cannot be empty")
        : new Book(Guid.NewGuid(), title, []);

    public Book WithAuthor(Author author) =>
        Authors.Any(a => a.Id == author.Id) ? this : this with { Authors = Authors.Add(author) };
}
```

## After (Standard)

```csharp
public record Book(Guid Id, string Title, ImmutableList<Author> Authors);

public static class BookCreation
{
    extension(Book)
    {
        public static Book New(string title) => new(Guid.NewGuid(), title.AsValidTitle(), []);
        public static Book Restore(Guid id, string title, IEnumerable<Author> authors) =>
            new(id, title.AsValidTitle(), authors.ToImmutableList());
    }
}

public static class BookManagement
{
    extension(Book book)
    {
        public Book WithAuthor(Author author) =>
            book.Authors.Any(a => a.Id == author.Id) ? book : book with { Authors = book.Authors.Add(author) };
    }
}
```

## Rules for LLMs / Agents

- Keep a record's primary declaration to its positional parameter list; do not add factory methods or derived operations directly inside the record body when an extension member can express them instead.
- Use `extension(RecordType) { public static ... }` for named construction paths (`New`, `Restore`, `Parse`, etc.) rather than overloading the constructor or adding statics inside the record.
- Use `extension(RecordType instance) { ... }` for record-returning fluent operations (`WithX`), so call sites read naturally (`book.WithAuthor(a)`) without the record itself growing.
- Name each extension's containing static class after the responsibility it groups (`AuthorCreation`, `BookManagement`), and keep unrelated extension groups in separate static classes/files.
- Validate inputs inside the extension factory (throwing or otherwise) so an invalid record instance is never constructible through the public `New`/`Restore` path — but do not put that validation logic in the record's own body.

## When NOT to apply

Requires C# 14 (extension members) — on earlier language versions use conventional `static` factory methods and extension methods instead. Do not use this pattern when creation must guarantee no invalid instance can ever exist even via the primary constructor (records still allow calling `new Book(...)` directly, bypassing the extension factory); for that stronger guarantee, see the record-types-validation standard, which hides the constructor itself.
