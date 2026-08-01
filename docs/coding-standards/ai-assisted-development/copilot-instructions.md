---
title: "Write Concise, Rule-Based AI Coding Instructions"
---

# Write Concise, Rule-Based AI Coding Instructions


## The Standard

AI-facing coding-style instruction files must be short, declarative rule lists grouped by concern (namespaces, immutability, file organization, type design), scoped with front matter (`applyTo`) to the file types they govern, and phrased as direct imperatives ("Use", "Prefer", "Never", "Always") — not prose explanations, tutorials, or rationale essays.

## Why

The "before" state of this repo had no `.github/instructions` file at all — style was tribal knowledge, so an AI assistant (or a new contributor) had no way to reliably reproduce the author's preferences (file-scoped namespaces, one type per file, factory-method-driven record construction, discriminated unions as records). The "after" state adds `.github/instructions/style-guidelines.instructions.md` with YAML front matter (`applyTo: '**/*.cs'`) restricting scope, and a small number of terse bullet rules under `##` headings. Each rule is independently checkable/testable (e.g. "Define one type per file", "Never use record's constructor when there is a factory method") rather than a paragraph of reasoning — this is what makes it usable as a literal checklist an LLM can follow mechanically, which is exactly the property this project's own `docs/coding-standards` pages need.

## Before (Anti-pattern)

```markdown
<!-- No instructions file exists; style lives only in the author's head or scattered PR comments. -->
```

## After (Standard)

```markdown
---
applyTo: '**/*.cs'
---
Coding standards, domain knowledge, and preferences that AI should follow.

## Namespaces
- Use file-scoped namespaces that match the folder structure.

## Record Design
- Accompany each record `<name>` with `<name>Factory` static factory class.
- Expose static `Create` method in the factory class for instantiation.
- Place argument validation in the `Create` method.
- Never use record's constructor when there is a factory method.

## Discriminated Unions Design
- Prefer using records for discriminated unions.
- Derive specific types from a base abstract record.
- Define the entire discriminated union in one file.
```

## Rules for LLMs / Agents

- Write AI/agent-facing coding-standards files as short bullet lists under `##` topic headings, not narrative prose.
- Scope instruction files with explicit front matter (e.g. `applyTo: '**/*.cs'`) so tools only apply them where relevant.
- Phrase every rule as an imperative and, where possible, testable/checkable statement ("Define one type per file"), not a suggestion or explanation.
- Group related rules under a small number of named concerns (Namespaces, Immutability, File Organization, Record Design, etc.) rather than one flat list.
- Keep each instructions file focused on one coherent topic; split unrelated concerns into separate files rather than growing one file indefinitely.
- Avoid restating language/framework documentation the model already knows — only encode the project's specific deviations/preferences.

## When NOT to apply

None observed — the source material treats this format as the ideal answer to "can Copilot learn my coding style?" and does not describe cases where longer prose explanations would be preferable in an instructions file.
