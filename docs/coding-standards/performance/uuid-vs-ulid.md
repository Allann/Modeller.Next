---
title: "Choosing Primary Key Types: UUID vs ULID vs Clustered Surrogate"
---

# Choosing Primary Key Types: UUID vs ULID vs Clustered Surrogate


**Note:** This folder is a single BenchmarkDotNet reference project (`SQL_Insert_Benchmarks`) comparing insert/index performance across primary-key strategies, not a before/after refactor. The standard below is extracted directly from that reference.

## The Standard

When a table's primary key is used as the clustered index, do not use a randomly-generated `Guid` (UUID v4) as the clustering key. Prefer a sortable/monotonic key — either a `Ulid` (time-ordered, 128-bit, still globally unique) for the primary key, or a random `Guid` primary key with the clustered index moved onto a genuinely sequential column (e.g. `CreatedOnUtc`) instead.

## Why

This benchmark project models exactly this decision: `Table2Guid` uses a random `Guid` as a clustered key, `Table3Ulid`/`Table4UlidBinary` use `Ulid` (converted to string or to a 16-byte array via `ValueConverter`), and `Table5DateTime` keeps a random `Guid` primary key but makes it a *non-clustered* key (`IsClustered(false)`) while clustering on `CreatedOnUtc` instead, which has a DB-generated default (`GETUTCDATE()`) and is naturally monotonic. A random `Guid` clustering key causes non-sequential inserts into a B-tree, producing page splits and index fragmentation as rows land in effectively random leaf-page positions — this degrades bulk-insert throughput and increases index size/fragmentation over time. `Ulid` encodes a timestamp prefix, so `Ulid.NewUlid()` values sort roughly in creation order, giving mostly-sequential inserts like an identity/int column while remaining globally unique and not requiring a DB round trip to generate. The `index_stats_query.sql` script queries `sys.dm_db_index_physical_stats` specifically to compare fragmentation across these table strategies.

## Before (Anti-pattern)

```csharp
// Random GUID as the clustered primary key - inserts land at random points
// in the B-tree, causing fragmentation and page splits under load.
modelBuilder.Entity<Table2Guid>(b =>
{
    b.HasKey(p => p.Id);
    b.Property(p => p.Id).ValueGeneratedNever();
});
// Id = Guid.NewGuid()
```

## After (Standard)

```csharp
// Option A: use a time-sortable ULID as the primary/clustered key.
modelBuilder.Entity<Table3Ulid>(b =>
{
    b.HasKey(p => p.Id);
    b.Property(p => p.Id).ValueGeneratedNever().HasConversion<UlidToStringConverter>();
});
// Id = Ulid.NewUlid()

// Option B: keep a random GUID as a (non-clustered) surrogate key, but
// cluster the table on a genuinely sequential column instead.
modelBuilder.Entity<Table5DateTime>(b =>
{
    b.HasKey(p => p.Id).IsClustered(false);
    b.Property(p => p.Id).ValueGeneratedNever();
    b.Property(p => p.CreatedOnUtc).HasDefaultValueSql("GETUTCDATE()");
    b.HasIndex(p => p.CreatedOnUtc).IsClustered();
});
```

## Rules for LLMs / Agents

- Never use `Guid.NewGuid()` (random UUID v4) as a clustered primary key for a table expected to receive frequent inserts at scale.
- When a globally-unique, application-generated, sortable id is needed, prefer `Ulid`/`Ulid.NewUlid()` over a random `Guid` for the clustered key.
- If a random `Guid` is required as the public/external identifier (e.g. for API contracts), make it a non-clustered unique key and cluster the table on a naturally sequential column (identity, sequence, or a DB-generated timestamp).
- When storing a `Ulid` in EF Core, use an explicit `ValueConverter` (string or fixed-size byte array) and size hints matching the chosen representation, mirroring `UlidToStringConverter`/`UlidToBytesConverter`.
- Validate primary-key-type decisions for hot-insert tables with an actual benchmark (BenchmarkDotNet insert benchmark plus `sys.dm_db_index_physical_stats`/equivalent fragmentation check) rather than assuming.

## When NOT to apply

Low-insert-volume or small tables where index fragmentation is never a practical concern can safely use random `Guid` primary keys for simplicity. This tradeoff is specifically about clustered-index insert patterns at scale, not about `Guid` usage in general.
