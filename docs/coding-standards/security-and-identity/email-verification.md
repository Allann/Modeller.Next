---
title: "Email Verification Flow"
---

# Email Verification Flow


## The Standard

Registration MUST issue a single-use, time-limited verification token tied to the user, email it via a link, and login/other privileged actions MUST check the user's verified state before granting access. Verification tokens MUST be consumed (deleted) once used, and expired tokens MUST be rejected.

## Why

The "before" sample registers a user and logs them straight in with only a `// Email verification?` comment as a placeholder — there is no check that the email address is real/owned by the requester. The "after" sample closes this gap: `RegisterUser` creates an `EmailVerificationToken` (a `Guid` id, tied to `UserId`, with `CreatedOnUtc`/`ExpiresOnUtc`) in the same transaction as the user, generates a verification link via `LinkGenerator` (`EmailVerificationLinkFactory`), and emails it. `VerifyEmail` looks the token up, rejects it if missing, expired, or already verified, flips `User.EmailVerified`, and removes the token so it can't be replayed. `LoginUser` then treats an unverified user as not eligible to log in. This closes the loop: registered-but-unconfirmed accounts cannot fully authenticate, and verification links cannot be reused or replayed after expiry.

## Before (Anti-pattern)

```csharp
// Registration creates the user and immediately returns it - no proof
// the email address is real, no path to gate login on verification.
context.Users.Add(user);
await context.SaveChangesAsync();

// Email verification?

return user;
```

## After (Standard)

```csharp
// Registration issues a single-use, expiring token and emails a verification link.
var verificationToken = new EmailVerificationToken
{
    Id = Guid.NewGuid(),
    UserId = user.Id,
    CreatedOnUtc = utcNow,
    ExpiresOnUtc = utcNow.AddDays(1)
};
context.EmailVerificationTokens.Add(verificationToken);
await context.SaveChangesAsync();

string verificationLink = emailVerificationLinkFactory.Create(verificationToken);
await fluentEmail.To(user.Email).Subject("Email verification for CalConnect")
    .Body($"To verify your email address <a href='{verificationLink}'>click here</a>", isHtml: true)
    .SendAsync();

// Verification handler: reject missing/expired/already-used tokens, consume on success.
public async Task<bool> Handle(Guid tokenId)
{
    var token = await context.EmailVerificationTokens
        .Include(e => e.User)
        .FirstOrDefaultAsync(e => e.Id == tokenId);

    if (token is null || token.ExpiresOnUtc < DateTime.UtcNow || token.User.EmailVerified)
    {
        return false;
    }

    token.User.EmailVerified = true;
    context.EmailVerificationTokens.Remove(token);
    await context.SaveChangesAsync();
    return true;
}

// Login: gate on verification state.
if (user is null || !user.EmailVerified)
{
    throw new Exception("The user was not found");
}
```

## Rules for LLMs / Agents

- Any flow that creates an account/identity bound to an email address MUST issue a verification token and send a verification link before treating the email as confirmed.
- Verification tokens MUST carry an expiry (`ExpiresOnUtc`) and the verification handler MUST reject expired tokens.
- Once a token is successfully used, it MUST be deleted/consumed so it cannot be replayed.
- Build verification links with the app's `LinkGenerator`/routing infrastructure (not hand-built URL strings) so they stay correct if routes change.
- Gate login (and any other privileged action) on the account's verified flag; do not let unverified accounts fully authenticate.
- Guard the "already verified" case in the verification handler (`token.User.EmailVerified` check) to make verification idempotent-safe rather than erroring on repeat visits.

## When NOT to apply

Skip email verification for internal/service accounts not tied to a real inbox, or for systems where identity is already verified through another channel (e.g. SSO/Keycloak-issued identities, magic-link-only auth). None else observed.
