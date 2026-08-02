---
title: Data types
description: The closed value types used by Fields, Facts, and rule evaluation.
---

# Data types

Modeller uses two independent, closed sets of value types. A
[Field](/docs/concepts/ubiquitous-language#field) on an
[Entity](/docs/concepts/ubiquitous-language#entity) has one
[Data Type](/docs/concepts/ubiquitous-language#data-type) describing the shape
of stored domain data. A [Fact](/docs/concepts/ubiquitous-language#fact) has one
Fact type describing the shape of information supplied to a
[rule](/docs/concepts/ubiquitous-language#rule) or
[decision](/docs/concepts/ubiquitous-language#decision). The two sets are not
interchangeable: a Field's Data Type governs generated storage and API shapes,
while a Fact's type governs what a rule or decision may reason about.

## Field data types

| Data Type | Constraints | Purpose |
| --- | --- | --- |
| `Boolean` | none | A true/false value |
| `String` | optional `minimumLength`, `maximumLength` | Text |
| `Byte`, `Int16`, `Int32`, `Int64` | none | Whole numbers of increasing width |
| `Decimal` | optional `precision`, `scale` | Fixed-point numbers |
| `Date`, `Time`, `DateTime`, `DateTimeOffset` | none | Calendar and clock values |
| `UniqueIdentifier` | none | A globally unique value |
| `GeographicCoordinate` | none | A latitude/longitude pair |
| `Enumeration` | references a named enumeration by identity | A closed, named set of values |
| `EntityReference` | references a named entity by identity | An identity-based link to another entity |
| `ValueTypeReference` | references a named value type by identity | An identity-based link to a reusable value type |

Each Data Type carries only the constraints that apply to it — precision and
scale belong only to `Decimal`, and a reference type carries only a stable
target identity. Optionality and collection cardinality are properties of the
Field, not the Data Type itself.

Template packs render each Data Type to the equivalent host-language type. For
example, `Decimal` renders as `decimal` in the C# pack and `Decimal` in the
Python pack; `EntityReference` renders as the referenced entity's generated
name in both.

See [Readable Modelling Language](/docs/reference/readable-modelling-language)
for authoring syntax.

## Fact types

A Fact declares one of four types:

| Fact type | Purpose |
| --- | --- |
| `Truth` | A known true-or-false proposition |
| `Text` | A textual domain value |
| `Number` | A decimal domain value |
| `Date` | A calendar-date domain value |

These are declaration-level types, checked by the validation pipeline before a
Fact can bind to a rule or decision. They are semantic types, not C# storage
types — template packs decide how a validated Fact value is represented in
generated output (for example, `Truth` renders as `bool` and `Number` renders
as `decimal` in the C# pack).

### What the rules runtime evaluates today

The reference rules runtime (`reference/1.0`) currently evaluates only
`Truth`-typed Facts: rule expressions and decision-table conditions test a
Fact's truth value, and a supplied Fact whose runtime value is not
`TruthFactValue` is rejected with `runtime.request.fact-type-mismatch`. `Text`,
`Number`, and `Date` Facts can be declared and rendered into generated code
today, but are not yet consumable by rule or decision evaluation.

A decision's conclusion is a
[classification](/docs/concepts/ubiquitous-language#classification) — a
reference to one of the decision's declared
[conclusions](/docs/concepts/ubiquitous-language#conclusion) — represented at
runtime as `ClassificationFactValue`. This is a property of decision output,
not a fourth Fact type an author declares.

## Missing is not false

An absent Fact remains missing. Rule evaluation can therefore return an
[indeterminate result](/docs/concepts/ubiquitous-language#indeterminate-result)
with the required Fact IDs instead of coercing missing information to `false`,
an empty string, or a default classification.

## Type checking

The validation pipeline checks definition and reference compatibility before
runtime binding. The rules runtime then checks request values against the
bound Fact types. Invalid combinations produce stable diagnostics rather than
implicit conversions.

Decision-table conditions use typed truth values (`true`, `false`, or `any`) in
SAF 1.0. See [Semantic Assembly Format](/docs/reference/readable-source-language) for
syntax and [rules runtime](/docs/reference/rules-runtime) for evaluation results.
