---
title: "Generate URL Slugs via a Composable Handle Pipeline, Not Ad Hoc String Munging"
---

# Generate URL Slugs via a Composable Handle Pipeline, Not Ad Hoc String Munging


## The Standard

Do not expose database-generated numeric IDs in URLs, and do not build slugs with inline string manipulation scattered at each call site. Instead, model a slug-in-progress as a `Handle(params string[] Components)` value, transform it through small composable `TransformHandle` delegates (`ToLowercase`, `StopAtColon`, `IntoLetterAndDigitRuns`, ...), and finish with a `HandleToSlug` delegate (e.g. `Hyphenate`) that yields a `Slug` value. Persist and route on the resulting human-readable key (e.g. `Book.Key`), with collision detection/avoidance applied before saving.

## Why

In the "before" state, entities were looked up and routed to by raw integer ID (`/authors/{id?}`, `/books/{id?}`), which produces opaque, unstable-looking URLs and offers no natural place to build a slug from a title/name in a reusable way. The "after" state adds a `Handle`/`Slug` value pipeline (`Common/Handle.cs`, `HandleTransforms.cs`, `HandleToSlugConversions.cs`, `Slug.cs`) plus per-entity slug delegates (`BookTitleToSlug`, `PublisherNameToSlug`, `PersonalNameToSlug`) that compose the same small set of transforms (lowercase, split on whitespace/colon, split into letter/digit runs) differently per entity, then hyphenate. Routes switch to `/books/{handle?}` and a new `Book.Key` (and `Author.Key`, `Publisher.Key`) column stores the generated slug, with `GetKeyCollisions` querying for and disambiguating collisions (`AvoidHandleCollisionsWithNumber`) before insert. The composable pipeline means the same transform building blocks are reused across every entity's slug logic instead of each entity reimplementing its own string-cleanup code, and the `Handle`/`Slug` distinction keeps "text being normalized" and "final URL-safe key" as separate, purpose-built types rather than passing raw `string` through every step.

## Before (Anti-pattern)

```csharp
// Numeric-ID routing, no slug generation at all.
app.MapGet("/books/{id?}", async (BookstoreDbContext dbContext, [FromRoute] int? id) =>
    await dbContext.Books.QueryAggregates().WithOptionalId(id).ToListAsync());
```

## After (Standard)

```csharp
// Composable, reusable transform pipeline turning a title into a URL-safe slug.
public record Handle(params string[] Components);
public delegate Slug HandleToSlug(Handle handle);
public delegate Handle TransformHandle(Handle handle);

public static class HandleTransformCompositions
{
    public static Handle Transform(this Handle handle, params TransformHandle[] transforms) =>
        transforms.Aggregate(handle, (current, transform) => transform(current));

    public static Slug ToSlug(this Handle handle, HandleToSlug conversion) => conversion(handle);
}

services.AddSingleton<BookTitleToSlug>(_ => (culture, title) =>
    new Handle(title)
        .Transform(ToLowercase(culture), StopAtColon, IntoLetterAndDigitRuns)
        .ToSlug(Hyphenate));

// Route by the generated, human-readable key instead of the numeric ID:
app.MapGet("/books/{handle?}", async (BookstoreDbContext dbContext, [FromRoute] string? handle) =>
    await dbContext.Books.QueryAggregates().WithOptionalKey(handle).ToListAsync());
```

## Rules for LLMs / Agents

- Do not route on or expose raw numeric surrogate keys (`int Id`) in public URLs for user-facing resources that have a natural name/title — generate and route by a slug/key instead.
- Build slug generation as a pipeline of small, named, reusable transform steps (lowercase, whitespace splitting, punctuation truncation, letter/digit-run splitting, etc.) composed via a `.Transform(...)` /`.ToSlug(...)` style extension, not as one large inline string-manipulation method per entity.
- Keep "the value being normalized" (`Handle`) and "the finished URL-safe key" (`Slug`) as distinct types rather than passing a bare `string` through every step of slug generation.
- Before saving an entity with a generated slug, check for and resolve collisions (e.g. query existing keys, append a disambiguating suffix) rather than relying on a database unique-constraint violation to be handled reactively.
- Allow a caller-supplied handle/slug to override the generated one when provided (as the demo does with `book.handle`), rather than always forcing auto-generation.

## When NOT to apply

For internal/admin-only APIs or tables with no user-facing URL exposure, plain numeric IDs are fine and adding a slug pipeline is unnecessary overhead.
