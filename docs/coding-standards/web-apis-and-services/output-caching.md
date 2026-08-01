---
title: "Output Caching for GET Endpoints"
---

# Output Caching for GET Endpoints


## The Standard

GET endpoints that return per-user or otherwise cacheable data MUST use ASP.NET Core's output caching (`CacheOutput`) with an explicit expiry, a cache tag, and a `VaryByValue` key derived from the caller's identity when the response is user-specific. Any command/mutation that changes the underlying data MUST evict the corresponding cache tag via `IOutputCacheStore.EvictByTagAsync` in the same handler.

## Why

The "before" `GetUser` handler hits the database on every request with no caching at all. The "after" version adds `.CacheOutput(...)` with a 10-minute expiry, tags the cache entry with `UserEndpoints.Tag`, and varies the cache key by the authenticated user's id (`VaryByValue`) so one user's cached response is never served to another. Critically, `UpdateUser` — the only handler that mutates a user's profile — calls `await cacheStore.EvictByTagAsync(UserEndpoints.Tag, default)` after saving, so a stale cached response is never served after an update. A custom `IOutputCachePolicy` (`CustomPolicy`) additionally restricts caching to GET/HEAD, 200-OK responses without `Set-Cookie` headers, preventing cache poisoning of error/auth responses.

## Before (Anti-pattern)

```csharp
// No caching - every request round-trips to the database even though
// user profile data changes infrequently.
app.MapGet("users/{id:guid}", async (Guid id, GetUser useCase) =>
{
    var user = await useCase.Handle(id);
    return user is not null ? Results.Ok(user) : Results.NotFound();
})
.WithTags(UserEndpoints.Tag);
```

## After (Standard)

```csharp
app.MapGet("users/{id:guid}", async (Guid id, GetUser useCase, ClaimsPrincipal claimsPrincipal) =>
{
    if (id != claimsPrincipal.UserId()) return Results.Forbid();

    var user = await useCase.Handle(id);
    return user is not null ? Results.Ok(user) : Results.NotFound();
})
.WithTags(UserEndpoints.Tag)
.RequireAuthorization()
.CacheOutput(builder => builder
    .Expire(TimeSpan.FromMinutes(10))
    .Tag(UserEndpoints.Tag)
    .VaryByValue((httpContext, _) => ValueTask.FromResult(
        new KeyValuePair<string, string>(
            nameof(ClaimsPrincipalExtensions.UserId), httpContext.User.UserId().ToString()))),
    true);

// The mutation that invalidates the cache:
public async Task Handle(Command command)
{
    // ... update user, SaveChangesAsync ...
    await cacheStore.EvictByTagAsync(UserEndpoints.Tag, default);
}
```

## Rules for LLMs / Agents

- Add `.CacheOutput(...)` with an explicit `Expire(...)` to GET endpoints that serve data expensive to compute/query and safe to serve slightly stale.
- Tag every cached endpoint (`.Tag(...)`) with a stable tag name; use that same tag for cache eviction.
- When a response varies per caller (e.g. by authenticated user), set `VaryByValue`/`VaryByHeader` keyed on the identity so cached responses are never cross-served between users.
- Every command/handler that mutates data backing a cached GET endpoint MUST call `IOutputCacheStore.EvictByTagAsync` for that endpoint's tag after the write succeeds.
- Restrict caching to safe, cacheable responses: only GET/HEAD requests, only 200 OK, and never responses carrying `Set-Cookie` — enforce this via a custom `IOutputCachePolicy` if the default policy doesn't already guarantee it.
- Pair cached endpoints with `.RequireAuthorization()` and identity checks (e.g. `id != claimsPrincipal.UserId()`) where the resource is per-user, so caching never bypasses authorization.

## When NOT to apply

Do not cache endpoints returning highly volatile or security-sensitive data that must always be fresh (e.g. balance checks, one-time tokens), or POST/PUT/DELETE endpoints — output caching only applies to safe, idempotent GET/HEAD responses.
