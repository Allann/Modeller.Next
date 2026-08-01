---
title: "The Option Type Instead of Nullable Reference Chains"
---

# The Option Type Instead of Nullable Reference Chains


## The Standard

Model the possible absence of a value with an explicit `Option<T>` (or `ValueOption<T>` for value types) monad exposing `Map`, `Bind`, `Filter`, `Match`, `ValueOr`/`ValueOrThrow`, rather than chaining nullable reference types (`T?`) through null checks, `?.`, `??`, and null-forgiving (`!`) operators.

## Why

The "Nullable" reference sample threads `null` through every layer: `GetResearchStages` returns `List<string>?`, `AnalyzeExperimentTrend` returns `ExperimentTrend?`, and the caller re-checks each intermediate result with `if (x == null) return null;` guards and null-forgiving operators (`r!.ConfidenceScore`). Every method that consumes a nullable has to remember to re-check it, and forgetting one check compiles fine and fails at runtime (a `NullReferenceException`). The "OptionMonad" sample replaces every optional field/return with `Option<T>`, whose `IsSome` state is the only way to have a value, and whose `Map`/`Bind`/`Filter`/`Match` methods make "what happens if this is absent" an explicit, compiler-checked branch at every step instead of an implicit possibility. The call site is also forced to handle both cases via `Match(some:, none:)` — there is no way to accidentally dereference an absent value.

## Before (Anti-pattern)

```csharp
public ExperimentTrend? AnalyzeExperimentTrend(ResearchProject project)
{
    if (project.ExperimentalPhase == null || project.ExperimentalPhase.HistoricalExperiments.Count == 0)
        return null;

    var validResults = project.ExperimentalPhase.HistoricalExperiments
        .Select(exp => exp.Result)
        .Where(result => result != null)
        .ToList();

    if (validResults.Count == 0)
        return null;

    return new ExperimentTrend
    {
        AverageConfidence = validResults.Average(r => r!.ConfidenceScore),
        TrendDirection = validResults.Last()!.ConfidenceScore > validResults.First()!.ConfidenceScore
            ? TrendDirection.Improving : TrendDirection.Declining
    };
}
```

## After (Standard)

```csharp
public Option<ExperimentTrend> AnalyzeExperimentTrend(ResearchProject project)
{
    return project.ExperimentalPhase
        .Filter(phase => phase.HistoricalExperiments.Count > 0)
        .Bind(phase =>
        {
            var validResults = phase.HistoricalExperiments
                .Select(e => e.Result)
                .Where(r => r.IsSome)
                .Select(r => r.ValueOrThrow())
                .ToList();

            return validResults.Count > 0
                ? Option<ExperimentTrend>.Some(new ExperimentTrend { /* ... */ })
                : Option<ExperimentTrend>.None();
        });
}

// Caller must handle both branches explicitly:
report.Match(
    some: r => Console.WriteLine(r.ProjectName),
    none: () => Console.WriteLine("Unable to generate report."));
```

## Rules for LLMs / Agents

- Represent "may not have a value" with `Option<T>` (reference types) / `ValueOption<T>` (value types), not `T?` plus manual null checks, when the absence is a meaningful domain state (not just "not yet initialized").
- Chain optional-value transformations with `Map`/`Bind`/`Filter` instead of `?.`/`??`/early-return null guards.
- Consume an `Option<T>` via `Match(some:, none:)` at the point where both branches must produce a result; use `ValueOr`/`ValueOrThrow` only where a plain fallback or an explicit throw is truly the intended behavior.
- Never use the null-forgiving operator (`!`) to work around a nullable that an `Option<T>` would make explicit.
- Do not mix the two styles in the same type/method: once a member's optionality is modeled with `Option<T>`, keep the whole call chain in that style rather than unwrapping to `T?` partway through.

## When NOT to apply

Use plain nullable reference types (not `Option<T>`) for framework/interop boundaries (EF Core entities, DTOs/JSON contracts, ASP.NET model binding) where the ecosystem expects `T?`/nullability annotations; reserve `Option<T>` for internal domain/business logic where explicit, composable absence-handling adds value.
