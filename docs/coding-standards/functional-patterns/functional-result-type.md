---
title: "Compose Result Values with Map/Bind/And/MapError, Not Sequential IsSuccess Checks"
---

# Compose Result Values with Map/Bind/And/MapError, Not Sequential IsSuccess Checks


## The Standard

Once a `Result<T, TError>` type exists, stop branching on `.IsSuccess` imperatively at each step. Compose validations and transformations through `Map` (transform the success value), `Bind` (chain an operation that itself returns a `Result`), `And` (combine independent results into a tuple, accumulating all errors), and `MapError` (transform the error channel) so an endpoint or workflow is a single expression pipeline.

## Why

The naive style repeats the same shape — run an operation, check `IsSuccess`, add to an error accumulator, repeat, then check the accumulator — at every validation step, mixing control flow with the mutable `ValidationErrorResponse` being built up. The pipeline style instead treats validation as combining independent `Result`s (so *all* field errors are collected, not just the first) and threads a single expression from raw input to either a fully validated value or an error response, with no `if` branching on success/failure.

## Before (Anti-pattern)

```csharp
ValidationErrorResponse validationErrors = new(uriHelper.FormatDocumentationUrl<PostBookRequest>());

Result<Publisher, string> publisher = await LoadPublisher(dbContext, book);
if (!publisher.IsSuccess) validationErrors.AddFieldError(nameof(book.PublisherHandle), publisher.Error);

Result<List<Author>, string> authors = await LoadAuthors(dbContext, book);
if (!authors.IsSuccess) validationErrors.AddFieldError(nameof(book.AuthorHandles), authors.Error);

Result<CultureInfo, string> titleCulture = book.TitleCulture.TryParseCultureName();
if (!titleCulture.IsSuccess) validationErrors.AddFieldError(nameof(book.TitleCulture), titleCulture.Error);

if (validationErrors.ContainsErrors()) return Results.BadRequest(validationErrors);
```

## After (Standard)

```csharp
Result<(BookRequestFields fields, BookTitle title), ValidationErrorResponse> request =
    (await ValidateBookRequestFields(dbContext, uriHelper, book))
        .Bind(fields => fields.WithBookTitle(uriHelper, book));

if (!request.IsSuccess) return Results.BadRequest(request.Error);

static async Task<Result<BookRequestFields, ValidationErrorResponse>> ValidateBookRequestFields(
    BookstoreDbContext dbContext, UriHelper uriHelper, PostBookRequest request) =>
    (await ValidatePublisherAsync(dbContext, request))
        .And(await ValidateAuthorsAsync(dbContext, request))
        .And(ValidateCulture(request.TitleCulture, nameof(request.TitleCulture)))
        .And(ValidateCulture(request.Culture, nameof(request.Culture)))
        .Map(fields => new BookRequestFields(fields.Item1, fields.Item2, fields.Item3, fields.Item4))
        .MapError(errors => errors.ToErrorResponse(uriHelper));
```

## Rules for LLMs / Agents

- Never inspect a `Result<T, TError>`'s `Value`/`Error` directly to decide control flow — always compose through `Map`/`Bind`/`And`/`MapError`.
- Use `Map` when transforming a success value with a function that cannot itself fail.
- Use `Bind` when chaining a step that itself returns a `Result` (railway-oriented composition), so failure short-circuits automatically.
- Use `And` to combine multiple independent validations into one tuple result, accumulating **all** errors — not just the first one encountered — so callers see every invalid field at once.
- Use `MapError` at the boundary (e.g. an endpoint handler) to translate an internal error representation into the response shape, keeping the pipeline itself response-agnostic.
- Represent every expected failure (missing related entity, invalid format, duplicate key) as a `Result` failure; do not throw exceptions for these cases.
- Do not leave scattered `if (result.IsSuccess) ... else ...` guards mid-method once a pipeline is available — fold the branch into the pipeline's error channel instead.

## When NOT to apply

A single, non-composed fallible call with no downstream chaining does not need the full combinator pipeline — a direct `IsSuccess` check is fine when there is nothing to compose with.
