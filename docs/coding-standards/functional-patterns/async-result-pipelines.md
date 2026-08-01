---
title: "Chain Async Operations Through Result, Don't Unwrap-and-Branch"
---

# Chain Async Operations Through Result, Don't Unwrap-and-Branch


## The Standard

When a sequence of validation/creation steps can each fail, express it as a single expression chaining `Result<T, TError>`/`Task<Result<T, TError>>` through `Bind`/`Map`/`MapError`/`Match` (and their `*Async` counterparts), rather than `await`-ing each step, manually checking `if (!result.IsSuccess) return ...;`, and threading `.Value` through local variables by hand.

## Why

The initial `PostBook` handler awaits each step, then manually short-circuits with `if (!request.IsSuccess) return Results.BadRequest(request.Error);`, repeating this check-and-early-return for every fallible step and manually pulling fields out of `.Value` tuples. This is easy to get wrong (forgetting a check silently continues with an invalid value) and buries the actual business steps in control-flow boilerplate. The final version defines `AsyncFunctionalResult` — `MapAsync`/`BindAsync`/`MapErrorAsync`/`MatchAsync` overloads covering every combination of `Task<Result<...>>` and sync/async continuation — so the whole pipeline reads as a linear list of steps (`ValidateBookRequestFields(...).BindAsync(...).BindAsync(...).MapAsync(...).MapAsync(...).MatchAsync(...)`), each annotated with its intermediate `Result<T, TError>` shape in a comment, and failure short-circuiting is handled once, inside the combinators, instead of at every call site.

## Before (Anti-pattern)

```csharp
Result<(BookRequestFields fields, BookTitle title), ValidationErrorResponse> request =
    (await ValidateBookRequestFields(dbContext, uriHelper, book))
        .Bind(fields => fields.WithBookTitle(uriHelper, book));

if (!request.IsSuccess) return Results.BadRequest(request.Error);   // manual short-circuit #1

Result<string, string> handle = await dbContext.Books.TryGetUniqueHandle(titleToSlug, request.Value.title, book.Handle);
if (!handle.IsSuccess) return Results.BadRequest(                   // manual short-circuit #2
    NewBookErrors(uriHelper).AddFieldError(nameof(book.Handle), handle.Error));

// ... business logic continues, unwrapping .Value by hand ...
```

## After (Standard)

```csharp
public static async Task<IResult> PostBook(
    BookstoreDbContext dbContext, UriHelper uriHelper, BookTitleToSlug titleToSlug,
    ITitleValidity titleValidityRule, [FromBody] PostBookRequest book) =>
    await ValidateBookRequestFields(dbContext, uriHelper, book)         // Result<BookRequestFields, ValidationErrorResponse>
        .BindAsync(fields => fields.WithBookTitle(uriHelper, book))     // Result<(fields, title), ValidationErrorResponse>
        .BindAsync(tuple => tuple.WithHandle(dbContext, uriHelper, titleToSlug, book))
        .MapAsync(tuple => tuple.CreateBook(book))                      // Task<Result<Book, ValidationErrorResponse>>
        .MapAsync(book => book.SaveEntity(dbContext))
        .MatchAsync(                                                    // Task<IResult>
            book => book.ToCreatedResponse(uriHelper),
            error => Results.BadRequest(error));
```

## Rules for LLMs / Agents

- For any sequence of steps that can each fail, model it with `Result<T, TError>` and chain with `Bind`/`Map`/`MapError`, never with `if (!result.IsSuccess) return ...` sprinkled between manual `await`s.
- When any step in the chain is asynchronous, use the `*Async` combinators (`BindAsync`, `MapAsync`, `MapErrorAsync`, `MatchAsync`) that operate directly on `Task<Result<T, TError>>` so the whole pipeline stays one composed expression ending in a single `await`.
- Terminate the pipeline with `Match`/`MatchAsync` to convert the final `Result` into the caller-facing type (e.g. `IResult` for a minimal API endpoint) in exactly one place, rather than returning early from multiple branches.
- Extract each step as a small named extension method (`WithBookTitle`, `WithHandle`, `CreateBook`, `SaveEntity`) so the pipeline reads as a sequence of named business steps, and annotate the intermediate `Result<...>` shape in a comment when it isn't obvious.
- Never call `.Value` on a `Result` without first establishing (via `Bind`/`Map`/pattern matching) that it is successful — accessing `.Value` directly on an unchecked `Result` reintroduces the bug class this pattern exists to prevent.

## When NOT to apply

A single fallible step with no further chaining (e.g. one validation check before a simple return) does not need the full monadic pipeline — a plain `if`/early-return is clearer. Reserve `Bind`/`Map` chaining for multi-step sequences where the boilerplate of manual checks would otherwise dominate the method.
