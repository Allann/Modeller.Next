---
title: "Use Keyed DI Services for Compile-Time Implementation Choice, Composition for Runtime Choice"
---

# Use Keyed DI Services for Compile-Time Implementation Choice, Composition for Runtime Choice


## The Standard

Use ASP.NET Core keyed services (`AddKeyedSingleton`/`AddKeyedScoped`/`AddKeyedTransient` with `[FromKeyedServices(key)]`) when several implementations of the same interface coexist in the container and the choice of which one a given consumer receives is a static, startup-time decision. Use a decorator/composite pattern (composing implementations together at startup into one registered instance) instead when behavior must be assembled from multiple parts but there is no runtime lookup by key.

## Why

The reference codebase uses both, deliberately, for different problems: `IBibliographicEntryFormatter`/`IAuthorListFormatter` have multiple citation-formatting strategies, and *which one* a given page uses is a fixed, per-feature decision — a natural fit for keyed services, so each page simply asks for its named formatter. `IDiscount`, by contrast, is composed once at startup (`RelativeDiscountCap` wrapping `NoZeroDiscounts` wrapping a `ParallelDiscounts` array) and registered as a single plain `AddSingleton<IDiscount>()`, because discount rules combine rather than get selected among by a caller. Reaching for keyed services where composition is the real need (or vice versa) adds either an unnecessary runtime lookup or an inflexible hard-coded pipeline.

## Before (Anti-pattern)

```csharp
// Runtime "if/switch on a string" instead of using the DI container's own keying
IBibliographicEntryFormatter GetFormatter(string feature) => feature switch
{
    "BookList" => new CsvFullNamesThenTitleFormatter(),
    "RecommendedBooks" => new AcademicFormatter(),
    _ => throw new ArgumentException(feature)
};
```

## After (Standard)

```csharp
// Program.cs — register named/typed keys
builder.Services.AddKeyedSingleton(FeatureKeys.BookList, (_, _) => csvFullNamesThenTitle);
builder.Services.AddKeyedSingleton(FeatureKeys.RecommendedBooks, (_, _) => academicFormatter);
builder.Services.AddKeyedSingleton(typeof(BooksModel), (_, _) => csvFullNamesOnlyFormatter);
builder.Services.AddKeyedSingleton(typeof(BooksModel), (_, _) => titleOnlyFormatter);

// BookDetails.cshtml.cs — resolve a single keyed instance
public BookDetailsModel(
    IAuthorNameFormatter authorNameFormatter,
    [FromKeyedServices(FeatureKeys.RecommendedBooks)] IBibliographicEntryFormatter recommendedBooksFormatter) { }

// Books.cshtml.cs — resolve every registration under one key as IEnumerable<T>
public BooksModel(
    [FromKeyedServices(typeof(BooksModel))] IEnumerable<IBibliographicEntryFormatter> bookFormatters) { }
```

## Rules for LLMs / Agents

- Reach for keyed services when several implementations of the same interface must coexist in the container and a specific consumer/feature needs a specific one, chosen at startup — not when the choice depends on runtime data.
- Reach for a decorator/composite pattern (composed once in `Program.cs`/DI setup) when behavior must combine multiple implementations, or when the selection genuinely depends on runtime data rather than the identity of the consumer.
- Prefer a dedicated `enum FeatureKeys` as the key for cross-cutting/product concepts; use `typeof(ConsumerType)` as the key when the registration really means "the set of implementations this consumer needs."
- To resolve multiple implementations registered under one key, register the key multiple times and inject `IEnumerable<T>` with `[FromKeyedServices(key)]` on the enumerable parameter.
- Do not replace a `switch`/dictionary-based factory that already reads a runtime value with keyed services just for its own sake — keyed services resolve by a compile-time-known key, not by arbitrary runtime data.

## When NOT to apply

Do not use keyed services when the selection must vary per-request based on data (e.g., which discount rule applies to which order) — use composition or an explicit strategy/factory evaluated against that data instead.
