---
title: "Avoid SelectMany + Per-Element Allocation for Grouping/Counting; Aggregate Into a Dictionary Instead"
---

# Avoid SelectMany + Per-Element Allocation for Grouping/Counting; Aggregate Into a Dictionary Instead


## The Standard

When the end goal of a LINQ pipeline is a count or grouping per key (not the flattened elements themselves), do not flatten via `SelectMany` from a per-element array/selector and then `CountBy`/`GroupBy` the result. Instead, `Aggregate` directly into a `Dictionary<TKey, TCount>`, updating it item-by-item, so no small intermediate array is allocated per source element and no separate flattening pass runs before counting.

## Why

The benchmark in this material compares three implementations of "count words by their capitalized initial": a `SelectMany` pipeline (`words.SelectMany(selector).CountBy(...)`) where the selector allocates a one-element (or empty) `string[]` for every single word just to satisfy `SelectMany`'s signature, versus an `Aggregate`-based pipeline (`words.Aggregate(new Dictionary<string,int>(), counter)`) where the delegate mutates a running dictionary directly with no intermediate collection at all. `Processing.GroupCount` is provided in both flavors precisely to make the comparison explicit, and `Utilities.QuickBenchmark` is used to measure "SelectMany" against "Dictionary" against a "Baseline" (does the match but discards it) — demonstrating that the per-element array allocation and the extra flattening enumeration in the `SelectMany` version is pure overhead when the array was only ever going to be immediately consumed and discarded by `CountBy`.

## Before (Anti-pattern)

```csharp
// Allocates a string[] (or Array.Empty<string>()) for every single word
string[] InitialCapitalLetterSelector(string word) =>
    capitalInitialPattern.Match(word) is Match { Success: true } match ? [match.Value]
    : Array.Empty<string>();

var counts = words
    .SelectMany(InitialCapitalLetterSelector)   // flattens N small arrays into one sequence
    .CountBy(value => value)
    .Select(pair => (pair.Key, pair.Value));
```

## After (Standard)

```csharp
Dictionary<string, int> InitialCapitalLetterCounter(Dictionary<string, int> counts, string word)
{
    if (capitalInitialPattern.Match(word) is Match { Success: true } match)
        counts[match.Value] = counts.GetValueOrDefault(match.Value, 0) + 1;
    return counts;
}

var counts = words
    .Aggregate(new Dictionary<string, int>(), InitialCapitalLetterCounter)
    .Select(pair => (pair.Key, pair.Value));
```

## Rules for LLMs / Agents

- When a LINQ pipeline's only purpose is a per-key count or group (not the individual flattened elements), do not reach for `SelectMany(...).CountBy(...)`/`GroupBy(...)`; fold into a `Dictionary` with `Aggregate` instead.
- Do not write a selector that allocates a new array (`[value]` or `Array.Empty<T>()`) per source element purely to satisfy `SelectMany`'s signature when the immediate next step just flattens and counts/groups it — replace the selector+`SelectMany` pair with a direct accumulator delegate.
- Measure before assuming: when in doubt about whether a LINQ rewrite is worth it, benchmark the `SelectMany`-based version against the `Aggregate`/`Dictionary`-based version on realistic input size (this material benchmarks against a real book's text) rather than guessing.
- Reserve `SelectMany` for cases where the flattened elements themselves are the desired output (e.g., subsequent `.Where`/`.Select`/materialization of individual items), not as a stepping stone to an aggregate count.

## When NOT to apply

If the flattened sequence produced by `SelectMany` is genuinely consumed as a sequence of individual elements afterward (filtered, projected, paged, etc.) rather than immediately collapsed into a count/group, `SelectMany` remains the right, idiomatic tool — do not rewrite it into a hand-rolled `Aggregate` loop just to avoid the pattern by rule of thumb.
