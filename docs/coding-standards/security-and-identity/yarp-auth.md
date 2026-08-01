---
title: "Per-Route Authorization on a YARP Gateway"
---

# Per-Route Authorization on a YARP Gateway


## The Standard

A YARP reverse-proxy gateway that fronts multiple backend clusters MUST authenticate requests and attach a distinct authorization policy (via claims) to each proxied route in configuration (`ReverseProxy:Routes:*:AuthorizationPolicy`), rather than exposing the reverse proxy unauthenticated or with a single blanket policy for every downstream API.

## Why

The "before" gateway does nothing but `AddReverseProxy().LoadFromConfig(...)` and `MapReverseProxy()` — every route is proxied without any authentication or authorization, so any caller that can reach the gateway can reach every backend cluster. The "after" gateway adds bearer-token authentication (`AddAuthentication(BearerTokenDefaults.AuthenticationScheme).AddBearerToken()`), defines named authorization policies per downstream API (`first-api-access`, `second-api-access`) each requiring a specific claim, and the YARP route configuration in `appsettings.json` assigns `"AuthorizationPolicy": "first-api-access"` / `"second-api-access"` to the corresponding route. This means a caller authenticated with only `first-api-access` cannot reach the `second-api` cluster through the gateway even though both are proxied through the same process — authorization is enforced per destination, not just at the gateway's front door.

## Before (Anti-pattern)

```csharp
// Gateway proxies everything with no authentication or per-route authorization.
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();
app.MapReverseProxy();
app.Run();
```

## After (Standard)

```csharp
builder.Services
    .AddAuthentication(BearerTokenDefaults.AuthenticationScheme)
    .AddBearerToken();

builder.Services.AddAuthorization(o =>
{
    o.AddPolicy("first-api-access",
        policy => policy.RequireAuthenticatedUser().RequireClaim("first-api-access", true.ToString()));
    o.AddPolicy("second-api-access",
        policy => policy.RequireAuthenticatedUser().RequireClaim("second-api-access", true.ToString()));
});

app.UseAuthentication();
app.UseAuthorization();
app.MapReverseProxy();
```

```json
// appsettings.json - each route pins its own authorization policy
"Routes": {
  "api1-route": { "ClusterId": "api1-cluster", "AuthorizationPolicy": "first-api-access", "Match": { "Path": "first-api/{**catch-all}" } },
  "api2-route": { "ClusterId": "api2-cluster", "AuthorizationPolicy": "second-api-access", "Match": { "Path": "second-api/{**catch-all}" } }
}
```

## Rules for LLMs / Agents

- Any YARP (or equivalent) gateway route that proxies to a backend cluster MUST declare an `AuthorizationPolicy` in its route configuration; do not leave routes without an explicit policy unless the route is intentionally public.
- Define one named authorization policy per distinct access scope/backend, each requiring the specific claim(s) that grant access to that backend — do not reuse a single generic "authenticated" policy across unrelated downstream APIs.
- Register authentication (`AddAuthentication(...).AddBearerToken()` or the platform's chosen scheme) and call `UseAuthentication()`/`UseAuthorization()` before `MapReverseProxy()` in the pipeline.
- Keep the mapping from route to policy in the reverse-proxy configuration (`appsettings.json`/config provider), not scattered in code, so route-to-policy assignments stay auditable in one place.

## When NOT to apply

A route intended to be publicly reachable (health checks, public status endpoints) can omit `AuthorizationPolicy`, but that omission should be a deliberate, reviewed decision, not the default for every route.
