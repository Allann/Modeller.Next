---
title: "Prefer Closed Records + Functional Map Over OO Interface Hierarchies"
---

# Prefer Closed Records + Functional Map Over OO Interface Hierarchies


## The Standard

When a type has a small, closed set of variants whose only purpose is to carry data and be branched on, do NOT model it as an `interface` with one implementing class per variant and virtual/polymorphic methods. Instead, model it as an `abstract record` with `sealed record` subtypes (a discriminated union), add a single generic `Map<T>(Func<Variant1,T>, Func<Variant2,T>, ...)` extension method that performs the `switch` exhaustiveness check once, and implement all behavior as free functions built on top of `Map` rather than as virtual methods on the type itself.

## Why

In the "01-Initial" version, `IEdition`, `IPartialDate`, and `IPublicationInfo` were classic OO interfaces: each variant (`OrdinalEdition`, `SeasonalEdition`; `FullDate`, `YearMonth`, `Year`; etc.) implemented its own copy of every behavior method (e.g. `AdvanceToNext()`), scattering the logic for one operation across N files and making it easy to forget a case when a new variant is added (no compiler help — you just don't implement the interface member on the new class, and normally you would, but the pattern relies on each class doing its own thing rather than a single switch). It also means each new *operation* is a breaking interface change to every implementer.

In "02-Final", the same concepts became `abstract record` hierarchies of `sealed record`s plus one `Map<T>` extension per type. `Map` is the single place where the closed set is enumerated and the compiler-checked `switch` expression lives (with a defensive `throw` in the `_` arm as a runtime backstop). Every behavior (`AdvanceToNext`, `GetBeginning`, `GetNext`, `GetLastDateOf`) is then a small pure function composed from `Map`, living in a dedicated `*Manipulation`/`*Mapping` static class instead of being smeared across per-variant classes. Adding a new operation is now a single new static method; adding a new variant only requires updating the `Map` switch (and the compiler/tests will catch missed operations because they all funnel through it). This is strictly less code, keeps data (records) separate from behavior (static functions), and uses `Func<TVariant, T>` — i.e. functional interfaces in the classic sense — as the extension point instead of a multi-method OO interface.

## Before (Anti-pattern)

```csharp
public interface IEdition
{
    IEdition AdvanceToNext();
}

public class OrdinalEdition(int number) : IEdition
{
    public int Number { get; private set; } = number;
    public IEdition AdvanceToNext() => new OrdinalEdition(Number + 1);
}

public class SeasonalEdition(YearSeason season, int year) : IEdition
{
    public YearSeason Season { get; private set; } = season;
    public int Year { get; private set; } = year;
    public IEdition AdvanceToNext() =>
        new SeasonalEdition(Season.Next(), Season.IsLast() ? Year + 1 : Year);
}
```

## After (Standard)

```csharp
public abstract record Edition;
public sealed record Ordinal(int Number) : Edition;
public sealed record Seasonal(int Year, YearSeason Season) : Edition;

public static class EditionMapping
{
    public static T Map<T>(this Edition edition,
        Func<Ordinal, T> mapOrdinal, Func<Seasonal, T> mapSeasonal) => edition switch
    {
        Ordinal ordinal => mapOrdinal(ordinal),
        Seasonal seasonal => mapSeasonal(seasonal),
        _ => throw new InvalidOperationException("Unsupported Edition type")
    };
}

public static class EditionManipulation
{
    public static Edition AdvanceToNext(this Edition edition) => edition.Map<Edition>(
        ordinal => new Ordinal(ordinal.Number + 1),
        seasonal => new Seasonal(
            seasonal.Season.IsLast() ? seasonal.Year + 1 : seasonal.Year,
            seasonal.Season.Next()));
}
```

## Rules for LLMs / Agents

- When a domain concept has a fixed, known-at-compile-time set of variants (a "one of these N shapes" type), model it as `abstract record Base` with `sealed record` variants, not as an `interface` implemented by separate classes.
- Give the abstract type exactly one `Map<T>(Func<Variant1,T> ..., Func<VariantN,T> ...)` extension method that contains the sole `switch` expression over the variants, including a `_ => throw new InvalidOperationException(...)` defensive arm.
- Implement every operation on the type as a separate static extension method built by calling `Map`, grouped in a `*Manipulation` or `*Mapping` static class — never as a virtual/abstract method redeclared on each variant.
- Do not put behavior methods (other than the `Map` itself) directly inside the `interface`/`abstract record` declaration; keep data definitions and behavior in separate files/classes.
- Prefer this pattern only when the variant set is genuinely closed (you control all cases and don't expect third parties to add new implementations). If external code needs to add new variants without modifying the base type, a real polymorphic interface is still the correct tool — do not force this pattern there.
- Name variant classes as plain nouns describing the case (e.g. `Ordinal`, `Seasonal`, `Published`, `Planned`, `NotPublished`) rather than suffixing the base type name (`OrdinalEdition` → `Ordinal`), since the base record's namespace/type already disambiguates.

## When NOT to apply

Do not apply this standard when the set of implementations is genuinely open-ended and meant to be extended by other assemblies/plugins/consumers (true polymorphic extensibility) — a conventional interface is correct there, since a closed `Map` switch cannot account for cases it doesn't know about. Also skip it for types that carry meaningful mutable state or identity across their lifetime rather than being immutable value-like data, since records/`Map` assume value semantics.
