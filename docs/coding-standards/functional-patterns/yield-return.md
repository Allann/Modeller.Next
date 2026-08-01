---
title: "Write Iterator Methods with yield return, Not a Hand-Rolled IEnumerator"
---

# Write Iterator Methods with yield return, Not a Hand-Rolled IEnumerator


## The Standard

When a method needs to produce a lazily-evaluated sequence, implement it as an iterator method using `yield return` and return `IEnumerable<T>` (or `IAsyncEnumerable<T>`), rather than hand-authoring a class that implements `IEnumerable<T>`/`IEnumerator<T>` with manual `MoveNext()` state-machine logic.

## Why

The reference material demonstrates both approaches producing the identical sequence `0..n-1`, side by side. The `yield return` version is a four-line local function: the compiler generates the entire state machine (fields for current position, `Current`, `MoveNext`, `Dispose`, `Reset`) automatically. The manually-written equivalent (`NumbersSequenceGenerator` + `NumbersEnumerator`) requires two extra classes, an explicit `_state` field, a `switch` expression encoding what would otherwise be implicit control flow across `MoveNext()` calls, and hand-implemented `Current`, `Reset`, and `Dispose` members — all to reproduce behavior `yield return` gives for free, with far more surface area for an off-by-one or missed-state bug.

## Before (Anti-pattern)

```csharp
class NumbersSequenceGenerator(int n) : IEnumerable<int>
{
    public IEnumerator<int> GetEnumerator() => new NumbersEnumerator(n);
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

class NumbersEnumerator(int n) : IEnumerator<int>
{
    public int Current { get; private set; } = 0;
    private int _state = 0;
    object IEnumerator.Current => Current;

    public bool MoveNext()
    {
        (_state, Current, var result) = _state switch
        {
            0 when Current < n => (1, Current, true),
            0 => (2, 0, false),
            1 when ++Current < n => (1, Current, true),
            1 => (2, Current, false),
            _ => (2, Current, false)
        };
        return result;
    }

    public void Reset() => (_state, Current) = (0, 0);
    public void Dispose() { }
}
```

## After (Standard)

```csharp
IEnumerable<int> GetNumbersSequence(int n)
{
    for (int i = 0; i < n; i++)
    {
        yield return i;
    }
}
```

## Rules for LLMs / Agents

- When writing a method that produces a sequence of values, default to an iterator method (`yield return`) returning `IEnumerable<T>`/`IAsyncEnumerable<T>`, not a manually implemented `IEnumerator<T>`/`IEnumerable<T>` class pair.
- Never hand-implement `IEnumerator<T>.MoveNext()` as a state machine (explicit `_state` field plus a `switch`) when the same sequence can be expressed as a loop with `yield return` inside a method.
- Only implement `IEnumerable<T>`/`IEnumerator<T>` manually when something `yield return` cannot express is required (e.g. a custom `Reset()` that must rewind without recreating the enumerator, or integrating with an existing non-.NET iteration protocol) — and document why in that case.
- Prefer `yield return` even for finite, simple sequences (e.g. `for` loops producing a bounded range) — brevity and correctness both favor it over manual state machines regardless of sequence size.

## When NOT to apply

If a genuine requirement needs an enumerator with behavior `yield return` cannot produce (e.g. supporting `Reset()` without re-invoking the iterator method, or needing a struct-based enumerator to avoid allocation on a hot path), a manual `IEnumerator<T>` implementation is justified — but this should be the exception, not the default.
