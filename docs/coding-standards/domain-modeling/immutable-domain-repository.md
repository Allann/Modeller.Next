---
title: "Model EF Core Aggregates as Immutable Types, Diff-Reconcile Them at the Persistence Boundary"
---

# Model EF Core Aggregates as Immutable Types, Diff-Reconcile Them at the Persistence Boundary


## The Standard

Domain aggregates persisted through EF Core must be modeled as immutable types — constructors and `With*`-style copy methods instead of public setters — even though EF Core's change tracker expects mutable, settable entities. Bridge the two by keeping mutation entirely inside a small, reusable persistence-layer component (a `IImmutableEntityRepository<T>`-style abstraction) that: (1) always eager-loads the full aggregate graph on read, (2) reconciles an updated immutable copy against the tracked original by diffing navigation collections/references on a stable key and applying the correct `EntityState` per child, and (3) never requires the domain model itself to expose a setter for EF Core's benefit.

## Why

EF Core's change tracking is built around mutable entities: it expects you to load an entity, mutate its properties/collections in place, and call `SaveChanges()`. That pushes mutability into the domain model purely to satisfy the ORM, even when the domain is better expressed immutably (constructors that validate invariants, `with`-style updates that can never produce a half-constructed or invalid object, thread-safety by default). This library's approach lets the domain stay immutable — `Invoice` in the demo has `init`-only properties, a private copy constructor, and `With*` methods (`WithCustomerName`, `WithLines`, ...) that return a new instance — while `ImmutableUpdateExtensions.UpdateImmutable` does the translation work: it finds the currently-tracked entity by a reflection-derived stable key (falling back to alternate/natural keys, not just the primary key), detaches it, attaches the new immutable copy as `Modified`, then walks each navigation (`CollectionEntry`/`ReferenceEntry`) and diffs the modified aggregate's children against the tracked ones by key — children present in both become recursively `UpdateImmutable`-d, children only in the new copy become `Added`, and children only in the old tracked graph are `Remove`d. This is meaningfully different from "just call `.Update()`," which would blindly mark the whole graph `Modified`/overwrite children rather than correctly detecting adds/removals within a collection navigation.

## Before (Anti-pattern)

```csharp
// Domain model mutability driven purely by EF Core's needs.
public class Invoice
{
    public string CustomerName { get; set; }              // public setter exists only for EF Core
    public List<InvoiceLine> Lines { get; set; } = new();  // mutable collection, no invariant protection
}

// Update requires manually loading, mutating in place, and hoping child collection diffing is correct:
var invoice = await dbContext.Invoices.Include(i => i.Lines).FirstAsync(i => i.PublicId == id);
invoice.CustomerName = "Sleepy Sam";
invoice.Lines.Add(new InvoiceLine("Invisibility Cloak", 1, new Money(99.99m, usd)));
await dbContext.SaveChangesAsync();
```

## After (Standard)

```csharp
// Domain model stays immutable — no setters, only constructors and With* copy methods.
public class Invoice(InvoiceNumber number, string customerName, DateOnly invoicedOn, InvoiceStatus status, Currency currency)
{
    public string CustomerName { get; init; } = customerName;
    public ImmutableList<InvoiceLine> Lines { get; init; } = ImmutableList<InvoiceLine>.Empty;

    public Invoice WithCustomerName(string customerName) => new(this) { CustomerName = customerName };
    public Invoice WithLines(ImmutableList<InvoiceLine> lines) => new(this) { Lines = lines };
}

// DbContext exposes a repository, not a raw DbSet, for immutable aggregate access:
public IImmutableEntityRepository<Invoice> Invoices => Set<Invoice>().ToImmutableEntityRepository(this, "Lines");

// Application code reads, derives a new immutable value, and hands it back — no in-place mutation:
var current = await dbContext.Invoices.FindImmutableAsync(invoice.PublicId);
var updated = current!.WithCustomerName("Sleepy Sam")
    .WithLines(current.Lines.Add(new InvoiceLine("Invisibility Cloak", 1, new Money(99.99m, usd))));

dbContext.Invoices.UpdateImmutable(updated);
await dbContext.SaveChangesAsync();
```

## Rules for LLMs / Agents

- Do not add public setters to a persisted domain entity's properties solely to satisfy EF Core — model the entity with `init`-only properties, a private constructor for EF materialization, and `With*` copy methods for changes.
- Route all reads and writes of an immutable aggregate through a dedicated repository abstraction (e.g. `IImmutableEntityRepository<T>`) rather than exposing the raw `DbSet<T>` for ad hoc `Include`/mutate/`SaveChanges` sequences at each call site.
- Ensure the repository's read path always eager-loads the complete aggregate graph (declare the include paths once, centrally, when building the repository) so every consumer gets a fully-populated aggregate rather than each call site declaring its own `.Include(...)` chain.
- When reconciling an updated immutable copy against a tracked original, diff child collections/references by a stable key (not by reference identity, and not primary-key-only if a natural/alternate key exists) so unchanged children stay `Unchanged`, new children become `Added`, and removed children are `Remove`d — never blanket-mark an entire graph as `Modified`.
- Keep the diff/reconciliation logic in one shared, tested library component, not duplicated per aggregate type — new aggregates should be able to opt in by exposing a repository, without writing bespoke update logic per entity.

## When NOT to apply

For simple, single-table entities with no meaningful invariants or child collections (pure lookup/reference data), plain mutable EF Core entities with straightforward `Update()` calls are simpler and this pattern's overhead (stable-key reflection, graph diffing) is not justified.
