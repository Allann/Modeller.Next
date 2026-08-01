---
title: "Persist a C# Union Type in EF Core via a Shadow Complex Property"
---

# Persist a C# Union Type in EF Core via a Shadow Complex Property


## The Standard

If a domain concept is intentionally modeled with C#'s native `union` type (not an abstract record hierarchy), do not try to map the union directly as an EF Core entity/complex type. Instead: (1) `Ignore` the union property on the entity, (2) introduce a private, EF-mapped shadow property backed by a plain data record (`entity.ComplexProperty<TData>("ShadowName")`), and (3) write the union-to-data and data-to-union conversions once, next to the union, as `extension` members so every read/write of the domain property goes through them transparently via the entity's shadow property getter/setter.

## Why

The "before" state modeled `FourEyesApproval` as an abstract record hierarchy (`NotRequired`, `PendingApproval`, `PartlyApproved`, ...), which meant the entity's `OnModelCreating` had to `entity.Ignore(x => x.Approval)` — the value was never persisted at all. The "after" state switches the domain type to a real C# `union FourEyesApproval(NotRequired, PendingApproval, PartlyApproved, FullyApproved, Rejected)`, which is not a class EF Core can map on its own. The demo makes this persistable by adding a `FourEyesApprovalData` record (`Discriminator` + nullable fields for each variant's payload) and mapping it as a `ComplexProperty` on a private shadow property `ApprovalData` whose getter/setter call `Approval.ToDataModel()` / `data.ToModel()`. Because the conversion logic lives in `extension(FourEyesApproval approval) { ... }` / `extension(FourEyesApprovalData data) { ... }` blocks colocated with the types, callers never see the data model — `Transfer.Approval` remains a plain `FourEyesApproval` from the outside, and `dbContext.SaveChanges()` "just works".

## Before (Anti-pattern)

```csharp
// Domain type not persisted at all — EF Core can't map the hierarchy.
public abstract record FourEyesApproval;
public record PartlyApproved(Guid Approver) : FourEyesApproval;
// ...

modelBuilder.Entity<Transfer>(entity =>
{
    entity.Ignore(x => x.Approval);   // silently dropped from the database
});
```

## After (Standard)

```csharp
public union FourEyesApproval(NotRequired, PendingApproval, PartlyApproved, FullyApproved, Rejected);

public record FourEyesApprovalData(string Discriminator, Guid? Approver1, Guid? Approver2, Guid? Rejector);

public static class FourEyesApprovalConversions
{
    extension(FourEyesApproval approval)
    {
        public FourEyesApprovalData ToDataModel() => new(approval.Discriminator, approval.Approver1Data, approval.Approver2Data, approval.RejectorData);
        // ... private Discriminator / Approver1Data / etc. computed via switch expressions
    }

    extension(FourEyesApprovalData data)
    {
        public FourEyesApproval ToModel() => data.Discriminator switch
        {
            "PartlyApproved" => new PartlyApproved(data.Approver1!.Value),
            // ...
            _ => throw new InvalidOperationException($"Unknown approval type: {data.Discriminator}")
        };
    }
}

// Entity: union stays the public surface; the data record is a private implementation detail.
public class Transfer(Guid id, Guid from, Guid to, FourEyesApproval approval)
{
    public FourEyesApproval Approval { get; private set; } = approval;
    private FourEyesApprovalData ApprovalData
    {
        get => Approval.ToDataModel();
        set => Approval = value.ToModel();
    }
}

modelBuilder.Entity<Transfer>(entity =>
{
    entity.Ignore(x => x.Approval);
    entity.ComplexProperty<FourEyesApprovalData>("ApprovalData");
});
```

## Rules for LLMs / Agents

- Never let `entity.Ignore(x => x.SomeUnionProperty)` be the final state of a domain property that actually needs to be saved — treat an `Ignore`d property as a signal that a shadow-property mapping still needs to be added.
- When persisting a C# `union` type (or any type EF Core cannot map directly) put the conversion logic in `extension` members on the union type and its data-record counterpart, not scattered across repositories or services.
- Make the discriminator switch expressions in the conversion layer exhaustive; end every switch with a `throw new InvalidOperationException(...)` default arm naming the unrecognized value, never a silent fallback.
- Keep the persistence data record (e.g. `FourEyesApprovalData`) private/internal to the persistence layer — outward-facing code (domain logic, application services) must interact only with the union type, never the shadow data record.
- Name the shadow property distinctly from the domain property (e.g. `Approval` vs `ApprovalData`) so it's clear in the entity which one is the real domain surface and which is the EF Core plumbing.

## When NOT to apply

If the closed set of variants doesn't need a hand-rolled union type — i.e. you're free to choose the modeling approach — prefer an `abstract record` hierarchy mapped with EF Core's native TPH discriminator (`HasDiscriminator<T>()`) instead of this shadow-property workaround; it avoids maintaining a parallel data record and manual conversion layer entirely. Reach for the technique in this document only when the union type is otherwise the right domain shape and must still be persisted.
