---
title: "Model Discriminated Unions as Sealed Record Hierarchies with Extension Factories"
---

# Model Discriminated Unions as Sealed Record Hierarchies with Extension Factories


## The Standard

When a value can be one of several distinct, mutually exclusive shapes (e.g. an estimate that is either a fixed `Duration` or a variable `Interval` or `Unknown`), model it as an `abstract record` base with `sealed record` variants deriving from it, all defined in a single file, with construction routed through a static factory (using C# 14 extension members on the type, e.g. `extension(Estimate) { public static ... }`) rather than a single class carrying nullable fields for every variant plus a `Type`/`Kind` enum flag.

## Why

The "before" version modeled a work item with a single flat `TimeSpan Estimate` field on `WorkItem` — it could not represent "the estimate is unknown" or "the estimate is a range" without adding nullable fields and ad-hoc sentinel values, and any code consuming `Estimate` had no compiler help enumerating the cases. The "after" version introduces `abstract record Estimate` with `sealed record Duration(TimeSpan Value)`, `sealed record Interval(TimeSpan Start, TimeSpan Span)`, and `sealed record Unknown` — every consumer (`WorkItemEstimation.EstimateCompletion`, the `format`/`sortOrder` functions in `Program.cs`) uses an exhaustive `switch` expression over the sealed hierarchy, so the compiler can flag missing cases if a new variant is added. Construction is centralized in a static `EstimateConstruction` class using C# 14's `extension(Estimate) { ... }` members, which validates arguments (`value < TimeSpan.Zero ? throw ... : new Duration(value)`) so invalid `Duration`/`Interval` instances can't be constructed by calling code.

## Before (Anti-pattern)

```csharp
public record WorkItem(string Name, TimeSpan Estimate, ImmutableList<WorkItem> Prerequisites);
// No way to represent "unknown" or "a range" without nullable/sentinel hacks
```

## After (Standard)

```csharp
public abstract record Estimate;
public sealed record Duration(TimeSpan Value) : Estimate;
public sealed record Interval(TimeSpan Start, TimeSpan Span) : Estimate;
public sealed record Unknown : Estimate;

public static class EstimateConstruction
{
    extension(Estimate)
    {
        public static Estimate CreateDuration(TimeSpan value) =>
            value < TimeSpan.Zero ? throw new ArgumentOutOfRangeException(nameof(value)) : new Duration(value);

        public static Estimate CreateInterval(TimeSpan start, TimeSpan span) =>
            start < TimeSpan.Zero ? throw new ArgumentOutOfRangeException(nameof(start))
            : span < TimeSpan.Zero ? throw new ArgumentOutOfRangeException(nameof(span))
            : new Interval(start, span);
    }
}

// Exhaustive consumption:
string format(Estimate e) => e switch
{
    Duration d => FormatDuration(d),
    Interval i => FormatInterval(i),
    _ => "Unknown"
};
```

## Rules for LLMs / Agents

- Model any "one of several distinct shapes" domain value as an `abstract record` base with `sealed record` variants, never as one record/class with a `Kind`/`Type` enum plus nullable fields for each variant's data.
- Define the entire discriminated union (base + all variants) in a single file.
- Route construction of union variants through a dedicated static factory class/extension members that validate inputs and throw on invalid data, rather than exposing the variant record constructors directly for general use.
- Consume discriminated unions via exhaustive `switch` expressions over the sealed hierarchy so missing-case bugs surface at compile time (via a required `_` default that signals an unhandled case, or a warning if truly exhaustive).
- Prefer this pattern over nullable-field-based "poor man's union" types whenever the set of shapes is closed and known in advance.

## When NOT to apply

If the set of variants is genuinely open-ended/extensible by external code (plugin-style extension), a sealed record hierarchy is the wrong tool — use an interface-based polymorphic design instead.
