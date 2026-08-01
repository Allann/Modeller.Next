---
title: "Build Functional Types from Smart Constructors, Closed Unions, and a Match Extension"
---

# Build Functional Types from Smart Constructors, Closed Unions, and a Match Extension


## The Standard

Model a domain value with: (1) a smart constructor that returns a nullable type (`T?`) on invalid input instead of throwing, (2) a discriminated union expressed as an `abstract record` base with `internal` (not `public`) sealed record variants, so external code can only reach a value through the constructor and can only inspect it through (3) an exhaustive `Match<TResult>` extension method — never through public switch access to the subtype internals. Split supporting code into `Models` (types + constructors + `Match`), `Processes` (delegate types and generic composition/partial-application combinators, no business logic), and `UI`/consumer layers (concrete policies built by composing `Processes` combinators).

## Why

Making the union's variant types `internal` closes off two failure modes at once: callers cannot construct an invalid variant directly (bypassing the smart constructor's validation), and callers cannot pattern-match on the concrete subtype and silently miss a case if a new variant is added later — every consumer is forced through `Match`, which requires a handler for every case at the call site. Separating `Models` (what a value is) from `Processes` (generic composition machinery) from `UI` (concrete formatting policies) keeps business rules, composition mechanics, and presentation choices from bleeding into each other.

## Before (Anti-pattern)

```csharp
// Public subtypes + throwing constructor + ad hoc switch on concrete type
public record NameType;
public record FullNameType(string FirstName, string LastName) : NameType;
public record MononymType(string Name) : NameType;

public static class Name
{
    public static NameType Create(string firstName, string lastName)
    {
        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
            throw new ArgumentException("Invalid name");
        return new FullNameType(firstName, lastName);
    }
}

// Callers can construct FullNameType directly (bypassing Create),
// and a switch here silently does nothing if a new NameType variant is added:
string Format(NameType name) => name switch
{
    FullNameType fn => $"{fn.FirstName} {fn.LastName}",
    MononymType m => m.Name,
    _ => ""
};
```

## After (Standard)

```csharp
// Models/Name.cs
public abstract record NameType;
internal record FullNameType(string FirstName, string LastName) : NameType;
internal record MononymType(string Name) : NameType;

public static class Name
{
    public static NameType? Create(string firstName, string lastName) =>
        string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName) ? null
        : new FullNameType(firstName, lastName);

    public static R Match<R>(this NameType name, Func<string, string, R> fullName, Func<string, R> mononym) =>
        name switch
        {
            FullNameType fn => fullName(fn.FirstName, fn.LastName),
            MononymType mono => mononym(mono.Name),
            _ => throw new InvalidOperationException("Unexpected name type")
        };
}

// UI/NameFormatting.cs — a policy built purely by composing Match
public static NameFormatter FullNameFormatter => name => name.Match(
    (first, last) => $"{first} {last}",
    mononym => mononym);
```

## Rules for LLMs / Agents

- Give every domain value a smart constructor that returns `T?` (or a `Result<T, TError>`) on invalid input — never a public constructor that throws or silently accepts invalid state.
- Mark discriminated-union variant record types `internal` (or otherwise inaccessible outside the defining assembly/module), so the base type's public factory and `Match` are the only ways in and out.
- Provide a `Match<TResult>` extension requiring a handler for every variant; do not expose a public `switch` surface over the concrete subtypes.
- Keep delegate-type definitions and generic composition combinators (partial application, `Apply`) in a separate `Processes`-style layer with no business logic of their own.
- Build concrete formatting/behavior policies (in a `UI`/consumer layer) purely by composing `Processes` combinators over `Match`, not by re-implementing branching logic per policy.
- Thread optionality through `Bind`/`Map`/`Do`/`ForEach`-style extensions over nullable references rather than explicit `if (x != null)` chains.

## When NOT to apply

For simple DTOs with no invariants to protect and no closed set of variants, this level of ceremony (internal variants + Match) is unnecessary — a plain record is sufficient.
