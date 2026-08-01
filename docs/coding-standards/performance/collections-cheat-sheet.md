---
title: "Pick the Right Collection Type for the Access Pattern"
---

# Pick the Right Collection Type for the Access Pattern


## The Standard

Choose the collection type based on how the data will actually be accessed and what invariants it must hold — fixed-size indexed access (`Array`), growable indexed access (`List<T>`), key lookup (`Dictionary<TKey,TValue>`), uniqueness/set algebra (`HashSet<T>`), or undo/redo & LIFO/FIFO processing (`Stack<T>`/`Queue<T>`) — rather than defaulting to `List<T>`/`Dictionary<T,object>` for everything. Use collection expressions (`[]`) for literals and prefer purpose-built collections (a real `HashSet<T>`, not a `Dictionary<T, object?>` used as a fake set) so the type itself documents intent.

## Why

The cheat-sheet demo walks through the natural progression: a fixed-size `int[]`/`string[]` when the size is known upfront, `List<T>` once the count becomes dynamic (`int.Parse(Console.ReadLine())`), `Dictionary<TKey,TValue>` for `O(1)` key-based lookup, and a `Dictionary<string, object?>` "fake set" that is explicitly upgraded to a real `HashSet<string>` supporting `UnionWith`/`IntersectWith`/`ExceptWith` set algebra directly. The command-pattern example (`ICommand`, `ExecutionEngine`) shows `Stack<ICommand>` used for undo/redo specifically because the access pattern is LIFO — pushing on execute, popping on undo, with a second stack for redo — which a `List<T>` could technically do but would not communicate the intent or prevent misuse (e.g., indexing into the middle).

## Before (Anti-pattern)

```csharp
// "Set" implemented as a dictionary with unused values -- intent is hidden, no set algebra
Dictionary<string, object?> numbersSet = new()
{
    { "Zero", null }, { "One", null }, { "Two", null }
};
bool hasFive = numbersSet.ContainsKey("Five");
// No built-in UnionWith/IntersectWith/ExceptWith -- would need to hand-roll them
```

## After (Standard)

```csharp
HashSet<string> properSet = new(["Zero", "One", "Two", "Three", "Four", "Five"]);
HashSet<string> otherSet = new(["Five", "Six", "Seven"]);

properSet.Add("Infinite");
bool containsFive = properSet.Contains("Five");
properSet.UnionWith(otherSet);
properSet.IntersectWith(otherSet);
properSet.ExceptWith(otherSet);

// Undo/redo: Stack<T> communicates and enforces LIFO access
class ExecutionEngine
{
    private readonly Stack<ICommand> _undoStack = new();
    private readonly Stack<ICommand> _redoStack = new();

    public void Execute(ICommand command)
    {
        command.Execute();
        _undoStack.Push(command);
        _redoStack.Clear();
    }
}
```

## Rules for LLMs / Agents

- Do not use `Dictionary<TKey, object?>` (or similar) to emulate a set; use `HashSet<T>` and its set-algebra methods (`UnionWith`, `IntersectWith`, `ExceptWith`, `Contains`).
- Use `Array`/fixed-size collections when the count is known and fixed at creation; use `List<T>` only once the collection genuinely grows/shrinks.
- Use `Dictionary<TKey, TValue>` when lookups are by a natural key, not by iterating and comparing.
- Use `Stack<T>` for LIFO processing (undo/redo, backtracking) and `Queue<T>` for FIFO processing — don't repurpose `List<T>` with manual `Add`/`RemoveAt(0)`/`RemoveAt(Count-1)` for these.
- Prefer collection expressions (`[a, b, c]`) over `new List<T> { a, b, c }` / `new[] { a, b, c }` for literals.
- Choose the collection type to match and enforce the access pattern (uniqueness, key lookup, order of removal) rather than picking whatever collection is already in scope.

## When NOT to apply

For very small, short-lived local collections where performance and API-intent are not a concern (e.g., a handful of items inside a single method), the simplest collection (`List<T>` or an array) is fine even if a more specialized type exists.
