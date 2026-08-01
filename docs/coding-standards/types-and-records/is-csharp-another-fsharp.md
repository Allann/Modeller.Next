---
title: "Approximate F# Discriminated Unions and Pattern Matching with Sealed Records and C# 14 Extension Members"
---

# Approximate F# Discriminated Unions and Pattern Matching with Sealed Records and C# 14 Extension Members


## The Standard

When porting or matching an F# domain model that leans on discriminated unions and pipeline-style pattern matching, model the union as an `abstract record` base type with `sealed record` variants, express logic through property-pattern `switch` expressions with collection-expression results instead of `if`/null chains, and use C# 14 extension member blocks so computed views read like natural member access rather than static helper calls.

## Why

F#'s `ContactInfo = Billing of Address | Shipping of Address | Mailing of Address` and its `List.tryPick`/pipeline style have direct, idiomatic C# analogues once modern language features are used together: a sealed record hierarchy is a closed union the compiler can help match exhaustively; `OfType<T>().FirstOrDefault()` substitutes for `List.tryPick`; property patterns plus `[]`-collection-expression arms substitute for F#'s match-and-build pipeline; and C# 14 extension members let free functions attach to a type as computed properties, closing the ergonomic gap with F#'s member-like pipeline syntax — without introducing null-check ceremony.

## Before (Anti-pattern)

```csharp
// Manual, ceremony-heavy translation: static helper methods, explicit null checks,
// imperative list building instead of pattern matching
static class CompanyHelpers
{
    public static Address? GetBillingAddress(Company company)
    {
        foreach (var contact in company.Contacts)
            if (contact is Billing billing) return billing.Address;
        return null;
    }

    public static string[] GetLabel(Company company)
    {
        var address = GetBillingAddress(company);
        if (address == null) return Array.Empty<string>();
        var lines = new List<string>();
        lines.Add(address.State);
        lines.Add($"{address.ZipCode} {address.City}");
        lines.Add(address.Street);
        return lines.ToArray();
    }
}
```

## After (Standard)

```csharp
public abstract record ContactInfo;
public sealed record Billing(Address Address) : ContactInfo;
public sealed record Shipping(Address Address) : ContactInfo;
public sealed record Mailing(Address Address) : ContactInfo;

public record Company(ImmutableList<ContactInfo> Contacts);

public static class CompanyLabelPrinting
{
    extension(Company company)
    {
        public Address? BillingAddress =>
            company.Contacts.OfType<Billing>().FirstOrDefault()?.Address;

        public string[] Label => company.BillingAddress switch
        {
            null => [],
            { State: var state, City: var city, ZipCode: var zip, Street: var street } =>
            [
                state,
                $"{zip} {city}",
                street
            ]
        };
    }
}
```

## Rules for LLMs / Agents

- Model a closed set of variants as an `abstract record` base with `sealed record` subtypes, not a base class with a discriminator field or an enum-plus-nullable-fields bag.
- Replace `foreach`-with-`is`-check searches over a heterogeneous collection with `OfType<TVariant>().FirstOrDefault()` (or `SingleOrDefault`) when looking for one specific variant.
- Replace `if (x == null) return default; ... build result ...` chains with a `switch` expression using property patterns, ending each arm in a collection expression (`[]`, `[a, b, c]`) rather than manually constructed arrays/lists.
- Use C# 14 extension member blocks (`extension(Type x) { ... }`) to add computed, read-only views (`BillingAddress`, `Label`) that read as natural member access, instead of static helper classes with `GetX(instance)` methods.
- Favor expression-bodied members and `?.` chaining over explicit null-check statements when deriving one value from another.

## When NOT to apply

On C# language versions before 14 (or earlier .NET TFMs), extension member blocks are unavailable — fall back to conventional `static` extension methods. This is a translation/approximation guide, not a replacement for reaching for F# itself when a project's domain logic is dominated by union types and pattern matching.
