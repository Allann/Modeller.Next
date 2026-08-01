---
title: "Wrap Measured Quantities in Dedicated Types, Never Raw double"
---

# Wrap Measured Quantities in Dedicated Types, Never Raw double


## The Standard

Never represent a measured physical quantity (length, area, latitude, longitude, and similar) as a bare `double`. Wrap each unit in its own `record struct` (e.g. `Length(double Meters)`, `Latitude(double Degrees)`) with validation in named factory members, unit-aware arithmetic operators, and a `ToString()` that renders the unit — so the type system, not a code comment or a variable name suffix, guarantees a value is in the unit and valid range you think it is.

## Why

The source video's premise is the class of bugs caused by using `double` for geolocation and other measured quantities: nothing stops a `double` meant to hold meters from being passed where feet were expected, or a `double` meant to hold a latitude from holding 200 (out of the valid ±90° range) — the compiler cannot catch a unit-confusion or invalid-range bug when everything is just `double`. The demo's final code instead gives every unit its own `record struct` (`Length`, `Area`, `Latitude`, `Longitude`), pushes range validation into named static factory members (`Latitude.North(48.8584)` throws `ArgumentOutOfRangeException` outside ±90°), and defines conversions/arithmetic as `extension` operators (e.g. `Length * Length => Area`) so unit-safe arithmetic reads naturally (`width * breadth`) while unit mismatches or invalid values are compile-time or construction-time errors instead of silent runtime corruption.

## Before (Anti-pattern)

```csharp
// Raw doubles: no unit safety, no range validation, easy to mix up meters/feet or lat/lon.
double width = 2.0;          // meters? feet? unclear from the type
double latitude = 200.0;     // compiles fine, but is not a valid latitude
double area = width * 3.1;   // meaningless if operands are different units
```

## After (Standard)

```csharp
public record struct Length(double Meters)
{
    public override string ToString() => $"{Meters} m";
}

public static class LengthCalculation
{
    extension(Length)
    {
        public static Area operator *(Length width, Length breadth) =>
            (width.Meters * breadth.Meters).SquareMeters;
    }
}

public record struct Latitude(double Degrees)
{
    public override string ToString() => $"{Math.Abs(Degrees)}° {(Degrees >= 0 ? "N" : "S")}";
}

public static class LatitudeConstruction
{
    extension(Latitude)
    {
        public static Latitude North(double degrees) =>
            degrees is < 0.0 or > 90.0
                ? throw new ArgumentOutOfRangeException(nameof(degrees), "North latitude must be within [0°, 90°]")
                : new Latitude(degrees);
    }
}
```

## Rules for LLMs / Agents

- Never declare a domain field, property, or parameter of type `double` (or `decimal`/`float`) to represent a measured quantity with a unit (length, area, angle, currency amount without a `Money` type, etc.) — wrap it in a dedicated `record struct`.
- Give each unit type a validating named factory (static method or property) for construction when the raw range is constrained (e.g. `Latitude.North`, `Latitude.Degrees`), and throw `ArgumentOutOfRangeException` from inside the factory rather than allowing an invalid instance to exist.
- Implement unit conversions and cross-unit arithmetic (e.g. `Length * Length -> Area`) as operators/extension members on the unit types themselves, not as ad hoc multiplication of raw numbers at call sites.
- Override `ToString()` on every unit type so it renders with its unit/format (e.g. `"2 m"`, `"48.8584° N"`) instead of a bare number, making unit confusion visible in logs and debugging output.
- Do not add a second, differently-named type for the same physical quantity (e.g. both a `Meters` and a `Length`) — pick one canonical unit type per quantity and convert at the boundary if another unit is needed for input/output.

## When NOT to apply

Pure, transient intermediate calculations that never cross a method boundary or get stored (e.g. a local scratch variable inside a tight numerical algorithm) can stay as `double` if introducing a wrapper type would add no safety benefit and the value never leaves the local scope. The moment a quantity is a parameter, return value, or field, it should be wrapped.
