---
title: Architecture concepts
description: How Modeller preserves domain meaning through explicit module seams.
---

# Architecture concepts

Modeller separates durable domain meaning from source formats, generated code,
editors, and external systems. The [canonical model](/docs/reference/canonical-model)
is the authority shared by every workflow.

## Core boundaries

| Boundary | Responsibility | Current reference |
| --- | --- | --- |
| Model | Stable identities, definitions, references, and typed operations | [Canonical model](/docs/reference/canonical-model) |
| Contexts | Canonical persistence, package identity, imports, and federation | [Context packages](/docs/reference/context-packages) |
| Parsing | Compile source and preserve source provenance | [Readable Modelling Language](/docs/reference/readable-modelling-language), [SAF](/docs/reference/readable-source-language) |
| Validation | Ordered structural, reference, type, lifecycle, and policy checks | [Semantic validation](/docs/reference/semantic-validation) |
| Rules | Bind and evaluate rules and decision tables with explanations | [Rules runtime](/docs/reference/rules-runtime) |
| Projections | Derive behavioural, lifecycle, causality, context, structural, and rule views | [Diagram projections](/docs/reference/diagram-projections) |
| Generation | Create deterministic proposed artifacts | [Generation plans](/docs/reference/generation-plans) |
| Rendering | Render planned artifacts through bounded adapters | [Template rendering](/docs/reference/template-rendering) |
| Output | Preview or atomically apply manifest-owned changes | [Output application](/docs/reference/output-application) |
| Integrations | Present the same contracts through CLI and editor workflows | [CLI](/docs/reference/modeller-cli), [editor](/docs/reference/editor-integration) |

## Architectural rules

- Source syntax compiles to the canonical model; it is not another domain model.
- Validation and evaluation are explicit, deterministic operations.
- Diagrams are projections of model meaning, never an independent authority.
- Generation planning is pure; rendering and filesystem effects sit behind adapters.
- Generated files require manifest-proven ownership before Modeller may replace them.
- Integrations orchestrate modules without redefining their semantics.

See [Architecture 101](/docs/architecture/architecture-101) for the full flow and
the [decision records](/docs/architecture/decisions) for design rationale.
