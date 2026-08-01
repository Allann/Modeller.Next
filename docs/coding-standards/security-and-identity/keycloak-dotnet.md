---
title: "Keycloak Authentication in ASP.NET Core"
---

# Keycloak Authentication in ASP.NET Core


## The Standard

APIs that authenticate against Keycloak MUST use JWT bearer authentication configured via OIDC metadata discovery (`MetadataAddress`), not manually-supplied signing keys, and MUST validate `Audience` and `ValidIssuer` from configuration. Protected endpoints MUST be marked with `RequireAuthorization()` rather than relying on ambient middleware ordering alone.

## Why

The "before" sample wires up Swagger and calls `UseAuthentication()`/`UseAuthorization()` but never registers an authentication scheme or configures any handler — the middleware exists but nothing can actually be authenticated against, so `RequireAuthorization()` would fail closed with no valid path in. The "after" sample registers `AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(...)` pointed at Keycloak's realm's metadata endpoint (`Authentication:MetadataAddress`), which lets the JWT bearer handler fetch signing keys and issuer/JWKS data dynamically instead of hardcoding a shared secret — appropriate for an external IdP whose keys rotate. It also validates `Audience` and `ValidIssuer` explicitly, adds a Swagger OAuth2 security definition pointing at Keycloak's authorization URL so the docs UI can drive a real login flow, and adds OpenTelemetry tracing around the auth-protected pipeline. `source_code-keycloak-intro` is a minimal setup reference (a `docker run` command to stand up a dev Keycloak instance and a Postman collection for exercising its token endpoints) — it has no extractable code pattern beyond configuring the same `Authentication:MetadataAddress`/`Audience`/`ValidIssuer` triad shown here.

## Before (Anti-pattern)

```csharp
// Authentication/authorization middleware wired up, but no scheme is ever
// registered - there is nothing for UseAuthentication() to authenticate against.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGenWithAuth(builder.Configuration);

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.Run();
```

## After (Standard)

```csharp
builder.Services.AddAuthorization();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.RequireHttpsMetadata = false; // dev only
        o.Audience = builder.Configuration["Authentication:Audience"];
        o.MetadataAddress = builder.Configuration["Authentication:MetadataAddress"]!;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = builder.Configuration["Authentication:ValidIssuer"]
        };
    });

app.MapGet("users/me", (ClaimsPrincipal claimsPrincipal) =>
    claimsPrincipal.Claims.ToDictionary(c => c.Type, c => c.Value))
    .RequireAuthorization();
```

## Rules for LLMs / Agents

- Configure Keycloak (or any external OIDC IdP) integration with `AddJwtBearer` using `MetadataAddress` for discovery; do not hardcode signing keys/certs for an external IdP.
- Always set `Audience` and `ValidIssuer` (or `ValidIssuers`) from configuration to prevent tokens from unrelated clients/realms being accepted.
- Mark protected minimal-API endpoints/controllers with `RequireAuthorization()` explicitly rather than assuming global middleware ordering is sufficient.
- Only set `RequireHttpsMetadata = false` in local/dev configuration; production configuration must require HTTPS metadata.
- Register a Swagger/OpenAPI OAuth2 security definition pointing at the IdP's authorization URL when Keycloak auth is added, so interactive API docs can obtain tokens.
- Do not implement custom JWT validation/signature-checking logic when an IdP like Keycloak is in play — rely on the standard `AddJwtBearer` handler and metadata discovery.

## When NOT to apply

If the service issues its own tokens rather than delegating to an external IdP, follow `generating-jwts.md` instead (local `TokenProvider` with a configured secret) — the metadata-discovery approach here specifically applies to external IdP integration.
