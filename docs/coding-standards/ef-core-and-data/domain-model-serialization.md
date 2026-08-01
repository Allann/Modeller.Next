---
title: "Domain Model Serialization: DTOs, Not Domain Entities, Cross the Wire"
---

# Domain Model Serialization: DTOs, Not Domain Entities, Cross the Wire


## The Standard

Never serialize domain entities or their value objects (e.g. `Handle`, `Slug`, `Option<T>`, `PublicationDate`, `PersonalName`) directly to or from JSON. Every endpoint MUST define a dedicated `Request` record for input and a dedicated `Response` record for output, built from plain primitives (`string`, `int`, `string?` dates), and MUST map explicitly between the domain model and these DTOs via small `ToResponse`/`To...Response` extension methods (or equivalent constructors from validated request fields).

## Why

The domain model in this codebase (`DemoApi/Models`, `DemoApi/Common`) is intentionally rich: `PublicationDate` is a closed hierarchy (`FullDate`/`YearMonth`/`Year`), `Handle`/`Slug` are value objects with transform pipelines, `Option<T>` encodes optional values without null, and entities like `Author`/`Book` hide their constructors behind `CreateNew`/`CreateExisting` factories to enforce invariants. None of these types are annotated with `[JsonConverter]` or otherwise made JSON-aware — a grep across the whole solution turns up zero `JsonConverter`/`JsonSerializerOptions` usage tied to domain types. EF Core's `ValueConverter`s (`Data/Converters/PublicationDateConverter.cs`) exist purely to translate value objects to storage primitives (an `int` for `PublicationDate`), a separate and orthogonal concern from wire serialization.

Instead, every handler (`Endpoints/AuthorsHandlers.cs`, etc.) maps the domain object to a `Response` record whose shape matches what the API actually wants to expose (e.g. `AuthorResponse` exposes `FirstName`/`LastName`/`FullName`/`Culture`/`Url` as flat strings, collapsing `PersonalName` and `CultureInfo` in the process). The mapping methods (`AuthorResponseTransforms.ToAuthorResponse`, `BookResponseTransforms.ToResponse`) perform the pattern-matching needed to flatten sum types like `PublicationDate`/`IEdition`/`PublicationInfo` into plain strings, and requests (`PostAuthorRequest`) accept raw strings that the handler validates and converts into value objects (`CultureInfo.GetCultureInfo`, `new PersonalName(...)`) before constructing the entity.

This separation means: (1) the wire format is stable and intentional, not an accidental byproduct of the domain model's internal shape; (2) the domain model is free to evolve its internal representation (e.g. change how `PublicationDate` is structured) without breaking API consumers; (3) validation of untrusted input happens against a flat DTO before any domain invariant-enforcing constructor is invoked; (4) there is no reflection-based or converter-based coupling between `System.Text.Json` and domain invariants.

## Before (Anti-pattern)

```csharp
// Naive: serialize the domain entity/value object directly, patching
// System.Text.Json with converters to cope with its rich shape.
public class Author
{
    public string Key { get; private set; }
    public PersonalName Name { get; private set; }          // rich value object
    [JsonConverter(typeof(PublicationDateJsonConverter))]     // hack to (de)serialize a sum type
    public PublicationDate? Released { get; private set; }
    public CultureInfo Culture { get; private set; }          // leaks BCL internals (LCID etc.)
}

app.MapGet("/authors/{handle}", async (BookstoreDbContext db, string handle) =>
    Results.Json(await db.Authors.FirstOrDefaultAsync(a => a.Key == handle)));
// Whatever shape Author happens to have today becomes the public contract,
// and Author can never change internally without breaking clients.
```

## After (Standard)

```csharp
public record AuthorResponse(string FirstName, string LastName, string FullName, string Culture, string Url);

public static class AuthorResponseTransforms
{
    public static AuthorResponse ToAuthorResponse(this Author author, UriHelper uriHelper) =>
        new(author.Name.First, author.Name.Last, author.FullName,
            author.Culture.Name, uriHelper.FormatAuthorUrl(author).AbsoluteUri);
}

record PostAuthorRequest(string FullName, string Culture, string FirstName, string? MiddleNames, string LastName, string? Handle);

public static async Task<IResult> PostAuthor(BookstoreDbContext db, UriHelper uriHelper, PostAuthorRequest author)
{
    // validate the flat DTO, then construct the domain entity through its factory
    var name = new PersonalName(author.FirstName, author.MiddleNames ?? string.Empty, author.LastName);
    var newAuthor = Author.CreateNew(CultureInfo.GetCultureInfo(author.Culture), name, author.FullName, handle);
    db.Authors.Add(newAuthor);
    await db.SaveChangesAsync();
    return Results.Json(newAuthor.ToAuthorResponse(uriHelper));
}
```

## Rules for LLMs / Agents

- Never put `[JsonConverter]`, `[JsonPropertyName]`, or any `System.Text.Json` attribute on a domain entity or domain value object (`Handle`, `Slug`, `Option<T>`, sum-type records like `PublicationDate`/`IEdition`). If a converter is needed, it belongs in `Data/Converters` for EF Core persistence only, never for wire serialization.
- For every endpoint that accepts a body, define a `record` in an `Endpoints/Requests` folder using only primitive types (`string`, `int`, nullable primitives). Validate its fields explicitly in the handler before constructing domain objects.
- For every endpoint that returns a body, define a `record` in an `Endpoints/Responses` folder using only primitive types, and implement the mapping as a `ToXResponse`/`ToResponse` extension method on the domain type, kept next to the response record.
- Never call `Results.Json(entity)` (or return a domain entity/EF Core query result directly) from a Minimal API handler — always map to a `Response` DTO first.
- Flatten domain sum types (e.g. `PublicationDate`, `IEdition`, `PublicationInfo`) to a single representation appropriate for the DTO (a formatted string, a discriminated primitive) using exhaustive pattern matching in the mapping method, not by exposing the type hierarchy to the serializer.
- Construct domain entities only through their factory methods (`CreateNew`/`CreateExisting`) from validated request data — never deserialize JSON straight into a domain entity's constructor.
- Keep `ValueConverter`s in `Data/Converters` scoped to EF Core persistence concerns; do not reuse them as JSON converters and do not assume they satisfy the wire-serialization requirement.

## When NOT to apply

None observed. The reference implementation applies this separation uniformly across all endpoints (`Authors`, `Books`, `Publishers`) and entity shapes, including simple ones (`PublisherResponse`) and complex nested ones (`AuthorWithBooksResponse`, `BookResponse`), suggesting no carve-out was intended even for trivial DTOs.
