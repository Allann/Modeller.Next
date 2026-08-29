using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Modeller.Api.Initiative;

/// <summary>The role a session credential (issue #146) actually carries. Judged from the
/// credential presented — never from a client-supplied role string.</summary>
public enum InitiativeCredentialRole { Facilitator, DomainExpert }

/// <summary>Why a presented credential was refused.</summary>
public enum InitiativeCredentialFailure { Missing, Malformed, Expired, WrongSession }

public sealed record InitiativeCredentialResult(bool Succeeded, InitiativeCredentialRole? Role, InitiativeCredentialFailure? Failure)
{
    public static InitiativeCredentialResult Ok(InitiativeCredentialRole role) => new(true, role, null);

    public static InitiativeCredentialResult Fail(InitiativeCredentialFailure failure) => new(false, null, failure);
}

/// <summary>
/// Mints and validates the two role-scoped credentials issued when an Initiative session starts
/// (issue #146). Every credential is a self-contained, HMAC-signed token carrying the session ID,
/// the role, and an expiry — so validating one is a pure function of the credential string and the
/// route's session ID, with no repository round trip and no dependency on which
/// <see cref="IInitiativeSessionRepository"/> backend is in play (JSON file locally, Upstash on
/// Vercel).
/// </summary>
public interface IInitiativeCredentialService
{
    /// <summary>Mints a credential for <paramref name="sessionId"/>/<paramref name="role"/>. Pass an
    /// explicit (even negative) <paramref name="ttl"/> to mint an already-expired credential for
    /// testing; otherwise the configured default TTL applies.</summary>
    string Mint(Guid sessionId, InitiativeCredentialRole role, TimeSpan? ttl = null);

    /// <summary>Validates <paramref name="credential"/> against <paramref name="expectedSessionId"/>
    /// — the session named in the URL/route, not the session the token itself claims. A credential
    /// minted for a different session fails with <see cref="InitiativeCredentialFailure.WrongSession"/>.</summary>
    InitiativeCredentialResult Validate(string? credential, Guid expectedSessionId);
}

/// <summary>
/// JWT-backed credential, issued and validated per <c>docs/coding-standards/security-and-identity/generating-jwts.md</c>
/// via <see cref="JsonWebTokenHandler"/> (<c>Microsoft.IdentityModel.JsonWebTokens</c>) rather than a
/// hand-rolled token format: an HMAC-SHA256-signed JWT carrying the session ID and role as custom
/// claims (<c>sid</c>/<c>role</c>) and an <c>exp</c> claim for expiry, plus a standard <c>iss</c>/<c>aud</c>
/// pair — both minted and validated — so a structurally-valid, correctly-signed token minted for some
/// other purpose (or by some other deployment sharing the same signing key) is still rejected. Issuer
/// and audience come from <c>Initiative:CredentialIssuer</c>/<c>Initiative:CredentialAudience</c>; unlike
/// the signing key they are not secrets, so an unset value falls back to a fixed, non-secret default
/// (<see cref="DefaultIssuer"/>/<see cref="DefaultAudience"/>) in every environment rather than failing
/// fast. The signing key comes from
/// <c>Initiative:CredentialSigningKey</c> so credentials survive process restarts (a session can run
/// over days, and Vercel's serverless functions cold-start independently). That config value is
/// <b>required</b> outside the Development environment — this service throws at construction time
/// (DI resolution) rather than silently minting credentials under a key nobody else has. In
/// Development only, an unset key falls back to a random key generated for this process's lifetime:
/// local dev restarts constantly (file watchers, rebuilds) and nobody expects a shared link to
/// survive that, so failing fast there would just be noise; the fallback is also what lets
/// <c>WebApplicationFactory&lt;Program&gt;</c>-based tests (which default to the Development
/// environment) mint and validate within the same process without any test configuration. TTL
/// defaults to 30 days (<c>Initiative:CredentialTtlDays</c>) — generous enough for a multi-day
/// Discover/Frame/Shape engagement while still eventually expiring an abandoned link.
/// </summary>
public sealed class HmacInitiativeCredentialService : IInitiativeCredentialService
{
    private const string SessionIdClaim = "sid";
    private const string RoleClaim = "role";

    /// <summary>Non-secret default <c>iss</c> claim used when <c>Initiative:CredentialIssuer</c> is unset.</summary>
    public const string DefaultIssuer = "modeller-initiative";

    /// <summary>Non-secret default <c>aud</c> claim used when <c>Initiative:CredentialAudience</c> is unset.</summary>
    public const string DefaultAudience = "modeller-initiative-clients";

    private static readonly JsonWebTokenHandler TokenHandler = new();

    private readonly SigningCredentials _signingCredentials;
    private readonly SymmetricSecurityKey _securityKey;
    private readonly TimeSpan _defaultTtl;
    private readonly TimeProvider _timeProvider;
    private readonly string _issuer;
    private readonly string _audience;

