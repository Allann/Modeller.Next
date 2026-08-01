---
title: "Avoid Anemic Domain Models: Put Behavior on the Model, Not in a Service"
---

# Avoid Anemic Domain Models: Put Behavior on the Model, Not in a Service


## The Standard

Domain classes (`Book`, `PublicationDate`, `Edition`) MUST own the behavior and invariants that concern their own state. Do not build a class that is only public auto-properties/setters plus a same-named `*Services` class that performs every mutation, validation, and query against it from the outside — that is the anemic domain model anti-pattern and it forfeits the benefits of OOP (encapsulation, locality of behavior, impossible-by-construction invalid states).

## Why

Across the "Initial" -> "Transaction Scripts" -> "CRUD" progression in this material, `Book` never grows behavior: it stays a bag of public/settable properties, while `BookServices.CreateNew/GetBooks/DeleteBook/Update` performs every operation on it from outside, including reaching into EF Core's change tracker (`_dbContext.Entry(existingBook).CurrentValues.SetValues(book)`). `PublicationDate` and `Edition` compound the problem: instead of being closed hierarchies of states (as in `avoid-boolean-state-flags.md`), they are single classes with a `bool IsPublished`/`bool IsDaySpecified`/`bool IsMonthSpecified` triplet plus nullable `Date`/`Number`/`Season`/`Year` fields, so illegal combinations (e.g. `IsDaySpecified = true` with `Date = null`) are representable and have to be defended against everywhere the type is used instead of being ruled out by the type itself. The Readme for this bundle explicitly frames the final stage as "the beginning of redesigning into a rich domain model" — i.e. this material documents the failure mode to recognize and refactor away from, not a finished solution.

## Before (Anti-pattern)

```csharp
public class Book
{
    public int Id { get; private set; }
    public string Title { get; set; }              // freely settable, no invariant enforcement
    public PublicationDate Date { get; private set; }
    public ICollection<BookAuthor> Authors { get; private set; }
}

public class PublicationDate
{
    public DateOnly? Date { get; private set; }
    public bool IsDaySpecified { get; private set; }
    public bool IsMonthSpecified { get; private set; }
    public bool IsPublished { get; private set; }   // combinatorial flags instead of a type per state
}

// all behavior lives outside the model
public class BookServices
{
    public async Task Update(Book book)
    {
        Book existingBook = await _dbContext.Books.FindAsync(book.Id);
        _dbContext.Entry(existingBook).CurrentValues.SetValues(book); // bypasses any domain rule entirely
    }
}
```

## After (Standard — the direction this material points toward)

```csharp
public abstract record PublicationDate
{
    public abstract bool IsPublishedBefore(DateOnly date);
}
public sealed record Published(DateOnly Date) : PublicationDate { /* ... */ }
public sealed record Planned(DateOnly Date) : PublicationDate { /* ... */ }
public sealed record NotPublished : PublicationDate { /* ... */ }

public class Book
{
    public string Title { get; private set; }
    public PublicationDate PublicationDate { get; private set; }
    private readonly List<BookAuthor> _authors = [];
    public IReadOnlyList<BookAuthor> Authors => _authors;

    public void Publish(DateOnly date) => PublicationDate = new Published(date);   // behavior lives on the model

    public void AddAuthor(Author author) =>
        _authors.Add(new BookAuthor(this, author, _authors.Count + 1));
}
```

## Rules for LLMs / Agents

- Do not create a `*Services`/`*Manager` class whose only job is to perform CRUD/mutation on another class's public setters; put the mutation as a named method on the domain type itself (`Publish`, `AddAuthor`, `Rename`) that enforces its own invariants.
- Never expose a public setter on a domain entity for a property that has business rules attached to changing it; expose an intention-revealing method instead.
- Replace a group of related booleans (`IsPublished`, `IsDaySpecified`, `IsMonthSpecified`) plus nullable fields with a closed hierarchy of types, one per real state (see `avoid-boolean-state-flags.md`).
- Do not use `dbContext.Entry(existing).CurrentValues.SetValues(incoming)` (or equivalent blind property-copy) to "update" an entity — it bypasses every domain invariant the entity is supposed to enforce. Call domain methods on the tracked entity instead.
- Treat a service/application layer as an orchestrator of domain objects and persistence, not as the home for logic that belongs on the domain object.

## When NOT to apply

Pure CRUD admin screens over data with no real business rules (e.g. a lookup table maintenance page) may legitimately use a thin, anemic-style DTO plus a generic repository — introducing rich behavior there is unnecessary ceremony. Reserve rich domain modeling for entities that actually carry business rules and invariants.
