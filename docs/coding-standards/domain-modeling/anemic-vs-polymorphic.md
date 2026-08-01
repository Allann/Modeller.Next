---
title: "Push Type-Specific Branching Into Polymorphic Methods, Not Caller-Side Switches"
---

# Push Type-Specific Branching Into Polymorphic Methods, Not Caller-Side Switches


## The Standard

When a domain type has a closed set of variants that each need to answer the same question differently (e.g. "when does this partial date begin?"), model the variants as an `abstract` base class (or interface) with one `abstract`/`virtual` member each variant overrides, and call that member polymorphically from consumers. Do not model the variants as a single flat class with boolean/enum flags (`IsDaySpecified`, `IsMonthSpecified`) that every consumer must re-interpret with its own conditional logic to answer the same question.

## Why

In the "before" state, `PartialDate` was one concrete class holding `Date`, `IsDaySpecified`, and `IsMonthSpecified`. Every place that needed "the earliest date this partial date could represent" (e.g. sorting books by publication date) had to re-derive it inline: `BookServices.GetBooksFromNewest` manually unpacked `rawDate.Date.Year`, checked `IsMonthSpecified`/`IsDaySpecified` to decide whether to default month/day to `1`, built a new `DateOnly`, and collected these into a side list before it could sort — ~15 lines of conditional reconstruction logic duplicated at the one call site that needed it (and ready to be duplicated again at the next). In the "after" state, `PartialDate` became an `abstract class` with a single `abstract DateOnly Beginning { get; }`, and each variant (`FullDate`, `YearMonth`, `Year`) computes its own `Beginning` where it has the necessary specificity. `PublicationInfo` gained a one-line `GetBeginning(DateOnly orElse) => PublicationDate?.Beginning ?? orElse`, and `BookServices.GetBooksFromNewest` collapsed to a single `OrderByDescending(book => book.Publication.GetBeginning(DateOnly.MaxValue))` expression — the video's title ("I Cut My Code in Half After Adding Just One Virtual Method") is a literal description of this diff. The lesson: an anemic model pushes "what does this variant mean" logic out to every caller; a polymorphic model asks each variant to answer for itself, once.

## Before (Anti-pattern)

```csharp
public class PartialDate
{
    public DateOnly Date { get; private set; }
    public bool IsDaySpecified { get; private set; }
    public bool IsMonthSpecified { get; private set; }
    // ... factory methods only
}

// Caller has to re-derive "beginning of this partial date" from the flags every time:
DateOnly publicationDate = DateOnly.MaxValue;
if (book.Publication.PublicationDate is PartialDate rawDate)
{
    int year = rawDate.Date.Year;
    int month = rawDate.IsMonthSpecified ? rawDate.Date.Month : 1;
    int day = rawDate.IsDaySpecified ? rawDate.Date.Day : 1;
    publicationDate = new(year, month, day);
}
```

## After (Standard)

```csharp
public abstract class PartialDate
{
    public abstract DateOnly Beginning { get; }
}

public class FullDate(DateOnly date) : PartialDate
{
    public override DateOnly Beginning => date;
}

public class YearMonth(int year, int month) : PartialDate
{
    private DateOnly Date { get; } = new(year, month, 1);
    public override DateOnly Beginning => Date;
}

public class Year(int yearNumber) : PartialDate
{
    public override DateOnly Beginning => new(yearNumber, 1, 1);
}

// Caller no longer needs to know how each variant computes its beginning:
public DateOnly GetBeginning(DateOnly orElse) => PublicationDate?.Beginning ?? orElse;

books.OrderByDescending(book => book.Publication.GetBeginning(DateOnly.MaxValue));
```

## Rules for LLMs / Agents

- When a domain type carries boolean/enum "which fields are meaningful" flags (`IsXSpecified`, a `Kind`/`Type` enum) alongside raw data, and callers branch on those flags to derive a value, stop and consider modeling the type as a variant hierarchy instead, with the derivation as a virtual/abstract member.
- Do not duplicate the same flag-interpreting conditional logic (or copy-paste it) at a second call site — that duplication is the signal to move the logic into the type via polymorphism.
- Name the polymorphic member after the question it answers (`Beginning`, not `GetValue` or `Compute`), so the call site reads as a direct question to the object, not a generic computation.
- After introducing the virtual member, remove the now-redundant flags/raw-data reconstruction logic from every caller — the point is deletion of caller-side branching, not addition of a parallel path.
- Keep the base type `abstract` (not a concrete class with optional overrides) whenever every variant must supply its own answer — an abstract member forces every new variant to implement it, whereas a virtual member with a default silently lets a new variant fall back to a possibly-wrong default.

## When NOT to apply

If only one call site ever needs the derived value, and there is no realistic prospect of a second variant or second consumer, introducing a full type hierarchy for a single conditional may be premature — a straightforward inline computation can stay as-is until duplication or a second variant actually appears.
