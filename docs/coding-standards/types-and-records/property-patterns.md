---
title: "Use Property Patterns and Tuple Switch Expressions Instead of If/Else Chains"
---

# Use Property Patterns and Tuple Switch Expressions Instead of If/Else Chains


## The Standard

Express branching logic that depends on the shape or combination of an object's properties as a `switch` expression using property patterns (`{ Property: pattern }`), tuple patterns for multi-value decisions, and `when` clauses for extra conditions — instead of nested `if`/`else` or a chain of boolean checks.

## Why

The reference material shows state-transition logic for an HVAC controller and a text-alignment routine. Both are naturally a decision table: "given this combination of properties, produce that result." Writing this as property/tuple patterns keeps every branch visible as one row of a `switch`, keeps related conditions physically grouped (`(device, sensors) switch { (_, { Temperature: var temp }) when temp < low => ..., ... }`), and lets the compiler flag non-exhaustive matches. The equivalent if/else version scatters the same conditions across separate statements and hides the fact that only one branch of a table is meant to fire. Nested property patterns (`{ Length: var len }` inside a tuple pattern) also let a single expression both test a condition and bind a variable for reuse in the following clauses, replacing repeated property lookups.

## Before (Anti-pattern)

```csharp
Hvac Update(Hvac device, Sensors sensors)
{
    if (device.State == State.Off) return device;
    if (device.Mode == OperatingMode.Off) { device.State = State.StandBy; return device; }
    if (device.Season == Season.Heating)
    {
        if (sensors.Temperature.Celsius < idealLow) { device.State = State.Heating; return device; }
        if (device.State == State.Heating && sensors.Temperature.Celsius >= idealHigh)
            { device.State = State.StandBy; return device; }
    }
    return device;
}
```

## After (Standard)

```csharp
Hvac Update(Hvac device, Sensors sensors) => device switch
{
    { State: State.Off } => device,
    { Mode: OperatingMode.Off } => device with { State = State.StandBy },
    { Season: Season.Heating } => RegulateHeating(device, sensors, GetIdealTemperatureRange()),
    { Season: Season.Cooling } => RegulateCooling(device, sensors, GetIdealTemperatureRange()),
    _ => device
};

Hvac RegulateHeating(Hvac device, Sensors sensors, (Temperature low, Temperature high) ideal) => (device, sensors) switch
{
    (_, { Temperature: var t }) when t < ideal.low => device with { State = State.Heating },
    ({ State: State.Heating }, { Temperature: var t }) when t >= ideal.high => device with { State = State.StandBy },
    _ => device
};
```

## Rules for LLMs / Agents

- When a method's job is to pick one of several outcomes based on an object's property values, write it as a `switch` expression with property patterns, not a sequence of `if` statements.
- When the decision depends on two or more objects together, switch on a tuple of them (`(a, b) switch { ... }`) rather than nesting separate switches or if-chains.
- Use `when` clauses to attach extra boolean conditions to a pattern arm instead of combining everything into one large `&&` expression.
- Bind reusable sub-values with `{ Property: var name }` inside the pattern instead of re-reading `obj.Property` multiple times in the following clauses.
- Prefer expression-bodied `switch` returning a new value (`device with { State = ... }`) over mutating the input in place inside a branch.
- Always include a `_ =>` (or otherwise exhaustive) default arm so the switch expression compiles without a "not all paths return a value" warning and behaves predictably for unmatched shapes.

## When NOT to apply

If a branch's condition cannot be expressed as a shape/value pattern (e.g., it calls into an async operation, or the condition is inherently procedural with side effects per step), keep it as an `if`/`else` or a regular `switch` statement — do not force such logic into a pattern-matching expression for its own sake.
