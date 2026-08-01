---
title: "Closed Shapes, No Nulls: F# Discipline in C#"
---

# Closed Shapes, No Nulls: F# Discipline in C#


## The Standard

Model closed sets of alternatives (e.g. "an address is used for billing, shipping, or mailing") as a small sealed type hierarchy or enum matched with an exhaustive `switch` expression, never as a flat class plus `if`/`else` type checks. Model data holders as immutable `record` types with structural equality, and model "may be absent" values with a non-null option/result type rather than a nullable reference that every caller must remember to check. Prefer expression-oriented, pipeline-style composition (`select`/chained method calls, or LINQ) over multi-statement procedural code with intermediate mutable variables.

## Why

The source F# program models `ContactInfo` as a discriminated union (`Billing | Shipping | Mailing` of `Address`) and finds the shipping contact with `List.tryPick (function Shipping addr -> Some addr | _ -> None)`. Three properties fall out of that design for free, and each is something C# code in this repo must deliberately reconstruct: (1) the compiler enforces that every case of `ContactInfo` is handled — adding a fourth case breaks every match until it's updated, so the "billing vs shipping vs mailing" invariant cannot silently rot; (2) `Address`/`Company` are plain immutable records, so two addresses with the same field values are equal without hand-written `Equals`/`GetHashCode`, and nothing can mutate a `Company`'s contacts out from under a caller who's holding a reference; (3) `tryPick` returns `Address option`, not `Address` or `null` — "not found" is a distinct, checked value the caller must unwrap (`Option.map` / `Option.defaultValue`), so a missing shipping address can never manifest as a `NullReferenceException` three calls later. The final pipeline (`company |> getShippingAddress |> Option.map getLabel |> Option.defaultValue []`) reads as a straight-line transformation with no mutable locals and no branching statements, which is easier to reason about and test in isolation than an equivalent loop with early returns. C# does not have discriminated unions, structural equality by default, or a built-in `Option<T>`, so this codebase MUST approximate them explicitly with `record`s, sealed hierarchies with exhaustive `switch` expressions, and nullable reference types (or an explicit option-like type) instead of falling back to enums-plus-if-chains and unchecked nulls.

## Before (Anti-pattern)

```csharp
public enum ContactKind { Billing, Shipping, Mailing }

public class ContactInfo
{
    public ContactKind Kind { get; set; }
    public Address Address { get; set; } // reference equality, mutable
}

public static Address GetShippingAddress(List<ContactInfo> contacts)
{
    foreach (var c in contacts)
    {
        if (c.Kind == ContactKind.Shipping) // no exhaustiveness check
            return c.Address;
    }
    return null; // caller must remember to null-check
}
```

## After (Standard)

```csharp
public sealed record Address(string Street, string City, string State, string Zip);

public abstract record ContactInfo;
public sealed record Billing(Address Address) : ContactInfo;
public sealed record Shipping(Address Address) : ContactInfo;
public sealed record Mailing(Address Address) : ContactInfo;

public static Address? GetShippingAddress(IEnumerable<ContactInfo> contacts) =>
    contacts.OfType<Shipping>().Select(s => s.Address).FirstOrDefault();

public static IReadOnlyList<string> GetCompanyLabel(IReadOnlyList<ContactInfo> contacts) =>
    GetShippingAddress(contacts) switch
    {
        null => [],
        Address a => [a.Street, $"{a.Zip} {a.City}", a.State],
    };
```

## Rules for LLMs / Agents

- For any "one of a fixed set of alternatives" domain concept, model it as a sealed abstract record/class with one derived type per case (or an enum only when no case carries distinct data), and consume it via a `switch` expression that covers every case explicitly (add a `_ => throw new UnreachableException()` default only as a compiler-satisfying guard, never as real handling logic).
- Never branch on a type-code field with `if`/`else if` chains when a pattern-matching `switch` over a closed hierarchy would let the compiler catch missed cases.
- Use `record`/`record struct` for data-holder types so equality is structural and instances are immutable by default; do not add public setters to them.
- Represent "value may be absent" as a nullable reference type (`Address?`) or an explicit option/result type, and require callers to handle the absent case at the point of use (pattern match or `??`), never return a bare `null` from a method whose signature doesn't advertise it.
- Prefer LINQ/method-chain pipelines (`Select`, `Where`, `FirstOrDefault`, `switch` expressions) over multi-statement loops with mutable accumulator variables when the transformation is a straight-line map/filter/reduce.

## When NOT to apply

Enums without associated data (e.g. a simple `Status` with no per-case payload) do not need to become a sealed record hierarchy — a plain `enum` with an exhaustive `switch` is sufficient. Mutable builder-style types used transiently during construction (e.g. EF Core change-tracked entities, object initializers inside a single method) are exempt; the immutability requirement applies to the domain model's public shape, not to every local intermediate.
