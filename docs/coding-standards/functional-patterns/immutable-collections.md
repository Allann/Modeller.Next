---
title: "Expose Immutable State via Persistent Collections, Not List<T> Copies"
---

# Expose Immutable State via Persistent Collections, Not `List<T>` Copies


## The Standard

When a record needs to expose a growable collection without allowing external mutation, type the property as `System.Collections.Immutable.ImmutableList<T>` (or another persistent immutable collection), not `List<T>`/`IList<T>`. Persistent collections use structural sharing, so "changing" them is O(log n), not an O(n) defensive copy — genuine immutability without the performance cost naive copying implies.

## Why

A record that exposes `List<Money> Deposits` and "changes" it by copying the whole list into a new `List<T>` before adding one item pays an O(n) cost on every operation and still risks exposing a mutable reference if the copy step is ever skipped. `ImmutableList<T>` is a persistent AVL-tree-like structure: `Add`/`AddRange` return a *new* `ImmutableList<T>` that shares almost all of its internal nodes with the original, so the operation costs `O(log n)` (or `O(m + log n)` for adding `m` items), the original instance is provably untouched, and there is no defensive-copy code to remember to write.

## Before (Anti-pattern)

```csharp
// Looks immutable but every "change" is an O(n) full copy,
// and nothing stops a caller from casting Deposits back to List<Money> and mutating it.
record AccountNaive(Currency Currency, List<Money> Deposits)
{
    public AccountNaive Deposit(Money m)
    {
        var newList = new List<Money>(Deposits) { m };   // O(n) copy every time
        return this with { Deposits = newList };
    }
}
```

## After (Standard)

```csharp
record Account(Currency Currency, ImmutableList<Money> Deposits, ImmutableList<Money> Withdrawals);

static class AccountExtensions
{
    public static Account Deposit(this Account account, Money money) =>
        money.Currency != account.Currency ? throw new ArgumentException("Currency mismatch")
        : account with { Deposits = account.Deposits.Add(money) };       // O(log n), structural sharing

    public static Money GetBalance(this Account account) =>
        new Money(account.Currency,
            account.Deposits.Sum(d => d.Amount) - account.Withdrawals.Sum(w => w.Amount));
}
```

## Rules for LLMs / Agents

- Type any collection property on an immutable record as `ImmutableList<T>` / `ImmutableArray<T>` / `ImmutableDictionary<K,V>` (whichever matches the access pattern), never `List<T>`/`IList<T>`/`Dictionary<K,V>` directly.
- "Change" a persistent collection by calling `Add`/`Remove`/`SetItem`/etc., which return a new instance — never mutate in place, and never manually clone into a fresh mutable collection first.
- Combine persistent collections with `with` expressions on the containing record so the whole update reads as one expression (`account with { Deposits = account.Deposits.Add(money) }`).
- Do not assume persistent collections are "slow because immutable" — structural sharing makes single-item changes `O(log n)`, not `O(n)`; only a true from-scratch bulk rebuild costs more.

## When NOT to apply

For collections that are built once and never modified afterward, a plain `ImmutableArray<T>` (or even a frozen array) is simpler and faster to iterate than `ImmutableList<T>`, which is optimized for incremental changes, not raw enumeration throughput.
