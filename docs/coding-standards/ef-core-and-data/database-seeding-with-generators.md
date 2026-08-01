---
title: "Seed Large Volumes of Test Data With Deterministic, Lazy Generators"
---

# Seed Large Volumes of Test Data With Deterministic, Lazy Generators


## The Standard

Build test/seed data generators as infinite, lazy `IEnumerable<T>` sequences (`yield return` in a `while (true)` loop) driven by a `Random` with a fixed seed, and compose them (names, addresses, companies) through constructor injection and LINQ rather than pre-materializing large in-memory lists. Callers pull exactly as many items as they need (`.Take(n)`) or consume the sequence directly in a streaming loop.

## Why

Seeding "millions of rows" is only tractable if generation is streaming and composable: `CompanyNameGenerator.GetCompanyNames()` lazily yields unique combinations of adjective/noun/type without ever allocating the full cross-product, and `CompanyGenerator.Companies(...)` is itself an infinite lazy sequence built by composing an injected `CompanyNameGenerator` and `AddressGenerator`. Using a seeded `Random(42)` makes every run of the CLI reproducible (same "random" data every time), which matters for demos, tests, and support. Because everything is expressed as small, focused generators wired together through constructor parameters, an AI coding assistant (or a human) can extend the data set (e.g. add a new entity generator) by writing one more small, testable generator class and composing it the same way, instead of hand-writing bespoke seed data or giant literal arrays of records.

## Before (Anti-pattern)

```csharp
// Materializing everything up front does not scale and isn't reusable/composable
var companies = new List<Company>();
for (int i = 0; i < 1_000_000; i++)
{
    companies.Add(new Company(Guid.NewGuid(), $"Company {i}", "12345678", new[] { new Address(...) }));
}
dbContext.Companies.AddRange(companies);
```

## After (Standard)

```csharp
public class CompanyNameGenerator
{
    private readonly Random _random;
    public CompanyNameGenerator(int seed = 42) => _random = new Random(seed);

    public IEnumerable<string> GetCompanyNames()
    {
        HashSet<string> generated = new();
        while (generated.Count < MaxCount)
        {
            string value = CreateNext();
            if (generated.Add(value)) yield return value;   // lazy, unique, unbounded
        }
    }
}

public class CompanyGenerator(CompanyNameGenerator companyNameGenerator, AddressGenerator addressGenerator)
{
    public Company Next(int maxAddresses, params Type[] companyTypes) { /* composes the injected generators */ }

    public IEnumerable<Company> Companies(int maxAddresses, params Type[] companyTypes)
    {
        while (true) yield return Next(maxAddresses, companyTypes);   // caller decides how many to pull
    }
}
```

## Rules for LLMs / Agents

- Express generators for seed/test data as `IEnumerable<T>` iterator methods (`yield return`), not as methods that build and return a fully materialized `List<T>`.
- Use a `Random` instance with an explicit, fixed seed for anything that must be reproducible across runs (demos, CLI reset commands, deterministic test fixtures).
- Compose generators through constructor injection (a `CompanyGenerator` takes a `CompanyNameGenerator` and `AddressGenerator`) rather than duplicating name/address logic inline.
- Let the consumer decide volume via `.Take(n)` or a bounded loop; the generator itself should not hardcode a row count.
- Guard against infinite loops in "unique value" generators by bounding the possible combination space (as `GetCompanyNames` does via `maxCount`) so a full space doesn't spin forever once exhausted.

## When NOT to apply

For small, fixed reference/lookup data (a handful of rows that will never change), a plain literal list is simpler and clearer than a generator — reserve the generator pattern for data volumes or variability large enough that hand-authoring every row is impractical.
