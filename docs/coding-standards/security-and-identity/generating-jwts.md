---
title: "Generating JWTs for Authentication"
---

# Generating JWTs for Authentication


## The Standard

Issue authentication tokens through a single dedicated `TokenProvider` that builds a `SecurityTokenDescriptor` (subject claims, expiry, signing credentials, issuer, audience) from configuration and returns a signed JWT via `JsonWebTokenHandler`; never hand-build or hardcode token contents inline in a handler.

## Why

The "before" sample's `LoginUser.Handle` verifies credentials and returns the raw `User` entity — there is no token issuance at all, so callers have nothing to attach to subsequent authenticated requests, and secrets/expiry/claims would end up duplicated wherever a token was needed. The "after" sample centralizes token creation in `TokenProvider`, which pulls the signing secret, issuer, audience, and expiration window from `IConfiguration` (`Jwt:Secret`, `Jwt:Issuer`, `Jwt:Audience`, `Jwt:ExpirationInMinutes`) rather than hardcoding them, includes only the minimal necessary claims (`sub`, `email`, `email_verified`), and signs with `SymmetricSecurityKey` + `HmacSha256`. `LoginUser` then depends on `TokenProvider` and returns the token string instead of the entity, so the API's login response is what a client can actually use for subsequent calls.

## Before (Anti-pattern)

```csharp
// Login only checks credentials and returns the User entity - no token is
// ever issued, so there is nothing for a client to authenticate with afterward.
public async Task<User> Handle(Request request)
{
    User? user = await context.Users.GetByEmail(request.Email);
    if (user is null || !user.EmailVerified) throw new Exception("The user was not found");
    if (!passwordHasher.Verify(request.Password, user.PasswordHash))
        throw new Exception("The password is incorrect");

    return user;
}
```

## After (Standard)

```csharp
internal sealed class TokenProvider(IConfiguration configuration)
{
    public string Create(User user)
    {
        var securityKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(configuration["Jwt:Secret"]!));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim("email_verified", user.EmailVerified.ToString())
            ]),
            Expires = DateTime.UtcNow.AddMinutes(configuration.GetValue<int>("Jwt:ExpirationInMinutes")),
            SigningCredentials = credentials,
            Issuer = configuration["Jwt:Issuer"],
            Audience = configuration["Jwt:Audience"]
        };

        return new JsonWebTokenHandler().CreateToken(tokenDescriptor);
    }
}

// LoginUser now returns the token, not the entity.
string token = tokenProvider.Create(user);
return token;
```

## Rules for LLMs / Agents

- Isolate JWT creation in a single dedicated provider/service (e.g. `TokenProvider`); do not inline `SecurityTokenDescriptor` construction inside request handlers.
- Source the signing secret, issuer, audience, and expiration from configuration (`IConfiguration`), never hardcode them in source.
- Sign with `SigningCredentials` over a `SymmetricSecurityKey` (or asymmetric key per infra requirements) and issue via `JsonWebTokenHandler.CreateToken`, not manual string concatenation.
- Keep the claim set minimal and purposeful — only include what the API/downstream consumers actually need to check (subject id, email, verification state), not the whole entity.
- Handlers that authenticate a user (login, refresh, etc.) MUST return the token, not the domain entity, so the API boundary exposes a client-usable credential.
- Wire up Swagger/OpenAPI bearer-auth security definitions (as in `ServiceCollectionExtensions.AddSwaggerGenWithAuth`) whenever JWT auth is added, so the API surface documents how to authenticate.

## When NOT to apply

Skip local JWT issuance entirely when the system delegates authentication to an external identity provider (e.g. Keycloak) — see `keycloak-dotnet.md` — since in that case tokens are minted by the IdP, not generated in application code.
