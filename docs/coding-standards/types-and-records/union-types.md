---
title: "Choose the Right Sum-Type Representation for Closed Sets of Variants"
---

# Choose the Right Sum-Type Representation for Closed Sets of Variants


## The Standard

When a value can only ever be one of a small, closed set of unrelated shapes, model it as a nominal sum type (an `abstract record` hierarchy, or the native C# `union` type where available) rather than as a bag of nullable fields, a base class with an `object`/`enum`-tag field, or an untyped container. Prefer whichever representation gives the compiler the ability to catch a missed case at compile time (exhaustiveness), and reserve fully generic/anonymous containers (`OneOf<T1,T2,T3>`, `object`-boxing "Union" wrappers) for cases where the variant types are unrelated primitives with no shared vocabulary and a nominal type is not justified.

## Why

This material walks through four different techniques for representing "this value is exactly one of these N shapes" side by side: (1) an anonymous/generic `OneOf<T1,T2,T3>`-style wrapper using implicit conversions and a `Match` method, (2) a manual `readonly record struct Union(object Value)` "union principle" wrapper relying on runtime `is` pattern matching with a `throw` for the impossible default case, (3) a classic nominal `abstract record` hierarchy with sealed derived records, and (4) the native C# `union` keyword (`union Animal(Cat, Dog, Bird)`) demonstrated as a preview feature. Every technique produces the same demo output, but they differ in how much the compiler can verify for you: the OneOf/object-wrapper approaches always need a `_ => throw new Exception(...)` fallback arm because the compiler cannot prove the switch is exhaustive, whereas the nominal `abstract record` hierarchy and the native `union` type let the compiler flag a missing case (the demo even calls out a "false positive" comment on the `union`-based switch, showing the compiler's exhaustiveness checking is aware of the closed set). The lesson is to pick the representation that maximizes compile-time safety for the actual shape of the domain concept, rather than defaulting to a generic wrapper out of habit.

## Before (Anti-pattern)

```csharp
// Generic wrapper: works, but the compiler can't prove exhaustiveness,
// so every consumer needs an "impossible" default arm.
class OneOf<T1, T2, T3>
{
    private readonly object _value;
    public TResult Match<TResult>(Func<T1, TResult> f1, Func<T2, TResult> f2, Func<T3, TResult> f3) =>
        _value switch
        {
            T1 v1 => f1(v1),
            T2 v2 => f2(v2),
            T3 v3 => f3(v3),
            _ => throw new InvalidOperationException("Unexpected type")   // unreachable, but required
        };
}
```

## After (Standard)

```csharp
// Nominal sum type: closed set, compiler-checked exhaustiveness, no defensive default arm needed.
abstract record Animal
{
    public string Label => this switch
    {
        Cat c => c.Name,
        Dog d => d.Name,
        Bird b => b.Species,
    };
}

sealed record Cat(string Name) : Animal;
sealed record Dog(string Name) : Animal;
sealed record Bird(string Species) : Animal;
```

## Rules for LLMs / Agents

- For a closed set of domain variants with distinct shapes/payloads, default to an `abstract record` base with `sealed record` derived types, switched over with a `switch` expression.
- Do not introduce a generic `OneOf<T1,T2,...>`/object-boxing wrapper type for a domain concept that has a name in the ubiquitous language — give it a real nominal type instead.
- Only reach for a generic `OneOf<...>`-style wrapper when combining truly unrelated, pre-existing types (e.g. primitives or third-party types you don't own) where introducing a wrapper record for each case is not justified.
- When a `switch` over a closed set of variants must include a `_ => throw ...` default arm purely because the compiler can't prove exhaustiveness (as with `object`-boxing wrappers), treat that as a sign the type should be redesigned as a nominal hierarchy or `union` type instead of accepting the throw as normal.
- If the C# version in use in this codebase supports the native `union` keyword, treat it as equivalent to an `abstract record` hierarchy for in-memory-only values and prefer it for its conciseness; but see `union-types-ef-core.md` when the value must be persisted.

## When NOT to apply

None observed — the material's own conclusion is situational (nominal type for domain concepts, generic wrapper only for ad hoc combinations of foreign types), and that situational guidance is captured above rather than being an exception to it.
