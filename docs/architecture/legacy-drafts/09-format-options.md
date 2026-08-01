---
title: "Format Options"
---

# Format Options

> **Decision**: We chose the **Custom DSL** approach (Option 3), implemented with [Pidgin](https://github.com/benjamin-hodgson/Pidgin) parser combinators. See the rationale below.

This document discusses the trade-offs between different formats for writing domain definitions.

## Options

### 1. YAML

```yaml
entity: Booking
description: Planned attendance for a child

attributes:
  Date:
    type: date
    description: When attendance is planned
    
  Status:
    type: BookingStatus

belongs_to: Child
```

**Pros:**
- Industry standard, widely understood
- Good tooling (IDE support, linters, validators)
- AI models trained extensively on YAML
- JSON Schema validation available
- Easy to parse in any language

**Cons:**
- Whitespace sensitivity causes frustration
- Verbose for simple definitions
- Error messages can be cryptic
- Indentation errors are common

---

### 2. JSON

```json
{
  "entity": "Booking",
  "description": "Planned attendance for a child",
  "attributes": {
    "Date": {
      "type": "date",
      "description": "When attendance is planned"
    }
  }
}
```

**Pros:**
- Universal support
- Strict syntax, clear errors
- No whitespace issues
- Easy to validate with JSON Schema

**Cons:**
- Very verbose (quotes, braces everywhere)
- Hard to read for larger definitions
- No comments allowed
- Poor for human authoring

---

### 3. Custom DSL (Evolved from existing format)

Based on your existing `/definition.old` format:

```
entity Booking
  description "Planned attendance for a child"

  attributes
    Date: date
      description "When attendance is planned"

    Status: BookingStatus

  belongs_to Child
end
```

**Pros:**
- Complete control over syntax
- Can be optimised for readability
- Natural language friendly
- No whitespace sensitivity (explicit `end` keywords)
- Better error messages
- Domain-specific tooling

**Cons:**
- Learning curve for new contributors
- No existing IDE support (would need to build)
- AI may need examples to generate correctly

#### Parser Implementation: Pidgin

For implementing a custom DSL, we recommend using [Pidgin](https://github.com/benjamin-hodgson/Pidgin) - a lightweight, fast parser combinator library for C#.

**Why Pidgin:**

| Feature | Benefit |
|---------|---------|
| **Parser combinators** | Build complex parsers from simple, composable pieces |
| **High performance** | Competitive with hand-written recursive descent parsers |
| **Low allocation** | Designed to minimise garbage collection pressure |
| **Pure C#** | No code generation step, works with standard tooling |
| **Active maintenance** | Regularly updated, v3.5.1 released Oct 2025 |
| **Expression parsing** | Built-in support for operator-precedence parsing |
| **Good error messages** | Includes source position tracking |

**Example - Parsing an entity definition:**

```csharp
using Pidgin;
using static Pidgin.Parser;
using static Pidgin.Parser<char>;

// Parse: entity Booking
var entityHeader = String("entity")
    .Then(Whitespaces)
    .Then(Identifier)
    .Select(name => new EntityNode(name));

// Parse: description "some text"
var description = String("description")
    .Then(Whitespaces)
    .Then(QuotedString);

// Compose into full entity parser
var entity = entityHeader
    .Before(Whitespaces)
    .Then(description.Optional())
    .Before(String("end"));
```

**Pidgin vs Alternatives:**

| Library | Speed | Streaming | Arbitrary Tokens | C# Native |
|---------|-------|-----------|------------------|-----------|
| **Pidgin** | Fast | Yes | Yes | Yes |
| Sprache | Slower | No | No | Yes |
| FParsec | Fastest | Yes | No | No (F#) |
| ANTLR | Fast | No | Yes | Generated |

**Installation:**
```bash
dotnet add package Pidgin
```

**Resources:**
- [Documentation](https://www.benjamin.pizza/Pidgin/)
- [GitHub](https://github.com/benjamin-hodgson/Pidgin)
- [Tutorial: Parsing Prolog](https://www.benjamin.pizza/Pidgin/)

---

### 4. Markdown-based

```markdown
# Entity: Booking

Planned attendance for a child

## Attributes

| Name | Type | Description |
|------|------|-------------|
| Date | date | When attendance is planned |
| Status | BookingStatus | Current state |

## Relationships

- **belongs_to**: Child
```

**Pros:**
- Extremely readable
- Renders nicely in GitHub, wikis, etc.
- Business stakeholders can read/edit
- No syntax to learn

**Cons:**
- Harder to parse reliably
- Tables get unwieldy for complex definitions
- Mixing documentation with definition
- Less structured

---

## Recommendation: Hybrid Approach

Consider a two-layer approach:

### Authoring Layer (Custom DSL)
Human-friendly syntax for writing definitions:

```
entity Booking
  "Planned attendance for a child"
  
  Date: date "When attendance is planned"
  Status: BookingStatus
  
  belongs_to Child
end

command RecordAttendance
  "Records a child's arrival"
  
  involves
    Booking: accessed through
    Attendance: creates
  
  input
    Booking: Booking
    TimeIn: time
  
  publishes AttendanceRecorded
end
```

### Processing Layer (YAML/JSON)
DSL compiles to YAML/JSON for:
- Validation (JSON Schema)
- AI consumption
- Code generation
- Integration with other tools

### Implementation Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                    AUTHORING (Human/AI)                         │
│                                                                 │
│  ┌─────────────┐      ┌─────────────┐      ┌─────────────────┐ │
│  │  DSL File   │      │    IDE      │      │   AI Agent      │ │
│  │ (.entity)   │      │  Extension  │      │   Generation    │ │
│  └──────┬──────┘      └──────┬──────┘      └────────┬────────┘ │
└─────────┼────────────────────┼─────────────────────┼───────────┘
          │                    │                     │
          ▼                    ▼                     ▼
┌─────────────────────────────────────────────────────────────────┐
│                    PARSING (Pidgin)                             │
│                                                                 │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │              Domain Definition Parser                    │   │
│  │         (Parser combinators built with Pidgin)           │   │
│  └─────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
          │
          ▼
┌─────────────────────────────────────────────────────────────────┐
│                 PROCESSING (Intermediate Format)                │
│                                                                 │
│  ┌─────────────┐      ┌─────────────┐      ┌─────────────────┐ │
│  │    YAML     │      │    JSON     │      │   In-Memory     │ │
│  │   Export    │      │   Export    │      │     Model       │ │
│  └──────┬──────┘      └──────┬──────┘      └────────┬────────┘ │
└─────────┼────────────────────┼─────────────────────┼───────────┘
          │                    │                     │
          ▼                    ▼                     ▼
┌─────────────────────────────────────────────────────────────────┐
│                    OUTPUT (Code Generation)                     │
│                                                                 │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────────────┐│
│  │   C#     │  │   SQL    │  │   API    │  │  Documentation  ││
│  │   Code   │  │  Schema  │  │  Specs   │  │                 ││
│  └──────────┘  └──────────┘  └──────────┘  └──────────────────┘│
└─────────────────────────────────────────────────────────────────┘
```

### Benefits
- Best of both worlds
- Clean separation of concerns
- Existing tooling for processing layer
- Custom, natural experience for authoring
- Consistent parsing via Pidgin ensures format stability
- Single source of truth for grammar definition

---

## Implementation Status ✅

We implemented the **Custom DSL** approach. The "hybrid" YAML/JSON processing layer described earlier in this document was considered but not built — the DSL feeds directly into the Scriban generation pipeline without an intermediate serialisation step.

| Component | Status | Notes |
|-----------|--------|-------|
| **Pidgin Parser** | ✅ Complete | All 12 file types supported |
| **Domain Models** | ✅ Complete | Immutable records with factory validation |
| **Scriban Templates** | ✅ Complete | External `.scriban` files; no recompilation needed |
| **VS Code Extension** | ✅ Complete | Syntax highlighting + custom icons |
| **Sample Definitions** | ✅ Complete | The canonical reference project is in `samples/child-care/` |

### Decisions Made

1. **Primary audience**: Developers author definitions; business stakeholders review them
2. **AI interaction**: AI works with the DSL directly (structured grammar is AI-friendly)
3. **Existing format**: Clean break — new DSL designed for domain modelling
4. **IDE support**: VS Code extension with syntax highlighting and file icons
