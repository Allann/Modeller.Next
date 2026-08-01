---
title: "Validate Records at Construction With a Private Constructor and Nullable Factory"
---

# Validate Records at Construction With a Private Constructor and Nullable Factory


## The Standard

For a `record` whose fields have validity rules (required, non-empty, well-formed), do not use the positional primary constructor as the public construction path. Give the record an `internal`/`private` constructor, expose `get`-only properties, and add a static extension factory (`CreateNew`, `Restore`) that runs validation and returns a nullable record (`Person?`) — `null` on invalid input, a fully-valid instance otherwise. This makes it structurally impossible to end up holding an invalid instance.

## Why

The "before" version, `public record class Person(Guid PublicId, string FirstName, string LastName);`, looks safe but isn't: the public positional constructor lets any caller do `new Person(Guid.Empty, "", null!)` with no validation at all — invalid `Person` instances are fully constructible. The "after" version moves the constructor to `internal` (only reachable from the factory) and exposes construction only via `Person.CreateNew(...)` / `Person.Restore(...)`, both of which funnel through a private `CreateValid` that returns `null` when `firstName` is blank, and trims both names on the way in. Because there is no other way to obtain a `Person`, once you have one, its invariants (non-blank first name, trimmed names) are guaranteed to hold — validation happens exactly once, at the single choke point, rather than being re-checked (or forgotten) at every call site that touches a `Person`.

## Before (Anti-pattern)

```csharp
public record class Person(Guid PublicId, string FirstName, string LastName);

// Nothing stops this:
var invalid = new Person(Guid.Empty, "", null!);
```

## After (Standard)

```csharp
public record class Person
{
    internal Person(Guid publicId, string firstName, string lastName) =>
        (PublicId, FirstName, LastName) = (publicId, firstName, lastName);

    public Guid PublicId { get; }
    public string FirstName { get; }
    public string LastName { get; }
}

public static class PersonConstruction
{
    extension(Person)
    {
        public static Person? CreateNew(string firstName, string lastName) =>
            Person.CreateValid(Guid.NewGuid(), firstName, lastName);

        private static Person? CreateValid(Guid publicId, string firstName, string lastName) =>
            string.IsNullOrWhiteSpace(firstName) ? null
            : new(publicId, firstName.Trim(), lastName?.Trim() ?? string.Empty);
    }
}
```

## Rules for LLMs / Agents

- For any record with validity rules on its data, make the primary/positional constructor `internal` or `private` — never leave it publicly callable with unvalidated arguments.
- Expose construction only through static factory methods (`CreateNew` for brand-new instances, `Restore` for reconstructing from a trusted source like a database) that funnel through one shared private validation method.
- Return a nullable record (`Person?`) from the factory rather than throwing, when "invalid input" is an expected, recoverable outcome the caller must handle (e.g., user input); throw only when invalid input represents a programming error that should never occur (e.g., restoring corrupt persisted data).
- Normalize input (trim whitespace, apply casing rules) inside the single validation choke point, not at each call site that constructs or receives the type.
- Never re-validate a `Person` (or similarly-guarded record) elsewhere in the codebase on the assumption it might be invalid — if construction is properly gated, an existing instance is guaranteed valid.

## When NOT to apply

If invalid states are impossible to represent at all (e.g., every field is itself a value type that already enforces its own invariants, so no combination can be invalid), a plain positional record without a custom constructor is simpler and this extra factory machinery is unnecessary.
