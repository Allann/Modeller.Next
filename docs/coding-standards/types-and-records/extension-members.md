---
title: "Use C# 14 Extension Members for Context-Specific Views, Not Core-Type Bloat"
---

# Use C# 14 Extension Members for Context-Specific Views, Not Core-Type Bloat


## The Standard

Keep core domain types minimal (state plus the invariants they must enforce), and add every consumer-specific or bounded-context-specific capability — DTO mapping, computed views, aggregate helpers — as a C# 14 `extension` block declared in that consumer's own folder/namespace, next to the DTO or feature it serves, rather than adding it as a member on the core type.

## Why

In the reference implementation, `Book.cs` only exposes `Title`, `AuthorNames`, `PublicationYear`, and the one mutation (`AddAuthor`) needed to protect its invariants. Two different contexts consume `Book` in incompatible ways: `BackOffice` needs to construct a `Book` from a posted DTO, while `BookStore` needs to render it as a summary or a details view. Instead of piling `From(BookPostDto)`, `ToSummaryDto()`, and `ToDetailsDto()` onto `Book` itself (which would make the core type depend on every consumer's DTOs and grow without bound as contexts are added), each context declares its own `extension(Book book) { ... }` block colocated with its DTO: `BackOffice/Pages/BookPostDto.cs` owns `Book.From(dto)`, `BookStore/Pages/BookSummaryDto.cs` owns `ToSummaryDto()`, `BookStore/Pages/BookDetailsDto.cs` owns `ToDetailsDto()`. `Program.cs` also shows a same-context extension (`AuthorsCount`, `AddAuthors`, `MergeAuthorsWith`) used to add convenience read/write operations without touching `Book`'s definition. This keeps the core type stable, keeps each bounded context's concerns physically isolated, and lets extension *properties* and *static* extension members (new in C# 14) express read-only views and factory functions with the same natural call syntax as real members — `book.AuthorsCount`, `Book.From(dto)` — instead of the old awkward `static` helper-method style (`BookExtensions.GetAuthorsCount(book)`).

## Before (Anti-pattern)

```csharp
// Every consumer's concern crammed onto the core type itself
class Book
{
    public string Title { get; private set; } = "";
    public IEnumerable<string> AuthorNames => AuthorNamesCollection;
    public int PublicationYear { get; private set; }
    private List<string> AuthorNamesCollection = new();

    public void AddAuthor(string name) => AuthorNamesCollection.Add(name);

    // BackOffice concern leaking into the core domain type
    public static Book From(BookPostDto dto) { /* ... */ }

    // BookStore concerns leaking into the core domain type
    public BookSummaryDto ToSummaryDto() => new(Title, PublicationYear);
    public BookDetailsDto ToDetailsDto() => new(Title, AuthorNames.ToArray(), PublicationYear);

    // Old-style "static helper" extension, called awkwardly
    public static int GetAuthorsCount(Book book) => book.AuthorNames.Count();
}
```

## After (Standard)

```csharp
// BackOffice/Pages/BookPostDto.cs
namespace BackOffice.Pages;

record BookPostDto(string Title, string[] AuthorNames, int PublicationYear);

static class BookDtoTransforms
{
    extension(Book book)
    {
        public static Book From(BookPostDto dto)
        {
            var newBook = new Book(dto.Title, dto.PublicationYear);
            foreach (var author in dto.AuthorNames) newBook.AddAuthor(author);
            return newBook;
        }
    }
}

// BookStore/Pages/BookSummaryDto.cs
namespace BookStore.Pages;

record BookSummaryDto(string Title, int PublicationYear);

static class BookSummaryTransforms
{
    extension(Book book)
    {
        public BookSummaryDto ToSummaryDto() => new(book.Title, book.PublicationYear);
    }
}
```

## Rules for LLMs / Agents

- Keep the core domain type limited to the state and invariant-preserving members every consumer needs; do not add a member to it just because one context wants it.
- Place each context's `extension(CoreType x) { ... }` block in that context's own folder/namespace, colocated with the DTO or feature that motivates it (e.g. `BackOffice/Pages/BookPostDto.cs`, not a shared `BookExtensions.cs` file with everything mixed in).
- Use extension properties (`public int AuthorsCount => ...`) for read-only computed views instead of ad-hoc static helper methods.
- Use static extension members (`public static Book From(Dto dto)`) for context-specific factory/parsing logic instead of adding statics to the core type or writing a separate free-standing factory class.
- Do not attempt to add instance state or a true property setter inside an extension block — the underlying feature only supports members expressible in terms of the extended instance's existing public surface; if state or a real mutator is needed, it belongs on the core type itself.
- Name the extension's containing static class after the transform it groups (e.g. `BookSummaryTransforms`, `BookDtoTransforms`), not generically (`BookExtensions`) once more than one purpose exists in a project.

## When NOT to apply

Requires C# 14 and .NET 10 (at the time of writing, preview-only). Do not use extension blocks (`extension(Type x) { ... }`) on projects targeting an older C# language version or earlier .NET TFMs — fall back to conventional `static` extension methods there. Also do not reach for this pattern when the "extension" would need to hold its own state or implement a real mutating property/indexer setter; that functionality is a core-type responsibility, not an extension-member one.
