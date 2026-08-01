---
title: "Prefer params IReadOnlyList<T> (or Span) Over params T[]"
---

# Prefer `params IReadOnlyList<T>` (or Span) Over `params T[]`


## The Standard

When declaring a `params` parameter, use `params IReadOnlyList<T>` (or, for hot paths, `params ReadOnlySpan<T>`) instead of the classic `params T[]`. This lets every existing `List<T>`, array, span, or collection-expression call site pass its data directly without an allocation or copy, while still allowing ad-hoc call sites to pass loose values.

## Why

C# 13's `params` collections extend the `params` keyword to any type constructible via a collection expression (arrays, spans, anything with an indexer and `Count`/`Length`, or a type with an accessible parameterless constructor plus `Add`). Declaring the parameter as `params IReadOnlyList<int>` means: a caller who already has an `int[]` passes it with **no memory-to-memory copy** (`max(data[0], data[1..])`); a caller with a `List<int>` passes it directly; and a caller with loose values (`max(1, 2, 3, 4, 5)`) still gets an array/list synthesized for them automatically. Locking the parameter to `params T[]` forces every non-array caller (spans, lists) to materialize a new array just to satisfy the signature — pure allocation waste for call sites that already had a suitable collection in hand.

## Before (Anti-pattern)

```csharp
// Forces an array; List<T> and ReadOnlySpan<T> callers must copy into a new array first
int Max(int first, params int[] others)
{
    int max = first;
    foreach (var item in others) if (item > max) max = item;
    return max;
}
```

## After (Standard)

```csharp
// Accepts arrays, spans, lists, or loose values without an unnecessary copy
int Max(int first, params IReadOnlyList<int> others)
{
    int max = first;
    foreach (var item in others) if (item > max) max = item;
    return max;
}

int[] data = [1, 2, 3, 4, 5];
int a = Max(data[0], data[1..]);        // slice passed with no copy
List<int> list = [1, 2, 3, 4, 5];
int b = Max(0, list);                   // list passed directly
int c = Max(1, 2, 3, 4, 5);             // still works; compiler builds a collection
```

## Rules for LLMs / Agents

- Declare new `params` parameters as `params IReadOnlyList<T>` by default; use `params ReadOnlySpan<T>` when the method is a performance-sensitive, non-async hot path and does not need to store the collection.
- Do not use `params T[]` in new code unless the method genuinely requires array-specific APIs (e.g., `Array.Sort` in place) on the parameter itself.
- When accepting an existing array/list/span as the trailing argument, pass it directly (or as a slice, e.g. `data[1..]`) rather than spreading its elements — the compiler-supported collection types make this safe and copy-free.
- Guard against empty input explicitly at the top of the method (`if (data.Length == 0) throw ...`) when a `params` collection may legally be empty and the method's logic assumes at least one element beyond the fixed leading parameter(s).

## When NOT to apply

Requires C# 13 / .NET 9 or later for `params` collections beyond arrays — on earlier language versions, `params T[]` is the only option. Also keep `params T[]` when the method needs true array semantics (in-place mutation, `Array`-only APIs) rather than read-only iteration.