    public HmacInitiativeCredentialService(IConfiguration configuration, TimeProvider timeProvider, IHostEnvironment environment)
    {
        _timeProvider = timeProvider;
        _issuer = configuration["Initiative:CredentialIssuer"] is { Length: > 0 } configuredIssuer
            ? configuredIssuer
            : DefaultIssuer;
        _audience = configuration["Initiative:CredentialAudience"] is { Length: > 0 } configuredAudience
            ? configuredAudience
            : DefaultAudience;
        var configuredKey = configuration["Initiative:CredentialSigningKey"];
        byte[] key;
        if (!string.IsNullOrWhiteSpace(configuredKey))
        {
            key = Encoding.UTF8.GetBytes(configuredKey);
        }
        else if (environment.IsDevelopment())
        {
            key = RandomNumberGenerator.GetBytes(32);
        }
        else
        {
            throw new InvalidOperationException(
                "Initiative:CredentialSigningKey is required outside the Development environment. " +
                "Without a fixed signing key, every process restart (including Vercel cold starts) " +
                "invalidates every credential minted by the prior process, silently breaking every " +
                "outstanding Facilitator/Domain Expert link.");
        }

        _securityKey = new SymmetricSecurityKey(key);
        _signingCredentials = new SigningCredentials(_securityKey, SecurityAlgorithms.HmacSha256);
        var ttlDays = configuration.GetValue("Initiative:CredentialTtlDays", 30);
        _defaultTtl = TimeSpan.FromDays(ttlDays);
    }

    public string Mint(Guid sessionId, InitiativeCredentialRole role, TimeSpan? ttl = null)
    {
        var expiresAt = _timeProvider.GetUtcNow() + (ttl ?? _defaultTtl);
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Claims = new Dictionary<string, object>
            {
                [SessionIdClaim] = sessionId.ToString(),
                [RoleClaim] = role.ToString(),
            },
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = _signingCredentials,
            Issuer = _issuer,
            Audience = _audience,
        };

        return TokenHandler.CreateToken(tokenDescriptor);
    }

    public InitiativeCredentialResult Validate(string? credential, Guid expectedSessionId)
    {
        if (string.IsNullOrWhiteSpace(credential))
            return InitiativeCredentialResult.Fail(InitiativeCredentialFailure.Missing);

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _issuer,
            ValidateAudience = true,
            ValidAudience = _audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = _securityKey,
            ClockSkew = TimeSpan.Zero,
            // The default lifetime validator compares against the real system clock and treats an
            // expiry exactly equal to "now" as still valid (expires < now, not <=). This service is
            // tested against an injected TimeProvider (including pinning "now" onto the exact expiry
            // boundary), and the documented contract is that a credential expiring at exactly the
            // current instant is rejected — so the clock and the boundary comparison are both taken
            // over explicitly here instead of relying on the library default.
            LifetimeValidator = (_, expires, _, _) =>
                expires.HasValue && _timeProvider.GetUtcNow().UtcDateTime < expires.Value,
        };

        TokenValidationResult validationResult;
        try
        {
            // Reqnroll step bindings and endpoint filters call Validate synchronously; there is no
            // async validate call site in this codebase to switch to, so the async handler API is
            // bridged with GetAwaiter().GetResult() rather than making this method async all the way
            // up — JsonWebTokenHandler's validation work here is CPU-bound (signature check, JSON
            // parse), not I/O, so there is nothing to actually await.
            validationResult = TokenHandler.ValidateTokenAsync(credential, validationParameters).GetAwaiter().GetResult();
        }
        catch (SecurityTokenException)
        {
            return InitiativeCredentialResult.Fail(InitiativeCredentialFailure.Malformed);
        }
        catch (ArgumentException)
        {
            return InitiativeCredentialResult.Fail(InitiativeCredentialFailure.Malformed);
        }

        if (!validationResult.IsValid)
        {
            return validationResult.Exception switch
            {
                SecurityTokenExpiredException => InitiativeCredentialResult.Fail(InitiativeCredentialFailure.Expired),
                SecurityTokenInvalidLifetimeException => InitiativeCredentialResult.Fail(InitiativeCredentialFailure.Expired),
                _ => InitiativeCredentialResult.Fail(InitiativeCredentialFailure.Malformed),
            };
        }

        var claims = validationResult.ClaimsIdentity.Claims.ToDictionary(c => c.Type, c => c.Value);

        if (!claims.TryGetValue(SessionIdClaim, out var sessionIdClaim)
            || !Guid.TryParse(sessionIdClaim, out var sessionId)
            || !claims.TryGetValue(RoleClaim, out var roleClaim)
            || !Enum.TryParse<InitiativeCredentialRole>(roleClaim, out var role))
        {
            return InitiativeCredentialResult.Fail(InitiativeCredentialFailure.Malformed);
        }

        if (sessionId != expectedSessionId)
            return InitiativeCredentialResult.Fail(InitiativeCredentialFailure.WrongSession);

        return InitiativeCredentialResult.Ok(role);
    }
}
