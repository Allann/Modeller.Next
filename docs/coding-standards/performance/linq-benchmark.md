---
title: "Benchmark LINQ Against Manual Loops Before Assuming Either Is Faster"
---

# Benchmark LINQ Against Manual Loops Before Assuming Either Is Faster


## The Standard

Do not assume LINQ is "slow" or that a manual loop is "fast" — measure with BenchmarkDotNet against the actual data shape and operation in question before optimizing, and use `Span<T>`/`CollectionsMarshal.AsSpan` for indexed hot-path loops over `List<T>` rather than guessing which style is faster.

## Why

The reference benchmarks compare `List<int>.Max()` and a lazy `IEnumerable<int>.Max()` against manual indexed loops and `Span<T>`-based loops, and compare a `SelectMany().GroupBy().OrderByDescending().Take()` LINQ pipeline against a hand-rolled `Dictionary` + `Array.Sort` alternative for a realistic "top-10 by frequency" operation — always with LINQ as the `[Benchmark(Baseline = true)]` so every alternative is measured relative to it. No results file or numeric conclusion ships with the code; the shape of the two approaches (an allocation-heavy `GroupBy`/`OrderByDescending` chain vs. a single-pass `Dictionary` + `Array.Sort`) is there to be measured, not assumed. Treat any "X is faster" claim as something to verify with a benchmark against your own data volumes, not as a fixed rule to apply blindly.

## Before (Anti-pattern)

```csharp
// Assuming LINQ is too slow and hand-rolling without measuring first
var counts = new Dictionary<int, int>();
foreach (var value in data)
{
    foreach (var block in ToBlocks(value))
        counts[block] = counts.TryGetValue(block, out var c) ? c + 1 : 1;
}
int[] candidates = counts.Keys.ToArray();
Array.Sort(candidates, (a, b) => counts[b].CompareTo(counts[a]));
int[] winners = candidates[..10];
```

## After (Standard)

```csharp
[Benchmark(Baseline = true)]
public int[] Linq() =>
    _data
        .SelectMany(ToBlocks)
        .GroupBy(block => block, (block, items) => (block, count: items.Count()))
        .OrderByDescending(x => x.count)
        .Select(x => x.block)
        .Take(10)
        .ToArray();

[Benchmark]
public int[] ManualDictionary()
{
    var counts = new Dictionary<int, int>();
    foreach (var value in _data)
        foreach (var block in ToBlocks(value))
            counts[block] = counts.TryGetValue(block, out var c) ? c + 1 : 1;

    var candidates = counts.Keys.ToArray();
    Array.Sort(candidates, (a, b) => counts[b].CompareTo(counts[a]));
    return candidates[..10];
}
// Run both under BenchmarkDotNet against realistic data volumes before choosing.
```

## Rules for LLMs / Agents

- Do not rewrite a LINQ pipeline into a manual loop (or vice versa) for "performance" without a BenchmarkDotNet (or equivalent) measurement on representative data sizes.
- When a hot path genuinely needs to avoid LINQ's iterator/allocation overhead over a `List<T>`, prefer an indexed loop or `CollectionsMarshal.AsSpan(list)` over converting to an array first.
- Structure comparative benchmarks with the current/idiomatic approach as `[Benchmark(Baseline = true)]` so alternatives are reported as a relative ratio, not raw numbers alone.
- Do not cite specific speedup percentages in code comments or documentation unless a benchmark result backing that number is checked in alongside it.

## When NOT to apply

For code that is not on a measured hot path, prefer the more readable LINQ pipeline by default — only trade readability for a manual loop where a benchmark shows it matters.
