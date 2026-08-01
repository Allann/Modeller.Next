---
title: "Refactor Anemic State-Bag Models Into Types That Make Illegal States Unrepresentable"
---

# Refactor Anemic State-Bag Models Into Types That Make Illegal States Unrepresentable


## The Standard

Do not model a business entity as a single mutable class with a `Status` enum plus a pile of nullable fields that are "sometimes set" depending on that status, and do not model its component values (money, currency, account references, timestamps) as bare primitives (`decimal`, `string`, `Guid`, `DateTime`). Instead, give each meaningful value its own validating value type, and split status-dependent shapes into a class-per-state hierarchy (or discriminated union) so a given state's data is exactly the data that state needs — no nullable fields standing in for "not applicable in this state."

## Why

The "before" `Transfer` class has one `TransferStatus` enum and six nullable-or-defaulted fields (`ExecutedAt`, `ApprovedByEmployee1`, `ApprovedByEmployee2`, `RejectedByEmployee`) whose meaning depends entirely on the current `Status` — nothing in the type stops `ExecutedAt` being set on a `Pending` transfer, or both `ApprovedByEmployee` and `RejectedByEmployee` being set simultaneously, and every consumer must re-derive "what fields are valid right now" from `Status` at every use site. It is also full of bare primitives: `Amount` is a raw `decimal` with no non-negativity or currency-precision check, `Currency` is a raw `string`, `FromAccountId`/`ToAccountId` are raw `Guid`s that could be swapped without the compiler noticing. The "after" version replaces the primitives with validating value types (`Money(decimal, Iso4217Currency)` rejects the wrong number of decimal places; `AccountId(Guid)` rejects `Guid.Empty`) and replaces the status enum with a `Transfer` class hierarchy (`PendingTransfer`, `ApprovedTransfer`, `ExecutedTransfer`, `ExpiredTransfer`, `RejectedTransfer`) plus a parallel `FourEyesApproval` hierarchy (`PendingApproval`/`PartlyApproved`/`FullyApproved`/`Rejected`/`NotRequired`) gated by `IIncompleteApproval`/`ICompletedApproval` marker interfaces. `PendingTransfer.AddApproval` can only return an `ApprovedTransfer` or another `PendingTransfer` — the compiler guarantees a `RejectedTransfer` can never carry approval data, and `ExecutedTransfer.ExecutedAt` cannot be set past `ExpiresAt` because the constructor itself throws if it would be.

## Before (Anti-pattern)

```csharp
public class Transfer
{
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public TransferStatus Status { get; private set; }
    public DateTime? ExecutedAt { get; private set; }
    public Guid? ApprovedByEmployee1 { get; private set; }
    public Guid? ApprovedByEmployee2 { get; private set; }
    public Guid? RejectedByEmployee { get; private set; }
    // Nothing prevents ExecutedAt being set while Status == Pending,
    // or ApprovedByEmployee1 and RejectedByEmployee both being non-null.
}
```

## After (Standard)

```csharp
public record Money(decimal Amount, Iso4217Currency Currency)
{
    public decimal Amount { get; init; } =
        Amount >= 0 && Math.Round(Amount, Currency.MinorUnit) == Amount ? Amount
        : throw new ArgumentException("Amount must be non-negative with correct minor units.");
}

public abstract class Transfer(Money amount, Guid id, AccountId from, AccountId to,
    TransferTimestamp expiresAt, FourEyesApproval approval) { /* shared fields only */ }

public class PendingTransfer : Transfer
{
    public Transfer AddApproval(EmployeeId approver) =>
        IncompleteApproval.Approve(approver) switch
        {
            ICompletedApproval completed => new ApprovedTransfer(..., completed),
            IIncompleteApproval incomplete => new PendingTransfer(..., incomplete),
            _ => throw new InvalidOperationException("Impossible state")
        };
}
```

## Rules for LLMs / Agents

- Never represent a business value (money, currency, an entity's ID, a timestamp with domain meaning) as a bare `decimal`/`string`/`Guid`/`DateTime`; wrap it in a small validating value type (record) that rejects invalid values in its constructor/init.
- Never model a status-dependent entity as one class with a status enum plus nullable fields for each status's extra data; split it into one type per state (a small class hierarchy or discriminated union) so each state's constructor only accepts the fields that state actually has.
- Make state transitions return the new state's type, not mutate `Status` in place — e.g., `PendingTransfer.AddApproval(...)` returns `Transfer` (a `PendingTransfer` or `ApprovedTransfer`), it does not flip an enum field.
- Use marker interfaces (`ICompletedApproval`, `IIncompleteApproval`) to make illegal transitions a compile error where a plain enum comparison would only be a runtime check.
- Push validation into the constructor/`init` accessor of the value type itself, so an invalid instance cannot be constructed anywhere in the codebase — do not re-validate the same field at every call site.

## When NOT to apply

For a genuinely simple entity where every field is always meaningful regardless of state (no status-dependent nullability), a single class with plain properties is sufficient and this decomposition would be over-engineering. None observed against the material's own scope (money transfers with multi-step approval).
