---
title: "Verify AI-Generated Code's Claims Instead of Trusting Its Self-Description"
---

# Verify AI-Generated Code's Claims Instead of Trusting Its Self-Description


## The Standard

Never accept an AI coding assistant's description of what its own code does (e.g. "thread-safe," "comprehensive error handling," "clean architecture") as evidence that the code actually has that property. Every non-trivial claim about concurrency, error handling, or architectural guarantees must be verified by reading the actual synchronization primitives, catch blocks, and object graph — not by trusting the accompanying README or code comments the assistant produced alongside the code.

## Why

This material is the finished output of an AI coding assistant (VS Code Copilot) asked to build a console Snake game, used in a video arguing that "vibe coding" — accepting AI output without understanding or verifying it — is a dead end for juniors and non-programmers. The generated `README.md` explicitly claims "Thread Safe: Proper synchronization between game logic and input handling" and "Comprehensive exception handling." Reading the actual code shows neither claim holds: `SnakeGameEngine` mutates `_gameState`, `_snake`, and `_food` from a `System.Threading.Timer` callback (`GameTick`, thread-pool thread) with zero locks, while `GameController.HandleInputAsync` concurrently calls `_gameEngine.HandleInput(direction)` from a separate `Task.Run` thread, and `GameLoopAsync` concurrently reads the same fields from yet another async loop — there is no `lock`, `Interlocked`, `SemaphoreSlim`, or `volatile` anywhere in `SnakeGameEngine`. Similarly, the "comprehensive" error handling is a single `catch (Exception ex) { Console.WriteLine(...); break; }` per loop, which silently ends the render/input loop on any error rather than handling anything. The code reads as polished and professionally organized (clear namespaces, `GameEventArgs`, events, a `Snake.csproj`), which is precisely what makes it dangerous: a junior or non-programmer directing an AI assistant has no independent way to know the confident-sounding claims are false, because the code superficially looks like it was written by someone who knew what "thread-safe" means.

## Before (Anti-pattern)

```csharp
// README claims: "Thread Safe: Proper synchronization between game logic and input handling"
// Actual code: no lock, no Interlocked, no volatile guarding shared mutable state
// that is written from a Timer callback and read/written from two other async loops.
private void GameTick(object? state)          // runs on a thread-pool timer thread
{
    if (_gameState != GameState.Playing) return;
    var newHead = _snake.Move(foodEaten);      // mutates _snake with no synchronization
    // ...
}

public bool HandleInput(Direction direction)   // called concurrently from a separate Task.Run loop
{
    if (_gameState != GameState.Playing) return false;
    return _snake.ChangeDirection(direction);  // races with GameTick's mutation of _snake
}
```

## After (Standard)

```csharp
// Before accepting an AI-authored claim like "thread-safe," verify it explicitly:
// either add real synchronization for state touched from multiple execution contexts,
// or correct the documentation to state the actual (single-threaded/cooperative) guarantee.
private readonly object _stateLock = new();

private void GameTick(object? state)
{
    lock (_stateLock)
    {
        if (_gameState != GameState.Playing) return;
        var newHead = _snake.Move(foodEaten);
        // ...
    }
}

public bool HandleInput(Direction direction)
{
    lock (_stateLock)
    {
        if (_gameState != GameState.Playing) return false;
        return _snake.ChangeDirection(direction);
    }
}
```

## Rules for LLMs / Agents

- Treat any comment, docstring, or README generated alongside code (by yourself or another AI assistant) as an unverified claim, not a fact — cross-check it against the actual implementation before repeating or relying on it.
- When code mutates shared state from more than one thread, task, or timer callback, verify there is an actual synchronization mechanism (`lock`, `Interlocked`, `SemaphoreSlim`, immutable/message-passing design) present — do not describe code as "thread-safe" unless you have traced every writer and reader of the shared state.
- Do not let a broad `catch (Exception ex) { Console.WriteLine(...); }` (or similar swallow-and-log) be described as "comprehensive error handling" — that phrase implies deliberate handling per failure mode, which a single catch-all does not provide.
- When generating a README, feature list, or doc comment for code you just wrote, only assert a property (thread-safety, performance, security) that you have explicitly implemented and can point to in the code — never carry over a generic claim because it's a common thing to say about that kind of project.
- When reviewing AI-generated code on behalf of a user who may not be able to independently verify it, proactively flag any concurrency, security, or correctness claim in the accompanying documentation that doesn't hold up against the code, rather than passing the documentation through unchallenged.

## When NOT to apply

None observed — verifying claims against implementation is a baseline discipline that applies regardless of project size or whether the code was AI- or human-authored; it is not situational.
