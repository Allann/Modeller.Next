---
title: "Use var by Default; Let Descriptive Names Carry the Type Information"
---

# Use var by Default; Let Descriptive Names Carry the Type Information


## The Standard

Declare local variables with `var` by default, including for primitives and simple types, and rely on descriptive variable/method names and the right-hand-side expression to convey the type to a reader — rather than writing out explicit types "for clarity." Only fall back to an explicit type when `var` would genuinely leave the type ambiguous to a reader at the point of declaration (e.g. the initializer's return type isn't obvious from its name).

## Why

The source material's premise (per its title, "Change Your Mindset to Understand the `var` Keyword") is that resistance to `var` usually comes from treating the explicit type annotation as the only source of type information, when in a well-named codebase the variable name and the initializing expression already communicate intent more directly than a type name does. The reference implementation uses `var` uniformly — `var id = 17;`, `var car = new Repository<Vehicle>().Find(id);`, `var collisions = car?.DetectPossibleCollisions(...).ToList() ?? [];`, and even the destructured tuple pattern result `var report = (car, collisions) switch { ... }` — because each name (`id`, `car`, `collisions`, `report`) already states what the value is, and the method/property names on the right (`Find`, `DetectPossibleCollisions`) already state what shape it takes. Spelling out `int id`, `Vehicle? car`, `List<IMobile> collisions` would add characters without adding information a careful reader doesn't already have from the names.

## Before (Anti-pattern)

```csharp
int id = 17;
Vehicle? car = new Repository<Vehicle>().Find(id);
List<IMobile> collisions = car?.DetectPossibleCollisions(TimeSpan.FromSeconds(3)).ToList() ?? new List<IMobile>();
```

## After (Standard)

```csharp
var id = 17;
var car = new Repository<Vehicle>().Find(id);
var collisions = car?.DetectPossibleCollisions(TimeSpan.FromSeconds(3)).ToList() ?? [];

var report = (car, collisions) switch
{
    (null, _) => "Car not found...",
    (_, []) => "You're safe to proceed.",
    (_, var cars) => $"Collisions imminent! With: {string.Join(", ", cars)}"
};
```

## Rules for LLMs / Agents

- Default to `var` for local variable declarations, including primitives (`var count = 0;`) and simple value types, not just for complex generic types.
- Do not add an explicit type annotation purely as a form of self-documentation when the variable's name and initializer already make the type clear — that is treating the annotation as a crutch instead of writing a better name.
- Give variables names precise enough that the reader doesn't need the type spelled out to understand what the value represents (e.g. `collisions`, not `list1` or `result`).
- Use `var` inside pattern-matching arms (e.g. `(_, var cars) => ...`) consistently with the rest of the codebase's `var` usage.
- Reserve an explicit type declaration for the narrow case where the initializer's type genuinely cannot be inferred by a reader from the name and the right-hand side (e.g. numeric literal suffix ambiguity, or an interface-typed factory return where the concrete type matters to the reader).

## When NOT to apply

If assigning a numeric literal where the specific numeric type matters and isn't obvious from the literal (e.g. distinguishing `float` vs `double` vs `decimal`), or when the declared type is intentionally broader/narrower than the initializer's static type (e.g. `IReadOnlyList<T> items = someConcreteList;` to enforce an abstraction at the point of declaration), keep the explicit type.
