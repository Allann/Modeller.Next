---
title: Data types
description: The semantic value types used by facts, rules, and decisions.
---

# Data types

Modeller types domain information before it reaches a rule or decision. In
language 1.0, a [Fact](/docs/concepts/ubiquitous-language#fact) has one of these
semantic types:

| Type | Purpose | Runtime value |
| --- | --- | --- |
| `Truth` | A known true-or-false proposition | `TruthFactValue` |
| `Text` | A textual domain value | `TextFactValue` |
| `Classification` | A stable reference to a named classification | `ClassificationFactValue` |

These are semantic types, not C# storage types. Template packs decide how a
validated semantic value is represented in generated output.

## Missing is not false

An absent Fact remains missing. Rule evaluation can therefore return an
[indeterminate result](/docs/concepts/ubiquitous-language#indeterminate-result)
with the required Fact IDs instead of coercing missing information to `false`,
an empty string, or a default classification.

## Type checking

The validation pipeline checks definition and reference compatibility before
runtime binding. The rules runtime then checks request values against the bound
Fact types. Invalid combinations produce stable diagnostics rather than implicit
conversions.

Decision-table conditions use typed truth values (`true`, `false`, or `any`) in
language 1.0. See [readable source](/docs/reference/readable-source-language) for
syntax and [rules runtime](/docs/reference/rules-runtime) for evaluation results.
