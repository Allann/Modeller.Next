---
title: "Eliminate Data Clumps with Value Objects"
---

# Eliminate Data Clumps with Value Objects


## The Standard

When two or more primitive fields or parameters always travel together and only make sense in combination, extract them into a dedicated value object (with a validating constructor or named factory methods) instead of passing them around as separate primitives. Consumers must hold and pass the value object as a single unit, not its individual fields.

## Why

In the "01 Initial" version, `Book` took a flat clump of primitives — `DateOnly? publicationDate, bool isDaySpecified, bool isMonthSpecified, bool isPublished` for "when/whether published" and `int? editionNumber, YearSeason? editionSeason, int? seasonalEditionYear` for "which edition" — plus a wall of `if` checks in the constructor enforcing the valid combinations (e.g. "day specified requires month specified", "ordinal and seasonal edition must not both be set"). Because the clump could be assembled incorrectly at any call site, `BookServices` had to compensate with 13 differently-named factory methods (`CreateUnpublishedOrdinalEdition`, `CreatePlannedSeasonalEdition`, etc.) just to funnel valid combinations into one 10-parameter private `CreateBook`. In "02 Final", the clumps became `PublicationInfo`/`PartialDate` and `Edition` value objects that validate their own invariants in private constructors exposed only through named factories (`CreatePublished`, `CreatePlanned`, `CreateUnpublished`, `CreateOrdinal`, `CreateSeasonal`). `Book`'s constructor and `BookServices.CreateBook` collapsed down to one method taking a `PublicationInfo` and an `Edition`, because it is now impossible to construct an invalid combination — the type itself guarantees correctness, and the API surface shrank from 13 overloads to 1.

## Before (Anti-pattern)

```csharp
public class Book
{
    public DateOnly? PublicationDate { get; private set; }
    public bool IsDaySpecified { get; private set; }
    public bool IsMonthSpecified { get; private set; }
    public bool IsPublished { get; private set; }
    public int? EditionNumber { get; private set; }
    public YearSeason? EditionSeason { get; private set; }
    public int? SeasonalEditionYear { get; private set; }

    public Book(/* ...other args... */,
                DateOnly? publicationDate, bool isDaySpecified, bool isMonthSpecified, bool isPublished,
                int? editionNumber, YearSeason? editionSeason, int? seasonalEditionYear,
                IEnumerable<BookAuthor> authors)
    {
        if (isPublished && publicationDate is null)
            throw new ArgumentException("Publication date must be specified for published books.");
        if (editionNumber is not null && editionSeason is not null)
            throw new ArgumentException("Ordinal and seasonal edition must not be specified together");
        // ...six more cross-field validation checks...
    }
}

// Callers need one overload per valid combination:
public Task<Book> CreatePublishedOrdinalEdition(string title, string cultureName,
    DateOnly publishedDate, int editionNumber, params Author[] authors) => ...
```

## After (Standard)

```csharp
public class PublicationInfo
{
    public PartialDate? PublicationDate { get; private set; }
    public bool IsPublished { get; private set; }

    public static PublicationInfo CreatePublished(PartialDate date) => new(date, true);
    public static PublicationInfo CreatePlanned(PartialDate date) => new(date, false);
    public static PublicationInfo CreateUnpublished() => new(null, false);

    private PublicationInfo(PartialDate? publicationDate, bool isPublished) =>
        (PublicationDate, IsPublished) = (publicationDate, isPublished);
}

public class Edition
{
    public int? Number { get; private set; }
    public YearSeason? Season { get; private set; }
    public int? Year { get; private set; }

    public static Edition CreateOrdinal(int number) =>
        new(number > 0 ? number : throw new ArgumentOutOfRangeException(nameof(number)), null, null);

    public static Edition CreateSeasonal(YearSeason season, int year) =>
        new(null, season, year > 0 ? year : throw new ArgumentOutOfRangeException(nameof(year)));
}

// One constructor / one factory method replaces 13 overloads:
public async Task<Book> CreateBook(string title, string cultureName,
    PublicationInfo publication, Edition edition, IEnumerable<Author> authors) => ...
```

## Rules for LLMs / Agents

- Before adding a new parameter to a method, check whether it is always passed alongside one or more existing parameters; if so, treat that group as a data clump candidate.
- When a constructor or method needs several `if` checks that validate combinations *across* multiple parameters (not just each parameter individually), extract those parameters into a value object whose constructor/factories enforce the invariant instead.
- Model mutually-exclusive or "must appear together" primitive groups (e.g. an optional date with "is day/month specified" flags, or an ordinal-vs-seasonal edition) as a value object with named static factory methods (e.g. `CreateOrdinal`, `CreateSeasonal`), not as a shared set of nullable primitive fields.
- Keep value object constructors `private`; expose creation only through static factory methods that make illegal states unrepresentable.
- Do not create a family of differently-named overloads to cover valid combinations of a data clump — extracting the value object should collapse the overloads back down to a single method.
- Once a value object exists, pass and store it as a whole throughout the call chain (entities, services, DTOs) rather than re-destructuring it back into individual primitives at each layer.

## When NOT to apply

Do not extract a value object for two or three primitives that happen to appear in the same parameter list but are logically independent and vary separately (e.g. a `pageSize` and an unrelated `isAscending` flag) — the standard applies specifically when the primitives are validated together, always change together, and represent one cohesive domain concept.
