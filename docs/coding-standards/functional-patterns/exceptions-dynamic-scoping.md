---
title: "Exceptions Are Dynamically Scoped — Don't Use Them for Expected Failures"
---

# Exceptions Are Dynamically Scoped — Don't Use Them for Expected Failures


## The Standard

Do not use `throw`/`catch` to signal or handle expected, recoverable failure conditions. Model expected failures explicitly as part of a method's return type (a `Result<T, TError>` / `Option<T>`-style type), and reserve exceptions for truly exceptional, unrecoverable conditions that are caught only at a single well-defined boundary — never scattered across multiple layers of the call stack to intercept specific failure types from deeply nested calls.

## Why

Exception propagation is resolved by *dynamic scope* — the actual runtime call chain — not by the lexical structure of the code. A `catch` block therefore doesn't just handle errors from the method it appears to wrap; it silently reaches through every method transitively called beneath it, including ones the author of the `catch` never looked at and future ones that don't exist yet. In the demo, `MethodA` has specific `catch` clauses for exception types that are actually thrown two calls deeper, inside `MethodC`, with `MethodB` in between doing nothing to declare or advertise that fact. Nothing in `MethodB`'s signature says "I might let a `NetworkError` through." A generic `catch` at the top absorbs anything else, including brand-new exception types (like `IOError`, defined but never thrown yet) added later by someone who has no idea `MethodA` exists.

This is the opposite of how a `Result`/`Option` return type behaves: the possible failure is part of the method's *static* signature, visible at the call site, checked by the compiler, and composed explicitly through the call chain rather than being caught (or missed) based on which methods happen to be on the stack at runtime.

## Before (Anti-pattern)

```csharp
public int MethodA(int arg)
{
    try
    {
        return MethodB(arg) + 2;
    }
    catch (ErrorB)       { return 0; }
    catch (NetworkError) { return -5; }   // thrown two frames down, in MethodC
    catch                { return -1; }   // silently absorbs any future exception type
}

public int MethodB(int arg)
{
    if (arg < 0) throw new InvalidRequestError();
    return arg % MethodC(arg);            // MethodC's failures aren't part of MethodB's signature
}

public int MethodC(int arg)
{
    if (Random.Shared.Next(20) == 19) throw new NetworkError();
    return arg > 10 ? 2 : 3;
}
```

## After (Standard)

```csharp
public Result<int, OperationError> MethodA(int arg) =>
    MethodB(arg).Map(value => value + 2);

public Result<int, OperationError> MethodB(int arg)
{
    if (arg < 0) return Result<int, OperationError>.Failure(OperationError.InvalidRequest);
    return MethodC(arg).Map(modulo => arg % modulo);
}

public Result<int, OperationError> MethodC(int arg)
{
    if (Random.Shared.Next(20) == 19)
        return Result<int, OperationError>.Failure(OperationError.Network);

    return Result<int, OperationError>.Success(arg > 10 ? 2 : 3);
}
```

## Rules for LLMs / Agents

- Never use `try`/`catch` as a substitute for validating input, checking preconditions, or branching on an expected outcome (e.g. "not found", "invalid format", "conflict").
- When a method can fail in a way the caller is expected to handle, return a `Result<T, TError>` (or `Option<T>` for absence) instead of throwing.
- Do not place a `catch` clause several frames above where an exception is thrown "because it happens to work" — if you find yourself catching a specific exception type that no method you're directly calling throws, that is a sign the failure should be a `Result` instead, or caught at the point closest to where it is thrown.
- Never write a bare `catch { ... }` / `catch (Exception) { ... }` that swallows and translates errors into a fallback value inside business logic — this masks new/unanticipated failure types instead of surfacing them. Only equivalent code at a genuine process boundary (top-level host, middleware, background job runner) may do this, and only to log/report, not to silently substitute a value.
- Keep exception hierarchies for exceptions that are actually unrecoverable at the point of catch (programmer errors, environment failures) — not for conditions the calling code has a defined response to.
- When reviewing or writing code that calls a method with a `Result`/`Option` return type, propagate or map the failure explicitly (`.Map`, `.Bind`, pattern matching) rather than unwrapping with a throwing accessor (e.g. `.Value` without checking `.IsSuccess`) partway through business logic.

## When NOT to apply

Exceptions remain appropriate for truly exceptional, unrecoverable conditions (e.g. out-of-memory, corrupted invariants, programmer errors such as null-reference/argument violations) and for framework/infrastructure boundaries (e.g. ASP.NET Core's global exception handling middleware translating an uncaught exception into a 500 response) where a single top-level catch is the intended design, not a proxy for per-call-site error handling.
