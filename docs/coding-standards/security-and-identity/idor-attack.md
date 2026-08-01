---
title: "Scope Every Query by Owner at the Data-Access Layer to Prevent IDOR"
---

# Scope Every Query by Owner at the Data-Access Layer to Prevent IDOR


## The Standard

Never trust a route/query/form id alone as sufficient authorization to read, update, or delete a record. Scope every read/update/delete query by the authenticated user's id (or org/tenant id) at the repository/data-access layer — not just in the controller or page handler — so a forgotten check on any one caller can't leak or corrupt another user's data.

## Why

An id only identifies *which* record is being requested; it says nothing about *whether the current caller may access it*. In the vulnerable version, `CompaniesRepository.TryFindAsync` looked up a company purely by its external id, so any authenticated user could edit any other user's company by substituting a different guid into `/EditCompany?id=<guid>` (an Insecure Direct Object Reference). Fixing it at the UI/page level would only be as strong as the discipline of whoever writes the next page; pushing the ownership filter into the repository itself means every caller — present and future — gets the check automatically.

## Before (Anti-pattern)

```csharp
// CompaniesRepository.cs — no ownership check; any valid id from any user works
public async Task<Company?> TryFindAsync(ExternalId<Company> externalId, CancellationToken cancellationToken = default)
{
    const string sql = @"SELECT c.Id, c.ExternalId, c.UserId, c.Name, c.TIN, c.Deleted, c.CompanyType,
                                a.Id, a.ExternalId, a.StreetAddress, a.City, a.State,
                                a.PostalCode, a.Country, a.AddressKind
                         FROM business.Companies c
                         INNER JOIN business.Addresses a ON a.CompanyId = c.Id
                         WHERE c.ExternalId = @ExternalId AND c.Deleted = 0";
    // executed with only { ExternalId = externalId.Value } — UserId is never checked
}
```

## After (Standard)

```csharp
// CompaniesRepository.cs — owner threaded through the constructor, applied to every query
private UserId _owner;

public CompaniesRepository(DbConnection connection, UserId owner, DbTransaction? transaction = null) =>
    (_connection, _owner, _transaction) = (connection, owner, transaction);

public async Task<Company?> TryFindAsync(ExternalId<Company> externalId, CancellationToken cancellationToken = default)
{
    const string sql = @"... FROM business.Companies c
                         INNER JOIN business.Addresses a ON a.CompanyId = c.Id
                         WHERE c.ExternalId = @ExternalId AND c.Deleted = 0 AND c.UserId = @UserId;";
    // executed with { ExternalId = externalId.Value, UserId = _owner.Value }
}
// The same "AND UserId = @UserId" guard is added to AddAsync, UpdateAsync, TryFindCompanyId, and DeleteAsync.
```

## Rules for LLMs / Agents

- Never trust a route/query/form id as sufficient authorization — it identifies *which* record, not *whether the caller may see it*.
- Scope every read/update/delete query by the authenticated user's id (or org/tenant id) at the data-access layer, not just in the UI/controller, so a forgotten check on one page can't leak data.
- Resolve "current owner" once (e.g. from the auth claims) and thread it through the repository/unit-of-work constructor, rather than re-deriving/re-checking it ad hoc per handler.
- Treat "not found" and "not yours" identically — return the same 404/redirect for both, so an attacker cannot distinguish valid-but-forbidden ids from nonexistent ones via a different response.
- Use non-guessable, non-sequential external ids (GUIDs) as defense-in-depth, but never as a substitute for an ownership check.
- Apply the same ownership filter symmetrically across all CRUD paths (find, update, delete, add) — a fix applied only to `GET` leaves `POST`/`DELETE` exploitable.
- Add integration tests that assert one user cannot read/modify/delete another user's resource by id substitution.

## When NOT to apply

Resources that are genuinely global/shared (reference data, public catalog entries) do not need an ownership filter — only apply this to per-user or per-tenant owned data.
