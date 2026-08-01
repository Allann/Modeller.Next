---
title: "Avoiding Cartesian Explosion in EF Core Queries"
---

# Avoiding Cartesian Explosion in EF Core Queries


## The Standard

When an EF Core query uses `Include`/`ThenInclude` to load more than one collection navigation (either two sibling `Include`s on collections, or a chain of `ThenInclude` across nested collections), the query MUST use `AsSplitQuery()` to avoid a cartesian-product join that duplicates and multiplies row data.

## Why

A single SQL query that joins a parent to two or more child collections (or a multi-level chain of one-to-many relations) produces a cross-product of rows: parent columns are repeated once per combination of child rows across all included collections. This wastes bandwidth, inflates memory, and gets quadratically (or worse) worse as collection sizes grow. The benchmark in this material (`CartesianExplosionBenchmark.cs`) measures `Include+ThenInclude` and `Include+Include` scenarios with and without `AsSplitQuery()`, demonstrating the regular (single-query) form allocates and transfers substantially more data than the split-query form, which issues one SQL query per included collection instead.

## Before (Anti-pattern)

```csharp
// Single query joins Departments -> Teams -> Employees in one round trip,
// duplicating Department/Team columns for every Employee row.
var departments = await context
    .Departments
    .Include(d => d.Teams)
    .ThenInclude(t => t.Employees)
    .AsNoTracking()
    .Where(d => d.Id == id)
    .ToListAsync();
```

## After (Standard)

```csharp
// Two (or more) sibling/nested collection includes -> split into separate
// SQL queries to avoid the cartesian product.
var employees = await context
    .Employees
    .Include(e => e.Tasks)
    .Include(e => e.SalaryPayments)
    .AsNoTracking()
    .AsSplitQuery()
    .Where(e => e.Id == id)
    .ToListAsync();
```

## Rules for LLMs / Agents

- Always add `AsSplitQuery()` when a query has two or more `Include`/`ThenInclude` calls where more than one resolves to a collection navigation.
- A single `Include` for one collection navigation (with no further collection `ThenInclude`) does not need `AsSplitQuery()` — the standard applies specifically to multiple collection loads.
- Pair `AsSplitQuery()` with `AsNoTracking()` for read-only query endpoints, matching the pattern shown in every endpoint in the "after" sample.
- When in doubt about the shape of a query's includes, or when adding a new collection `Include` to an existing multi-include query, re-check whether `AsSplitQuery()` is now required.
- When performance of an EF Core query with multiple collection includes is in question, prefer writing/checking a BenchmarkDotNet `[MemoryDiagnoser]` benchmark comparing single vs. split query, mirroring `CartesianExplosionBenchmark.cs`.

## When NOT to apply

Do not use `AsSplitQuery()` for queries with a single collection include, or for includes that are all reference (non-collection) navigations — split queries add extra round trips and can introduce consistency issues (data changing between the split queries) that are unnecessary when there's no cartesian product risk.
