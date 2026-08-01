---
title: "Domain Definition Language"
---

# Domain Definition Language

> **Status**: Implemented
>
> This specification describes the domain definition language for Modeller. We chose a **custom DSL** parsed with [Pidgin](https://github.com/benjamin-hodgson/Pidgin) parser combinators, rather than YAML (see [Format Options](09-format-options.md) for the rationale).

This specification defines a domain definition format designed for:

1. **Business readability** - Definitions read like documentation that business stakeholders can review
2. **Technology agnostic** - Describes *what* the business does, not *how* systems implement it
3. **AI agent friendly** - Structured for AI understanding and generation
4. **Code generation ready** - Translatable to multiple output formats

## Core Principles

### Separation of Concerns

| Concern | Belongs In | Does NOT Belong In |
|---------|------------|-------------------|
| **Domain Model** | Entity/Value Object definitions | Database IDs, technical keys |
| **Persistence** | Separate storage definitions | Domain definitions |
| **Interfaces** | Command/Query definitions | Entity internals |
| **Business Rules** | Rules engine (future) | Scattered in definitions |

### Domain Building Blocks

```
┌─────────────────────────────────────────────────────────────┐
│                         SERVICE                              │
│  (Bounded Context)                                          │
│                                                             │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────┐ │
│  │  ENTITIES   │  │   VALUE     │  │    REFERENCE        │ │
│  │             │  │   OBJECTS   │  │    DATA             │ │
│  │ - Identity  │  │ - No ID     │  │ - External          │ │
│  │ - Lifecycle │  │ - Immutable │  │ - Read-only         │ │
│  │ - Mutable   │  │ - Equality  │  │ - Not owned         │ │
│  └─────────────┘  └─────────────┘  └─────────────────────┘ │
│                                                             │
│  ┌─────────────────────────┐  ┌───────────────────────────┐│
│  │       COMMANDS          │  │         QUERIES           ││
│  │ (Business Actions)      │  │ (Business Questions)      ││
│  │ - Change things         │  │ - Retrieve information    ││
│  │ - Notify when done      │  │ - No changes made         ││
│  └─────────────────────────┘  └───────────────────────────┘│
└─────────────────────────────────────────────────────────────┘
```

## Implementation

The DSL is implemented with:

| Component | Technology | Location |
|-----------|------------|----------|
| **Parser** | [Pidgin](https://github.com/benjamin-hodgson/Pidgin) parser combinators | `src/Modeller.Parser/` |
| **Domain Models** | Immutable C# records with factory validation | `src/Modeller.Domain/` |
| **VS Code Extension** | TextMate grammar + custom icons | `editors/vscode-modeller/` |
| **Sample Domain** | Full example definitions | `samples/modeller/` |

## Documents

| Document | Description |
|----------|-------------|
| [Overview](01-overview.md) | Philosophy and design goals |
| [Domain Concepts](02-domain-concepts.md) | Entities, Value Objects, Shared Data |
| [Behaviours](03-behaviours.md) | Commands, Queries, Workflows, and Events |
| [File Structure](04-file-structure.md) | Project organization |
| [Examples](05-examples.md) | Concrete definition examples |
| [AI Integration](06-ai-integration.md) | How AI agents consume and generate definitions |
| [Data Types](07-data-types.md) | Type system reference |
| [Glossary](08-glossary.md) | Key terms and definitions |
| [Format Options](09-format-options.md) | Why we chose custom DSL over YAML |
| [Templates](10-templates.md) | Code generation templates and engines |
| [Implementation Status](11-implementation-status.md) | Current build status and next steps |

## Design Goals

1. **Business-first** - Technical concerns (storage, interfaces) are separate
2. **Natural language** - Reads like English, understandable by non-technical stakeholders
3. **AI-friendly** - Clear semantics for AI understanding and generation
4. **Version-aware** - Support for evolution over time
5. **Composable** - Services can reference other services' data
