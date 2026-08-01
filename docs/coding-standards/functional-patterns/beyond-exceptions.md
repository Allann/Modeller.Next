---
title: "Prefer Result Types Over Exceptions for Expected Failures"
---

# Prefer Result Types Over Exceptions for Expected Failures


## The Standard

For failures that are expected/recoverable parts of a workflow (validation errors, transient network errors, business-rule violations), return a typed `Result<T, TError>` (or `Option<T>`) instead of throwing and catching exceptions across method boundaries. Reserve real .NET exceptions for truly exceptional, programmer-error, or unrecoverable conditions.

## Why

The demo contrasts two designs solving the identical problem (`MethodA` calling `MethodB` calling `MethodC`, with faults that must be classified and handled by the caller). The exception-based version requires the caller to know every exception type thrown transitively by callees several layers down (`ErrorB`, `NetworkError`, `TimeoutError`), and the compiler enforces nothing — a new exception type added deep in `MethodC` silently changes `MethodA`'s behavior unless a test happens to exercise it. The `Result<T, TError>`-based version (`ComponentA`/`ComponentB`/`ComponentC`) makes each method's possible failures part of its return type signature. Each layer explicitly `Map`s/`MapError`s errors from the layer below into its own error type, so the caller's `Match` is exhaustive and compiler-checked, and errors are translated (not just re-thrown) as they cross architectural boundaries.

## Before (Anti-pattern)

```csharp
public int MethodA(int arg)
{
    try { return MethodB(arg) + 2; }
    catch (ErrorB) { return 0; }
    catch (NetworkError) { return -5; }
    catch { return -1; }
}

public int MethodB(int arg)
{
    if (arg < 0) throw new InvalidRequestError();
    return arg % MethodC(arg);       // MethodC can also throw NetworkError, TimeoutError
}
```

## After (Standard)

```csharp
class ComponentA
{
    private ComponentB _dependency = new();

    public int MethodA(int arg) =>
        _dependency.MethodB(arg).Match(
            onSuccess: value => value + 2,
            onFailure: Handle);

    private int Handle(ErrorB error) => error switch
    {
        TransientError => -5,
        FatalError => -1,
        _ => 0
    };
}

class ComponentB
{
    public Result<int, ErrorB> MethodB(int arg) =>
        GetValidArgument(arg).Bind(GetModulo);

    private Result<int, ErrorB> GetModulo(int arg) =>
        new ComponentC().MethodC(arg)
            .Map(modulo => arg % modulo)
            .MapError(error => error switch
            {
                NetworkError => (ErrorB)new TransientError(),
                _ => new FatalError()
            });
}
```

## Rules for LLMs / Agents

- Model expected/recoverable failures (validation, business rules, "not found", transient I/O) as `Result<T, TError>` or `Option<T>` return values, not thrown exceptions.
- Define errors as a closed set of `record` types (an error discriminated union) per component/boundary, not shared/leaked concrete exception types from lower layers.
- When crossing an architectural boundary (e.g. component B calling component C), translate the lower layer's error type into the caller's own error type via `MapError`, don't let internal error types bubble up unchanged.
- Use `Bind`/`Map`/`MatchAsync` chains to compose multi-step operations that can each fail, instead of nested try/catch blocks.
- Consume a `Result` at the boundary with an exhaustive `Match`/`switch` over all declared error variants — do not add a catch-all `_ => ...` that silently swallows new error cases without an explicit decision.
- Reserve thrown exceptions for programmer errors (invalid arguments, broken invariants) and truly unrecoverable conditions, not for control flow.

## When NOT to apply

Use ordinary exceptions (not `Result`) for programmer errors (e.g. `ArgumentException` on invalid constructor input) and for conditions the caller cannot reasonably be expected to handle locally (out-of-memory, corrupted state). Framework/ASP.NET Core infrastructure code that must integrate with exception-based middleware (global exception handlers) can still translate a caught exception into an HTTP response at the outermost boundary.
