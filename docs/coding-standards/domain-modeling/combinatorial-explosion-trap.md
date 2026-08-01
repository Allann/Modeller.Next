---
title: "Avoid the Combinatorial Explosion Trap From Anemic Models"
---

# Avoid the Combinatorial Explosion Trap From Anemic Models


## The Standard

When a creation/behavior method's shape is driven by an anemic model's independent boolean flags and nullable fields (e.g. "is it published/planned/unpublished" x "is it an ordinal or seasonal edition" x "day/month/year precision"), do NOT add one named overload per combination. Fix the underlying model instead — collapse the independent flags into proper types (a discriminated union per concept) so one method works for every case, rather than growing `N x M x K` convenience methods.

## Why

`BookServices` in the initial stage already had one flag-heavy `CreateBook(...)` method taking `bool isDaySpecified, bool isMonthSpecified, bool isPublished` plus nullable ordinal/seasonal edition fields. The "final" stage in this material shows what happens when a team tries to make that friendlier without fixing the model: it adds a named factory per combination — `CreateUnpublishedOrdinalEdition`, `CreatePlannedOrdinalEdition` (x3 date-precision overloads), `CreatePublishedOrdinalEdition` (x3), and the same three again for `...SeasonalEdition` — 12+ methods that all funnel into the same flag-riddled `CreateBook`. Every new publication state or edition kind multiplies the method count instead of adding one case to a union. This is a direct symptom of the anemic model problem (`anemic-domain-model.md`) and the boolean-flags problem (`avoid-boolean-state-flags.md`): the explosion is the visible proof that the underlying types are wrong.

## Before (Anti-pattern)

```csharp
public async Task<Book> CreateUnpublishedOrdinalEdition(string title, string cultureName, int editionNumber, params Author[] authors) =>
    await CreateBook(title, cultureName, null, false, false, false, editionNumber, null, null, authors);

public async Task<Book> CreatePlannedOrdinalEdition(string title, string cultureName, DateOnly plannedDate, int editionNumber, params Author[] authors) =>
    await CreateBook(title, cultureName, plannedDate, true, true, false, editionNumber, null, null, authors);

// ...9 more near-identical overloads, one per (publication state x edition kind x date precision) combination...

public async Task<Book> CreateBook(
    string title, string cultureName,
    DateOnly? publicationDate, bool isDaySpecified, bool isMonthSpecified, bool isPublished,
    int? ordinalNumber, YearSeason? editionSeason, int? editionYear, params Author[] authors) { /* ... */ }
```

## After (Standard)

```csharp
// One union per independent concept — no cross-product of overloads needed.
public abstract record PublicationInfo;
public sealed record Published(PublicationDate On) : PublicationInfo;
public sealed record Planned(PublicationDate For) : PublicationInfo;
public sealed record NotPlannedYet : PublicationInfo;

public interface IEdition;
public sealed record OrdinalEdition(int Number) : IEdition;
public sealed record SeasonalEdition(YearSeason Season, int Year) : IEdition;

// A single creation method now covers every combination.
public async Task<Book> CreateBook(string title, string cultureName, PublicationInfo publication, IEdition edition, params Author[] authors) =>
    /* ... */;
```

## Rules for LLMs / Agents

- If you find yourself about to add a method whose name encodes a combination of independent concerns (`CreatePlannedSeasonalEdition`, `CreatePublishedOrdinalEdition`), stop — that is the combinatorial-explosion smell. Refactor the underlying flags/nullable fields into discriminated unions first.
- One factory/constructor should accept one instance of each union type (`PublicationInfo`, `IEdition`) and work for every case, rather than one method per combination.
- Count the independent axes of variation before adding an overload: if there are 2+ axes with 2+ options each and you're naming a method after a specific combination, that's a signal to introduce types, not more overloads.
- When you see several `bool`/nullable parameters that only make sense together (`isDaySpecified`, `isMonthSpecified`, `isPublished` alongside a nullable `DateOnly?`), treat that as the same underlying problem as `avoid-boolean-state-flags.md` and fix it at the type level.
- Prefer growing behavior by adding a new case to a closed union (compiler forces you to handle it everywhere) over adding a new method name to an ever-growing API surface.

## When NOT to apply

A handful of well-named convenience overloads for genuinely common, stable call patterns (e.g. `TimeSpan.FromSeconds`/`FromMinutes`) is fine when the number of combinations is small and fixed and unlikely to grow. The trap is specifically the *multiplicative* growth from independent concerns that should have been unified into types.
