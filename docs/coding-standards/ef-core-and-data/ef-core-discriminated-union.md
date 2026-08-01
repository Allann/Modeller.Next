---
title: "Model EF Core Discriminated Unions as Abstract Record Hierarchies, Not Language Union Types"
---

# Model EF Core Discriminated Unions as Abstract Record Hierarchies, Not Language Union Types


## The Standard

When a domain concept is a closed set of variants (e.g. an approval/payment/transfer status) and it must be persisted with EF Core, model it as an `abstract record` base type with `sealed`/derived `record` subtypes, and map the hierarchy directly with EF Core's Table-per-Hierarchy (TPH) `HasDiscriminator<T>` API. Do not model the variants with C#'s native `union` type shorthand when the union must round-trip through the database — EF Core cannot map a union type as an entity, forcing a manual shadow DTO and string-keyed conversion layer instead.

## Why

In the "before" state, the domain type `FourEyesApproval` was declared as a real C# discriminated union (`public union FourEyesApproval(NotRequired, PendingApproval, ...)`). This reads well in C#, but EF Core has no way to map a union type to a table, so the union had to be `entity.Ignore(x => x.Approval)`-d entirely. A parallel `FourEyesApprovalData` record (string `Discriminator` + a pile of nullable `Guid?` fields) was introduced purely as a persistence shim, with a hand-written `ToDataModel()`/`ToModel()` conversion layer wired through a private shadow property. Queries lost type safety and had to reach into shadow state by string: `EF.Property<FourEyesApprovalData>(t, "ApprovalData").Discriminator == "PartlyApproved"`.

In the "after" state, `FourEyesApproval` became a normal `abstract record` hierarchy. EF Core can map this natively: `modelBuilder.Entity<FourEyesApproval>()` with `.ToTable("Transfers")` and `.HasDiscriminator<string>("ApprovalType").HasValue<PartlyApproved>("PartlyApproved")...` puts all variants in one table with a discriminator column EF manages itself. This eliminated the manual conversion layer, the shadow property, and the stringly-typed shadow-state query — queries became `dbContext.Transfers.Include(t => t.Approval).FirstOrDefault(t => t.Approval is PartlyApproved)`, using real pattern matching against a real navigation property. The take-away: prefer the modeling approach EF Core has first-class support for over the more "modern"-looking language feature, when the type needs to be persisted.

## Before (Anti-pattern)

```csharp
// Domain type uses the C# union type shorthand — EF Core cannot map this.
public union FourEyesApproval(NotRequired, PendingApproval, PartlyApproved, FullyApproved, Rejected);

// So the entity ignores it and shims persistence via a parallel DTO + shadow property:
entity.Ignore(x => x.Approval);
// ...
private FourEyesApprovalData ApprovalData
{
    get => Approval.ToDataModel();   // manual switch-based conversion
    set => Approval = value.ToModel();
}

// Queries must bypass the type system and reach into shadow state by string:
var transfer = dbContext.Transfers
    .FirstOrDefault(t => EF.Property<FourEyesApprovalData>(t, "ApprovalData").Discriminator == "PartlyApproved");
```

## After (Standard)

```csharp
// Domain type is a plain abstract record hierarchy — EF Core maps this directly.
public abstract record FourEyesApproval;
public record NotRequired : FourEyesApproval;
public record PendingApproval : FourEyesApproval;
public record PartlyApproved(Guid Approver) : FourEyesApproval;
public record FullyApproved(Guid Approver1, Guid Approver2) : FourEyesApproval;
public record Rejected(Guid Rejector) : FourEyesApproval;

// TPH mapping, no manual conversion code needed:
modelBuilder.Entity<FourEyesApproval>(entity =>
{
    entity.ToTable("Transfers");
    entity.HasDiscriminator<string>("ApprovalType")
        .HasValue<PartlyApproved>("PartlyApproved")
        .HasValue<FullyApproved>("FullyApproved");
        // ...remaining variants
});

// Queries use real navigation + pattern matching:
var transfer = dbContext.Transfers
    .Include(t => t.Approval)
    .FirstOrDefault(t => t.Approval is PartlyApproved);
```

## Rules for LLMs / Agents

- When a closed set of variants must be persisted via EF Core, model it as an `abstract record` base type with derived `record` subtypes, not as a C# `union` type.
- Map the variant hierarchy with EF Core's native `HasDiscriminator<TDiscriminator>().HasValue<TVariant>(...)` (TPH), not with a hand-rolled DTO + string discriminator + manual `ToDataModel()`/`ToModel()` conversion layer.
- Never bypass the type system with `EF.Property<T>(entity, "ShadowPropertyName")` string lookups to query a variant type when a real mapped navigation/discriminator would let you write `entity.Variant is SomeVariant` directly in LINQ.
- If a type cannot be mapped by EF Core (e.g. a language union type used purely in memory, never persisted), that's fine to keep — but the moment persistence is required, switch the modeling to whatever EF Core has first-class support for, don't build a shim to force the unsupported shape through.
- Do not leave a superseded persistence-shim type (e.g. an old manual DTO/conversion class) in the codebase once it is replaced by direct EF Core mapping — delete the dead conversion code rather than leaving both approaches present.
- Prefer using `.Include(...)` plus C# pattern matching (`is SomeVariant variant`) over shadow-property/discriminator-string comparisons when filtering or projecting on a mapped variant hierarchy.

## When NOT to apply

If the discriminated-union value is never persisted to the database (pure in-memory domain logic, DTOs for an API response, etc.), a native C# `union` type or any other in-memory discriminated-union representation is fine — this standard only governs types that must round-trip through EF Core.
