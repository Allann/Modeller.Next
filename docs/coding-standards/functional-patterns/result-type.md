---
title: "Use a Result<T, TError> Type for Expected Failures Instead of Nulls, Out Params, and Try/Catch"
---

# Use a `Result<T, TError>` Type for Expected Failures Instead of Nulls, Out Params, and Try/Catch


## The Standard

For operations whose failure is an expected, recoverable outcome (parsing, lookups, cross-field validation), return `Result<T, TError>` — a small success/failure wrapper exposing `IsSuccess`, `Value`, and `Error` — instead of throwing/catching exceptions for control flow, returning `null` and hoping callers check it, or scattering ad-hoc `try`/`catch` blocks around each fallible call. Compose multiple such operations by collecting each `Result`, checking `IsSuccess`, and only proceeding to use `.Value` once all validations have passed.

## Why

The "before" `BooksHandlers.PostBook` wraps culture parsing and `BookTitle` construction each in their own `try { ... } catch { ... }` block, inlines the publisher/author lookup loops directly in the handler, and falls back to `?? throw new InvalidOperationException()` at the point of use to placate the nullable-reference-type checker — even though the preceding validation already proved those values weren't null. The "after" version factors each fallible step into a method or extension returning `Result<T, string>` (`LoadPublisher`, `LoadAuthors`, `"...".TryParseCultureName()`, `BookTitle.TryCreate(...)`), checks `!result.IsSuccess` uniformly to accumulate validation errors, and only reads `.Value` after a single combined `if (validationErrors.ContainsErrors()) return Results.BadRequest(...)` gate — eliminating every `try`/`catch` and every `?? throw new InvalidOperationException()` "this can't actually happen" escape hatch. The exception-based and Result-based versions produce identical HTTP responses; the difference is that failure is now a value the compiler forces you to look at (`Result.Value` throws `InvalidOperationException` if read without checking `IsSuccess` first) rather than a side channel (an exception or a silently-ignorable null) that's easy to forget to handle.

## Before (Anti-pattern)

```csharp
CultureInfo? titleCulture = null;
try { titleCulture = CultureInfo.GetCultureInfo(book.TitleCulture, true); }
catch { validationErrors.AddFieldValidationError(nameof(book.TitleCulture), "Invalid title culture name"); }

BookTitle? title = null;
if (titleCulture != null)
{
    try { title = new(book.Title, titleCulture); }
    catch { validationErrors.AddFieldValidationError(nameof(book.Title), "Invalid title"); }
}
// ...
Book newBook = Book.CreateNew(title ?? throw new InvalidOperationException(), /* ... */);
```

## After (Standard)

```csharp
public static class CultureInfoParsing
{
    public static Result<CultureInfo, string> TryParseCultureName(this string cultureName)
    {
        try { return Result<CultureInfo, string>.Success(CultureInfo.GetCultureInfo(cultureName, true)); }
        catch (CultureNotFoundException) { return Result<CultureInfo, string>.Failure("Invalid culture name"); }
    }
}

Result<CultureInfo, string> titleCulture = book.TitleCulture.TryParseCultureName();
if (!titleCulture.IsSuccess) validationErrors.AddFieldValidationError(nameof(book.TitleCulture), titleCulture.Error);

if (validationErrors.ContainsErrors()) return Results.BadRequest(validationErrors);

Result<BookTitle, string> title = BookTitle.TryCreate(book.Title, titleCulture.Value);
// title.Value is now guaranteed valid — no `?? throw` needed
```

## Rules for LLMs / Agents

- For any operation whose failure is expected and recoverable (parsing, "not found" lookups, business-rule validation), return `Result<T, TError>` rather than throwing, returning `null`, or using an `out bool TryX` pattern.
- Wrap unavoidable exception-throwing APIs (e.g., `CultureInfo.GetCultureInfo`) at the boundary in a single `try`/`catch` that converts the exception into `Result.Failure(...)`; do not let that exception propagate into calling code as control flow.
- Collect every `Result` for a multi-field validation, check all of them, and only branch out (`return Results.BadRequest(...)`) once after accumulating all errors — do not `return` on the first failure if the goal is to report every invalid field at once.
- Never read `.Value` before confirming `.IsSuccess` — treat a `Result` as failed-until-proven-successful; do not use `?? throw new InvalidOperationException()` as a substitute for checking `IsSuccess`.
- Keep `Result<T, TError>.Value`/`.Error` access guarded (throwing `InvalidOperationException` when read in the wrong state) so misuse fails loudly during development instead of silently returning `default`.

## When NOT to apply

Do not use `Result<T, TError>` for truly exceptional, non-recoverable conditions (programming errors, invariant violations, infrastructure failures like a lost DB connection) — those should still throw. Do not introduce `Result` for a single fallible call with no downstream composition; a simple nullable return or `TryParse`-style method is sufficient when there's nothing to accumulate or chain.
