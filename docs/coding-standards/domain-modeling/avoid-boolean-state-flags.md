---
title: "Replace Boolean Status Flags With Closed Type Hierarchies"
---

# Replace Boolean Status Flags With Closed Type Hierarchies


## The Standard

When a concept has more than two mutually exclusive states, or a boolean would need to be paired with extra nullable fields to make sense (e.g. `IsPublished` + a nullable `PublishedDate`/`PlannedDate`), model it as an abstract record with sealed subtypes (a discriminated union) instead of a `bool` (or several related booleans). Put the state-dependent behavior as an overridden method on each subtype, not as an `if (flag)` branch at every call site.

## Why

A single "published?" boolean cannot represent "planned for a future date" vs "not scheduled at all" vs "published on an approximate date (year-only, year+month, or full date)" without extra nullable companion fields and scattered null-checks. The refactor replaces any such boolean with `PublicationInfo` (`Published` / `Planned` / `NotPlannedYet`), each of which knows how to answer `IsPublishedBefore(date)` and `IsPlannedBefore(date)` on its own. Callers (`CountBook`, LINQ `Where`) never branch on a flag or null-check a date — they call a method and get a correct answer for every case, and the compiler enforces the switch is exhaustive because the hierarchy is sealed.

## Before (Anti-pattern)

```csharp
public class Release
{
    public PublicationDate Date { get; private set; }   // meaning depends on an implicit/missing flag elsewhere
    // somewhere else: bool isPublished; if (isPublished) { ... Date ... } else { ... }
}
```

## After (Standard)

```csharp
public abstract record PublicationInfo
{
    public abstract bool IsPublishedBefore(DateOnly date);
    public abstract bool IsPlannedBefore(DateOnly date);
}

public sealed record Published(PublicationDate PublishedOn) : PublicationInfo
{
    public override bool IsPublishedBefore(DateOnly date) => PublishedOn.EndsBefore(date);
    public override bool IsPlannedBefore(DateOnly date) => PublishedOn.EndsBefore(date);
}

public sealed record Planned(PublicationDate PlannedFor) : PublicationInfo
{
    public override bool IsPublishedBefore(DateOnly date) => false;
    public override bool IsPlannedBefore(DateOnly date) => PlannedFor.EndsBefore(date);
}

public sealed record NotPlannedYet : PublicationInfo
{
    public override bool IsPublishedBefore(DateOnly date) => false;
    public override bool IsPlannedBefore(DateOnly date) => false;
}

// call site: no flags, no null checks
var published = books.Where(book => book.Release.Publication.IsPublishedBefore(yearStart));
```

## Rules for LLMs / Agents

- If a `bool` field/parameter would need a companion nullable field to be meaningful (`bool isX` + `DateTime? xDate`), replace both with a closed hierarchy of records/subtypes, one per real state.
- Make the hierarchy exhaustive and sealed (`abstract record Base` with `sealed record` cases) so the compiler flags missing cases in pattern matches.
- Put state-specific logic as an `abstract`/`override` method on each case, not as `if`/`switch` on a flag scattered across the codebase.
- Never add a second or third boolean to "clarify" an existing boolean's meaning (`isPublished`, `isConfirmedPublished`) — that is the combinatorial-explosion smell; introduce a proper type instead (see `combinatorial-explosion-trap.md`).
- Prefer `date.EndsBefore(other)`-style methods on the value type itself over comparing raw fields inline, so date-shape variants (year-only vs year+month vs full date) each implement comparison correctly once.

## When NOT to apply

A `bool` is fine for a genuinely binary, context-free toggle with no related data (e.g. `IsActive` on a simple feature flag with no accompanying fields). Do not introduce a type hierarchy for trivial two-state switches that never grow extra associated data.
