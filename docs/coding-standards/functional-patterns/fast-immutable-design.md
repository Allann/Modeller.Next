---
title: "Fast Immutable Design"
---

# Fast Immutable Design


## The Standard

An immutable type MUST be built from plain, ordinary storage (arrays, `List<T>`, etc.) that is fully populated once at construction and never mutated afterward — do NOT reach for specialized persistent/immutable collection types (`ImmutableList<T>`, `ImmutableArray<T>`, etc.) "because the type is immutable." Immutability is a discipline enforced by never exposing a mutation path after construction, not a property that requires a particular collection implementation.

## Why

The `01-initial` demo modeled `WorkItem.Prerequisites` as `ImmutableList<WorkItem>` and added a `WithPrerequisite` extension method that produced a new `WorkItem` via `with { Prerequisites = ... Add(...) }`. This is the classic misunderstanding: it assumes an immutable record needs an immutable *collection* so that "updates" can be done efficiently and safely. But nothing in the actual usage (`Program.cs`) ever incrementally appends a prerequisite to an already-published `WorkItem` — every `WorkItem` is assembled bottom-up with its complete, final prerequisite array passed straight into the constructor (`WorkItem.Create(label, estimate, [dep1, dep2, ...])`). The `WithPrerequisite`/`ImmutableList` machinery was solving a problem (repeated persistent updates producing many versions) that the code never actually has, at the cost of `ImmutableList<T>`'s O(log n) tree-node overhead on every read and write.

`02-final` deletes that machinery entirely: `WorkItem.Prerequisites` becomes a plain `WorkItem[]`, and the whole `WorkItemConstruction`/`WorkPlanning` incremental-update surface is removed along with it. The record is still fully immutable in practice — the array is populated once at construction and the type never exposes a way to mutate it afterward — but reads are now flat O(1) array access instead of O(log n) tree traversal, and there is far less code. Immutability came from encapsulation and construction discipline, not from the collection type.

## Before (Anti-pattern)

```csharp
public record WorkItem(Guid Id, string Label, TimeSpan Estimate, ImmutableList<WorkItem> Prerequisites);

public static class WorkItemConstruction
{
    extension(WorkItem workItem)
    {
        public static WorkItem Create(string label, TimeSpan estimate, WorkItem[] prerequisites) =>
            new WorkItem(Guid.NewGuid(), label, estimate, prerequisites.ToImmutableList());

        // "Efficient" incremental update nobody in the codebase actually calls
        public WorkItem WithPrerequisite(WorkItem prerequisite) =>
            workItem with { Prerequisites = workItem.Prerequisites.Add(prerequisite) };
    }
}
```

## After (Standard)

```csharp
// Prerequisites are always known in full at construction time (see call sites:
// WorkItem.Create(label, estimate, [dep1, dep2, ...])) — there is no incremental
// "add one more prerequisite to an existing WorkItem" use case to optimize for.
public record WorkItem(Guid Id, string Label, TimeSpan Estimate, WorkItem[] Prerequisites);
```

## Rules for LLMs / Agents

- Before reaching for `ImmutableList<T>`, `ImmutableArray<T>`, `ImmutableDictionary<TKey,TValue>`, etc., check whether the code ever needs to derive a new version from an existing published instance. If every instance is fully assembled once at construction and never "updated," use the plain mutable collection type (`T[]`, `List<T>`, `Dictionary<TKey,TValue>`) instead.
- Do not add a `With*`/`Add*` builder-style method to a record "for immutability" unless a real call site actually needs to derive a new version of an already-published instance from an existing one.
- Treat immutability as a construction/encapsulation contract: populate the backing storage fully in the constructor (or a factory), never expose a member that mutates it afterward, and never leak a mutable reference to the backing storage that a caller could mutate.
- Prefer flat, O(1)-access storage (arrays/lists) over tree-based persistent collections unless the design genuinely requires cheap structural sharing across many concurrently-alive versions of the same logical object.
- When reviewing or writing a record/type described as "immutable," verify the actual usage pattern (how call sites construct and consume it) before choosing the backing collection type — do not default to an immutable-collection type out of habit.

## When NOT to apply

If the domain genuinely requires deriving many new versions from a shared base and needs to avoid O(n) copy cost on each derivation (e.g., undo/redo history, concurrent readers walking a structure while writers produce new versions, or structural sharing across a large number of live snapshots), a persistent/immutable collection is the right tool — the standard is to not use one by default, not to never use one.
