---
title: "Use Modern C# Syntax to Remove Boilerplate, Not Just to Look Terse"
---

# Use Modern C# Syntax to Remove Boilerplate, Not Just to Look Terse


## The Standard

Replace hand-written null-check ceremony, manual value-equality/`ToString` overrides, and imperative collection-building with the modern C# equivalents: null-conditional/coalescing operators (`?.`, `??`, `?.Member = value`), collection expressions (`[...]`), expression-bodied LINQ (`OrderBy`, `Distinct`), records for structural equality, and the C# 14 `field` keyword for auto-property-backed custom accessors. Every simplification must preserve the original behavior and invariants (e.g. validation still throws, "set-once" semantics still hold).

## Why

The "before" `Circle`/`Point` classes hand-implement `IEquatable<T>`, `Equals`, `GetHashCode`, `ToString`, and copy constructors — dozens of lines of boilerplate that a `record` generates automatically and correctly. Null handling was done with explicit `if (x is not null) ... else ...` blocks that the null-conditional/coalescing operators (`c2?.Tag ?? "N/A"`, `result?.Tag = "Resized"`) express in one line with identical semantics. `SortByRadiusDemo`/`DistinctDemo` were multi-line imperative loops calling `.Sort()`/wrapping in `new HashSet<>()`; the modern version is a single expression-bodied LINQ call (`circles.OrderBy(c => c.Radius)`, `circles.Distinct()`). The `Tag` property's "set only if not already set" invariant, previously a hand-written backing field (`_tag`) with a custom getter/setter, is preserved exactly using the C# 14 `field` keyword (`set => field = field == string.Empty ? value : field;`) without needing an explicit private field declaration.

## Before (Anti-pattern)

```csharp
class Circle : IEquatable<Circle>
{
    private string _tag = string.Empty;
    public string Tag
    {
        get { return _tag; }
        set { if (_tag != string.Empty) return; _tag = value; }
    }

    public Circle(Point center, float radius)
    {
        if (radius <= 0) throw new ArgumentOutOfRangeException(nameof(radius), "Radius must be positive.");
        Center = center; Radius = radius;
    }

    public override bool Equals(object? obj) => Equals(obj as Circle);
    public bool Equals(Circle? other) => other is not null && Radius == other.Radius && Center.Equals(other.Center);
    public override int GetHashCode() => ((17 * 23 + Center.GetHashCode()) * 23) + Radius.GetHashCode();
}

string tag2 = "N/A";
if (c2 is not null) tag2 = c2.Tag;
```

## After (Standard)

```csharp
record Circle(Point Center, float Radius, string Tag = "")
{
    public float Radius { get; init; } =
        Radius > 0 ? Radius : throw new ArgumentOutOfRangeException(nameof(Radius), "Radius must be positive.");

    public string Tag
    {
        get;
        set => field = field == string.Empty ? value : field;
    } = Tag;

    public Circle Resize(float factor) => this with { Radius = Radius * Math.Abs(factor) };
}

Console.WriteLine($"Tag #2: {c2?.Tag ?? "N/A"}");
List<Circle> circles = [ /* ... */ ];
IEnumerable<Circle> SortByRadiusDemo(IEnumerable<Circle> circles) => circles.OrderBy(c => c.Radius);
IEnumerable<Circle> DistinctDemo(IEnumerable<Circle> circles) => circles.Distinct();
```

## Rules for LLMs / Agents

- Replace `if (x is not null) y = x.Member; else y = fallback;` patterns with `x?.Member ?? fallback`.
- Replace `if (x is not null) x.Member = value;` with `x?.Member = value`.
- Use collection expressions (`[a, b, c]`) instead of `new List<T> { a, b, c }` / `new T[] { ... }` for literals.
- Use `record`/`record struct` instead of hand-rolled `IEquatable<T>` + `Equals`/`GetHashCode`/`ToString` overrides for structural-equality types.
- Use expression-bodied LINQ (`.OrderBy(...)`, `.Distinct()`) instead of manual `.Sort()` calls or wrapping collections in `new HashSet<T>(...)` purely to deduplicate.
- Use the `field` keyword inside a property accessor when a custom getter/setter needs a compiler-backed field, instead of declaring an explicit private backing field.
- When simplifying to modern syntax, verify the simplified form preserves the exact original behavior (validation, set-once semantics, null-handling edge cases) — do not silently change semantics while "modernizing" syntax.

## When NOT to apply

Don't force a `record` conversion onto a type with genuine mutable reference identity, and don't use `field`/collection expressions/null-conditional chains so aggressively that a single line becomes hard to read — clarity still wins over maximal terseness.
