---
title: "Choose class, record, or struct Based on Semantics, Not Habit"
---

# Choose class, record, or struct Based on Semantics, Not Habit


## The Standard

Model small, immutable, value-like domain concepts (money, coordinates, identifiers) as `record` (or `readonly record struct` when they are small and value-typed) so they get free structural equality, non-destructive `with`-mutation, and correct behavior in sets/dictionaries/`Distinct()`. Reserve mutable `class` for types with genuine reference identity or where in-place mutation is intentional, and avoid large `struct`s that make copying expensive.

## Why

The demo shows the same `Money` concept implemented three ways: a mutable `class` (`MoneyA`), a `readonly record struct` (`MoneyB`), and a `record class` (`MoneyC`). With `MoneyA`, calling `DoubleRefAmount` mutates the shared instance in place (surprising aliasing), and putting `MoneyA` in a `HashSet`/`Dictionary` or calling `.Contains()`/`.Distinct()` on a `List<MoneyA>` "doesn't make sense" because equality falls back to reference identity. `MoneyC` (record class) gives compiler-generated value equality, `==`/`!=`, and copy-on-write via `with`, so `Contains`/`Distinct`/`HashSet`/`Dictionary` all behave correctly. Large structs (`SomethingElse` with 12 `int` fields) are called out as an antipattern because passing/copying them is not "cheap like an int" and generic instantiation over them causes extra JIT-emitted code per distinct value-type size.

## Before (Anti-pattern)

```csharp
class MoneyA
{
    public string Currency { get; set; } = "";
    public decimal Amount { get; set; } = 0.0m;
}

void DoubleRefAmount(MoneyA money) { money.Amount *= 2; }   // mutates the caller's instance

var refList = new List<MoneyA> { new MoneyA { Currency = "USD", Amount = 1.00m } };
var found = refList.Contains(new MoneyA { Currency = "USD", Amount = 1.00m });   // false: reference equality
```

## After (Standard)

```csharp
record class MoneyC(string Currency, decimal Amount);
// Reference type; immutable properties; non-destructive mutation (with-expression)
// Compiler-generated Equals/GetHashCode; == and != operators

MoneyC c = new("USD", 1.00m);
c = c with { Amount = c.Amount * 2 };   // copy-on-write, no aliasing surprises

var recordList = new List<MoneyC> { new MoneyC("USD", 1.00m) };
var found = recordList.Contains(new MoneyC("USD", 1.00m));   // true: value equality
```

## Rules for LLMs / Agents

- Use `record` (class) for immutable domain values (money, names, identifiers, DTOs) that should compare by value and support `with`-based non-destructive updates.
- Use `readonly record struct` only for small value types (a handful of primitive-sized fields) where avoiding heap allocation matters; never make a large multi-field struct (roughly >2-3 machine words) — it becomes expensive to copy and pass around.
- Use a mutable `class` only when the type has genuine reference identity, requires in-place mutation, or participates in mutable object graphs (e.g., entities tracked by an ORM).
- Never put a plain mutable reference-equality `class` into a `HashSet<T>`, use it as a `Dictionary<TKey,TValue>` key, or call `.Distinct()`/`.Contains()` on a list of them expecting value semantics — it will silently do the wrong thing.
- When a type is used as a generic type argument across many collections, consider that each distinct struct size/shape can cause additional JIT-generated code; reference types (or `object`) share the generated code.

## When NOT to apply

Entities with an identity that outlives their field values (e.g. an EF Core-tracked aggregate, an actor/service object) should remain classes with mutable state and identity-based equality — do not switch them to records just to gain value equality.
