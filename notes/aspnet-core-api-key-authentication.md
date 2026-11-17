# API Key Authentication in ASP.NET Core (ASP.NET Core)

API keys are the simplest way to authenticate machine-to-machine calls: the client sends a secret string with each request, and the server checks it. They identify *which application* is calling, not *which user* — that distinction decides whether API keys are the right tool at all. This note covers a clean ASP.NET Core implementation, how to store and compare keys safely, and where API keys stop being enough.

## Custom Authentication Handler

The idiomatic approach is a custom `AuthenticationHandler<TOptions>`, which plugs into the normal authentication pipeline so `[Authorize]` and policies keep working. The handler reads the key from a header, looks it up, and builds a `ClaimsPrincipal` carrying the client identity.

```csharp
public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public string HeaderName { get; set; } = "X-Api-Key";
}

public class ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
    : AuthenticationHandler<ApiKeyAuthenticationOptions>(options, logger, encoder)
{
    // In a real app this comes from a database or secret store, hashed (see below).
    private static readonly Dictionary<string, string> ClientsByKeyHash = new();

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(Options.HeaderName, out var providedKey))
            return Task.FromResult(AuthenticateResult.NoResult());

        string providedHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(providedKey.ToString())));

        if (!ClientsByKeyHash.TryGetValue(providedHash, out string? clientId))
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));

        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, clientId) };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
```

Register it in `Program.cs` alongside the rest of the pipeline:

```csharp
builder.Services
    .AddAuthentication("ApiKey")
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>("ApiKey", options =>
    {
        options.HeaderName = "X-Api-Key";
    });

builder.Services.AddAuthorization();
```

Then protect endpoints with `[Authorize(AuthenticationSchemes = "ApiKey")]` — or combine schemes so some endpoints accept either an API key or a JWT.

## Comparing Keys in Constant Time

Never compare a presented key against the stored value with `==`. String equality short-circuits at the first differing character, so an attacker can measure response times and guess the key one character at a time (a timing attack). Compare hashes with `CryptographicOperations.FixedTimeEquals`, which always takes the same time regardless of where the values differ:

```csharp
byte[] presentedHash = SHA256.HashData(Encoding.UTF8.GetBytes(presentedKey));
byte[] storedHash = Convert.FromHexString(storedKeyHash);

bool matches = presentedHash.Length == storedHash.Length
    && CryptographicOperations.FixedTimeEquals(presentedHash, storedHash);
```

Hashing first has a second benefit: it makes the comparison length-independent, so `FixedTimeEquals` never throws for mismatched lengths (it requires equal-length inputs). It also means the comparison cost does not leak the key length.

## Storing Keys Hashed, Per Client

Store only a SHA-256 hash of each key, never the plaintext — exactly like passwords. If the database leaks, hashed keys cannot be used directly against the API, and each key can be revoked or rotated individually. Issue one key per client (or per integration), and record the client name, creation date, and last-used date alongside the hash. Per-client keys give you three things a single shared key cannot: revoking one client without breaking the others, usage and rate-limit accounting per client, and a meaningful identity in logs and audit trails.

## Header vs Query String

Send the key in a header (`X-Api-Key`), not in the query string. Query strings end up in server access logs, browser history, referrer headers, and analytics tools — all places a secret should never appear. Headers are still visible to anyone who can inspect the traffic, which is why API keys must only ever travel over HTTPS. A key sent over plain HTTP is compromised the moment it is sent, so enforce HTTPS and consider rejecting requests that arrive without TLS.

## When API Keys Are NOT Enough

API keys authenticate applications, not people, and they carry no expiry, no scopes, and no user identity. Reach for OAuth 2.0 / JWT instead when any of these apply: the caller is a human user (you need to know *who* is acting, and to revoke *their* access without touching other users), different callers need different permissions (scopes/claims), keys must expire automatically, or a third-party identity provider is involved. A common hybrid is to use API keys for trusted server-to-server integrations and JWT bearer tokens for user-facing endpoints — the two schemes can coexist in the same app, with each endpoint declaring which it accepts.

## Key Takeaways

- Implement API key auth as a custom `AuthenticationHandler`, so it composes with `[Authorize]`, policies, and other schemes.
- Never compare keys with `==`; hash the presented key and compare with `CryptographicOperations.FixedTimeEquals` to defeat timing attacks.
- Store only SHA-256 hashes of keys, one key per client, so keys can be revoked and rotated individually.
- Send keys in a header such as `X-Api-Key`, never in the query string, and only over HTTPS.
- API keys identify applications, not users — use OAuth 2.0 / JWT when you need user identity, scopes, or expiry.
- Combine schemes when it fits: API keys for server-to-server integrations, JWT for user-facing endpoints.
