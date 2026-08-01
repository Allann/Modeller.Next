---
title: Definitions
description: The semantic definitions that make up a Modeller bounded context.
---

# Definitions

A [definition](/docs/concepts/ubiquitous-language#definition) is a named,
stably identified concept owned by a
[bounded context](/docs/concepts/ubiquitous-language#bounded-context). Definitions
are immutable values in the canonical model and are changed through typed model
operations.

## Definition kinds

| Kind | Meaning |
| --- | --- |
| [Fact](/docs/concepts/ubiquitous-language#fact) | Typed information supplied to rules and decisions |
| [Entity](/docs/concepts/ubiquitous-language#entity) | A domain concept with identity and, optionally, a lifecycle |
| [Lifecycle](/docs/concepts/ubiquitous-language#lifecycle) | The governed stages available to an entity |
| [Rule](/docs/concepts/ubiquitous-language#rule) | A typed expression producing a conclusion and findings |
| [Decision table](/docs/concepts/ubiquitous-language#decision-table) | Explicit conditions and conclusions governed by a hit policy |
| [Behaviour](/docs/concepts/ubiquitous-language#behaviour) | An action associated with an entity and its outcomes |
| [Outcome](/docs/concepts/ubiquitous-language#outcome) | A business result of a behaviour |
| [Effect](/docs/concepts/ubiquitous-language#effect) | An observable consequence of an outcome |
| [Event](/docs/concepts/ubiquitous-language#event) | A durable fact that something meaningful occurred |
| [Transition](/docs/concepts/ubiquitous-language#transition) | A lifecycle change produced by an outcome and constrained by guards |

Every concept has a [semantic ID](/docs/concepts/ubiquitous-language#semantic-id),
name, slug, documentation, and ownership. References use IDs rather than names,
so a rename does not silently change meaning.

## Authoring and changing definitions

Authors normally use [readable source](/docs/reference/readable-source-language).
The parser compiles it into an `AuthoredContextRevision` and retains provenance
for diagnostics and navigation. Programmatic callers use `CanonicalModel.Apply`
with typed operations such as `AddDefinition`, `RenameConcept`, and
`DeleteConcept`.

Definitions become executable only after
[semantic validation](/docs/reference/semantic-validation). Persistence uses the
canonical [context-package](/docs/reference/context-packages) representation.
