---
title: "Keep Minimal API Handlers Thin by Extracting DTOs, Queries, and Pipelines"
---

# Keep Minimal API Handlers Thin by Extracting DTOs, Queries, and Pipelines


## The Standard

Keep ASP.NET Core Minimal API route registrations as a thin, declarative list in `Program.cs`. Push everything else out: request/response shapes into dedicated DTO records with colocated mapping extension methods, reusable query logic into `IQueryable<T>` extension methods, and multi-step string/key transforms (like slug generation) into a composable extension-method pipeline registered once via DI, so each route handler reads as a short expression rather than inline logic.

## Why

`Program.cs` maps every route directly on `app`, but the "cleanup" is in what each handler body delegates to rather than in how routes are grouped: `Requests.cs` holds flat DTO records separate from domain models; each response DTO's file also owns its own `ToResponse`/`ToSimpleResponse` mapping extension; query composition (`WithKey`, `WithOptionalAuthorKey`, `QueryAggregates`) lives as `IQueryable<T>` extensions in `Data/Queries/`; and slug/handle generation is a registered, composable delegate pipeline (`Transform(...).ToSlug(...)`) rather than inline string manipulation in the handler. This keeps a handler's job limited to "call the pipeline, map the result, return it" — the actual logic is unit-testable and reusable independent of the HTTP layer.

## Before (Anti-pattern)

```csharp
app.MapGet("/books/{handle}", async (BookstoreDbContext dbContext, string handle) =>
{
    var book = await dbContext.Books
        .Include(b => b.Authors)
        .Include(b => b.Publisher)
        .FirstOrDefaultAsync(b => b.Handle == handle);   // query logic inlined in the handler

    if (book is null) return Results.NotFound();

    return Results.Json(new
    {
        book.Title,
        Authors = book.Authors.Select(a => a.Name),      // response shaping inlined too
        book.PublicationYear
    });
});
```

## After (Standard)

```csharp
// Data/Queries/BookQueries.cs
static class BookQueries
{
    public static IQueryable<Book> QueryAggregates(this IQueryable<Book> books) =>
        books.Include(b => b.Authors).Include(b => b.Publisher);

    public static IQueryable<Book> WithKey(this IQueryable<Book> books, string handle) =>
        books.Where(b => b.Handle == handle);
}

// Responses/BookResponse.cs
record BookResponse(string Title, string[] Authors, int PublicationYear);

static class BookResponseTransforms
{
    public static BookResponse ToResponse(this Book book) =>
        new(book.Title, book.Authors.Select(a => a.Name).ToArray(), book.PublicationYear);
}

// Program.cs — handler is now a thin pipeline
app.MapGet("/books/{handle}", async (BookstoreDbContext dbContext, string handle) =>
    await dbContext.Books.QueryAggregates().WithKey(handle).FirstOrDefaultAsync() switch
    {
        Book book => Results.Json(book.ToResponse()),
        _ => Results.NotFound()
    });
```

## Rules for LLMs / Agents

- Never map a domain entity straight into an HTTP response — define a dedicated response DTO record and a colocated `ToResponse`/`ToSimpleResponse` extension method next to it.
- Define request DTOs as flat records separate from domain models; do not bind Minimal API routes directly to domain entities.
- Extract reusable EF Core query shaping (`Include`, filtering, key lookups) into `IQueryable<T>` extension methods in a `Data/Queries/` (or equivalent) location, so handlers compose a pipeline instead of writing inline LINQ/EF.
- Extract multi-step string/key transforms (slugs, handles, formatted keys) into composable delegate pipelines registered once via DI, rather than repeating the logic inline per handler.
- Keep the route handler body itself limited to: call the query/pipeline, pattern-match or check the result, map to a response, return it.

## When NOT to apply

For a genuinely trivial route (e.g., a health check or a single-field echo) with no query or mapping to extract, keep the handler inline — the extraction only pays off once there's real logic to isolate.
