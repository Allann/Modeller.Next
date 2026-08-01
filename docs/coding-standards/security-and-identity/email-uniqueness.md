---
title: "Enforcing Email Uniqueness"
---

# Enforcing Email Uniqueness


## The Standard

Uniqueness constraints on business-critical fields (e.g. a user's email) MUST be enforced at the database level with a unique index, and application code that checks for existence before insert MUST also handle the database-level unique-violation exception as a fallback for the race condition between the check and the insert.

## Why

An application-level "check then insert" (`if (await context.Users.Exists(email)) throw ...` followed later by `SaveChangesAsync()`) is inherently racy: two concurrent registration requests with the same email can both pass the existence check before either commits, producing duplicate rows. The "before" sample in this material relies solely on that check. The "after" sample adds a unique index via `builder.HasIndex(u => u.Email).IsUnique()` (with an EF Core migration) so the database itself is the source of truth, and wraps `SaveChangesAsync()` in a `try/catch` that translates the Postgres `UniqueViolation` (`23505`) surfaced through `NpgsqlException`/`DbUpdateException` back into the same domain-friendly error. This closes the race window while keeping the existing check as a fast-path/early-exit for the common case.

## Before (Anti-pattern)

```csharp
// Relies only on an application-level existence check - racy under concurrency,
// and nothing stops a duplicate row at the database level.
if (await context.Users.Exists(request.Email))
{
    throw new Exception("The email is already in use");
}

var user = new User { Id = Guid.NewGuid(), Email = request.Email, /* ... */ };
context.Users.Add(user);
await context.SaveChangesAsync();
```

## After (Standard)

```csharp
// EF Core configuration: database-level constraint is the real guarantee.
builder.HasIndex(u => u.Email).IsUnique();

// Handler: keep the fast-path check, but also catch the DB-level violation.
if (await context.Users.Exists(request.Email))
{
    throw new Exception("The email is already in use");
}

context.Users.Add(user);

try
{
    await context.SaveChangesAsync();
}
catch (DbUpdateException e)
    when (e.InnerException is NpgsqlException { SqlState: PostgresErrorCodes.UniqueViolation })
{
    throw new Exception("The email is already in use", e);
}
```

## Rules for LLMs / Agents

- Any entity property that must be unique MUST have a corresponding `HasIndex(...).IsUnique()` in its EF Core `IEntityTypeConfiguration`, backed by a migration.
- Never rely solely on an application-level "does it exist" check to enforce uniqueness; always assume it can race.
- When inserting an entity with a uniquely-constrained column, wrap `SaveChangesAsync()` in a `try/catch` matching on `DbUpdateException` with an `InnerException` of `NpgsqlException { SqlState: PostgresErrorCodes.UniqueViolation }` (or the equivalent for the provider in use), and translate it into the same domain error the pre-check would throw.
- Keep the pre-check (existence query) as an optimization for a clean error/UX path — do not remove it just because the DB constraint exists; the two are complementary, not alternatives.

## When NOT to apply

None observed — for any field where duplicates are a correctness problem (not just a UX nicety), both the DB constraint and the exception handling should be present. If duplicates are merely undesirable but not unsafe (e.g. a display name), a DB-level unique index may be too strict and can be skipped.
