---
title: "Model Ordered Work Queues with PriorityQueue<T> and Immutable Records, Not Manual Sorting and Mutable State"
---

# Model Ordered Work Queues with `PriorityQueue<T>` and Immutable Records, Not Manual Sorting and Mutable State


## The Standard

When code needs to process items in priority order, use the BCL's `PriorityQueue<TElement, TPriority>` with a small, pure, immutable comparison key instead of hand-rolling ordering logic over a mutable list. Represent per-item state (tasks, orders, work items) as immutable `record`/`record struct` types updated via `with` expressions, and decompose the driving loop into small, single-purpose local functions that each guard-clause their way out early rather than nesting conditionals or mutating shared fields inline.

## Why

The "01-Initial" starting point is a bare stub: it just enumerates a fixed list of books and prints their titles, with the actual scheduling/priority logic left as commented-out scaffolding (`BookPriorities`, `GetPriority`, `Report`). The "02-Final" version builds out that logic — a day-by-day work scheduler that must always work on the highest-priority, oldest, earliest-arrived task, and must preempt the current task when a `Critical` item arrives. Doing this by hand (re-sorting a `List<T>` on every change, tracking priority with loose fields, mutating a shared "current task" object property-by-property across branches) is exactly the kind of imperative, stateful, hard-to-follow code the video contrasts with a more functional approach. The final version instead:

- Delegates the actual ordering/dequeue-highest-priority mechanics to `PriorityQueue<QueueItem, SortOrder>`, so the scheduler never manually sorts or scans a list.
- Encodes "what makes one item outrank another" as a single pure `CompareTo` on an immutable `record struct SortOrder`, not as scattered `if` checks.
- Represents work-in-progress as an immutable `WorkItem` record and advances it with `currentTask with { WorkDays = currentTask.WorkDays - 1 }` instead of mutating a `WorkDays` field.
- Splits the day's transitions into named guard-clause functions (`CompleteCurrentTask`, `AcceptIncomingItems`, `PreemptCurrentTask`, `StartNewTask`, `WorkOnCurrentTask`) that each check one precondition and return early, keeping the top-level loop a flat, readable list of steps.

This keeps the only true mutable state to the minimum the simulation loop actually needs (`currentTask`, `currentSortOrder`, `sequence`), while pushing everything else — ordering, comparison, and per-step state transitions — into pure, immutable, declarative constructs.

## Before (Anti-pattern)

```csharp
// 01-Initial/Demo/Program.cs — no real scheduling logic yet;
// priority/ordering concerns are left as ad-hoc mutable scaffolding
// commented out in ConsoleUtilities.cs rather than modeled directly:

// private static Priority[] BookPriorities { get; } = [ ... ];
//
// private static Priority GetPriority(this Book book) =>
//     IncomingBooks.Zip(BookPriorities, (book, priority) => (book, priority))
//         .First(item => item.book == book).priority;

foreach (var book in IncomingBooks)
{
    Console.WriteLine(book.Title);
}
```

## After (Standard)

```csharp
// 02-Final/Demo/Program.cs
var workQueue = new PriorityQueue<QueueItem, SortOrder>();

void StartNewTask()
{
    if (currentTask is not null || workQueue.Count == 0) return;
    (currentTask, currentSortOrder) = workQueue.Dequeue();
}

void WorkOnCurrentTask()
{
    if (currentTask is null) return;
    currentTask = currentTask with { WorkDays = currentTask.WorkDays - 1 };
}

record struct SortOrder(Priority Priority, DateOnly DateArrived, long Sequence) : IComparable<SortOrder>
{
    public int CompareTo(SortOrder other) =>
        Priority != other.Priority ? Priority.CompareTo(other.Priority)
        : DateArrived != other.DateArrived ? DateArrived.CompareTo(other.DateArrived)
        : Sequence.CompareTo(other.Sequence);
}
```

## Rules for LLMs / Agents

- Use `System.Collections.Generic.PriorityQueue<TElement, TPriority>` for any "process by priority/order" scenario; do not hand-write sorting or scanning over a `List<T>` to find the next item to process.
- Encode ordering rules as a single `IComparable<T>` implementation on a small immutable `record`/`record struct` priority key, not as `if`/`switch` logic scattered across call sites.
- Represent per-item mutable-looking state (progress, remaining work, counters tied to one task) as an immutable `record` and advance it with `with` expressions, never by mutating fields/properties in place.
- Decompose a stateful control loop into named local functions, each with an early-return guard clause for its one precondition, instead of one large function with nested `if`/`else`.
- Keep genuinely necessary loop-driving mutable variables (the current item, an enumerator, a running counter) explicit and minimal — do not smuggle additional mutable state into fields "just in case."
- Do not resurrect commented-out ad-hoc priority arrays/lookups (e.g., a `Priority[]` zipped against another list); model the association directly on the data (e.g., as part of the item/record itself or via the comparison key).

## When NOT to apply

Domain entities that are legitimately mutable (e.g., EF Core-style aggregate roots like `Book` in this same sample, which retain settable properties for persistence/ORM concerns) are not part of this standard — immutability applies to the transient scheduling/ordering state modeled in the workflow, not necessarily to every persisted entity in the system.
