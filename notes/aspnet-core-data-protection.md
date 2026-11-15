# ASP.NET Core Data Protection (ASP.NET Core)

The Data Protection APIs encrypt and tamper-proof small pieces of data — cookie values, antiforgery tokens, password-reset links, and any short-lived token your app generates. The central type is `IDataProtector`: `Protect` encrypts a payload into an opaque string, `Unprotect` reverses it and throws if the payload was altered or the key is gone. The same APIs also back ASP.NET Core's authentication cookies and session state, so configuring them correctly matters for every app that runs on more than one server.

## IDataProtector Basics

A protector is created from the `IDataProtectionProvider` with a purpose string. Purposes isolate payloads: data protected with purpose `"PasswordReset"` cannot be unprotected with purpose `"EmailChange"`, even by the same app. Chain purposes with `CreateProtector` to build a hierarchy.

```csharp
using Microsoft.AspNetCore.DataProtection;

public class TokenService
{
    private readonly IDataProtector _protector;

    public TokenService(IDataProtectionProvider provider)
    {
        // Purpose strings namespace the payload; use a stable, specific value.
        _protector = provider.CreateProtector("MyApp.PasswordReset.v1");
    }

    public string CreateResetToken(string userId) =>
        _protector.Protect(userId);

    public string? ReadResetToken(string token)
    {
        try
        {
            return _protector.Unprotect(token);
        }
        catch (CryptographicException)
        {
            return null; // tampered, expired key, or wrong purpose
        }
    }
}
```

`Protect` output is a base64 string that embeds the key id used, so the system knows which key to use for decryption. Protection is authenticated encryption (AES-256-CBC with HMAC-SHA256 by default), so any modification is detected on unprotect. There is no built-in expiry: if a token must expire, embed the expiry time in the payload and check it after unprotecting.

## Key Storage and Key Rotation

Keys live in a key ring, and the ring must be persisted somewhere durable — by default, keys are stored under the user profile or the registry, which is lost on container restarts and is not shared between servers. For a web farm or a containerized app, persist keys to a shared store such as a folder, Azure Blob Storage, or Redis, and protect the keys at rest with a certificate or Azure Key Vault.

```csharp
// Program.cs — persist keys to a shared folder (web farm / containers).
using Microsoft.AspNetCore.DataProtection;

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo("/var/data-protection-keys"))
    .SetApplicationName("MyApp"); // keys are isolated per application name

// With a certificate protecting the key ring at rest:
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo("/var/data-protection-keys"))
    .ProtectKeysWithCertificate("thumbprint-of-cert")
    .SetApplicationName("MyApp");
```

Keys rotate automatically: a new key is generated every 90 days and becomes the default for new payloads, while old keys stay in the ring for decryption. Old keys can be revoked explicitly if one is compromised — but revoking a key permanently breaks every payload encrypted with it, including all auth cookies, so treat revocation as a deliberate, user-visible event.

## Data Protection in Cookies and Auth

Authentication cookies, antiforgery tokens, and session state all flow through the same key ring, which is why a key-ring mismatch logs users out. Two concrete consequences follow:

- Every instance of the app must share the same key ring **and** the same application name, or a cookie issued by server A will fail validation on server B.
- Changing the application name or wiping the key store invalidates every outstanding cookie and token — effectively logging out all users.

## Key Takeaways

- `IDataProtector.Protect` / `Unprotect` gives authenticated encryption for small payloads; tampering throws `CryptographicException`.
- Purpose strings isolate payloads — protect and unprotect must use the same purpose chain.
- Persist the key ring to a shared, durable store (and protect it at rest) for multi-server or containerized apps; the default local store is not shared.
- Keys rotate every 90 days automatically; old keys remain for decryption, and revoking one invalidates everything it encrypted.
- Set a stable `ApplicationName` and share the ring across instances, or auth cookies will fail on other servers.
