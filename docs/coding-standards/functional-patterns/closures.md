---
title: "Understand Closures Capture Variables by Reference, Not by Value"
---

# Understand Closures Capture Variables by Reference, Not by Value


## The Standard

Treat a captured local variable inside a lambda/closure as a live reference to the same storage location, not a snapshot of its value at creation time. When a closure needs to freeze a value at a point in time, copy it into a new local variable before capturing.

## Why

The demo decompiles what the compiler actually does with `Func<int> lambda = () => Utils.Prepare(n, m);`: it generates a hidden class (`MyClosure`) holding fields `_n` and `_m`, and the lambda becomes a method on an instance of that class. Because `n` and `m` are captured by reference to the same closure instance, reassigning `n = 3;` after creating the lambda changes what the lambda returns on its next invocation (`Puzzle` prints `1` then `3` for the same `lambda` variable) — a common source of subtle bugs in loops or event handlers where the "current" value of a captured loop variable is expected but the final value is observed instead.

## Before (Anti-pattern)

```csharp
int n = 2;
int m = 5;
Func<int> lambda = () => Utils.Prepare(n, m);

Console.WriteLine(Utils.Puzzle(0, lambda));   // Prints: 1
n = 3;
Console.WriteLine(Utils.Puzzle(0, lambda));   // Prints: 3 -- same lambda, different result!
```

## After (Standard)

```csharp
// Freeze the value at capture time by copying into a new local before the closure
int n = 2;
int m = 5;
int frozenN = n;
Func<int> lambda = () => Utils.Prepare(frozenN, m);

n = 3;                                         // no longer affects lambda's result
Console.WriteLine(Utils.Puzzle(0, lambda));    // still prints value based on frozenN == 2
```

## Rules for LLMs / Agents

- Never assume a lambda captures the value of an outer variable at the point of declaration; it captures the variable itself (its storage location).
- When writing a loop that creates closures (event handlers, deferred `Task`s, LINQ `Select` with side effects), if each closure needs its own snapshot of the loop variable, copy it into a new local declared inside the loop body before capturing.
- Be especially careful with mutable fields/locals shared between a closure and the enclosing method after the closure is created — mutations are visible to the closure and vice versa.
- When reviewing/generating code with closures over mutable state, call out explicitly whether by-reference capture is intentional or should be avoided.

## When NOT to apply

If the captured variable is never reassigned after the closure is created (effectively `readonly`/immutable at that point), by-reference-vs-by-value capture is not observable and no extra copying is needed.
