# ASP.NET Core Identity Password and Lockout

ASP.NET Core Identity ships with configurable password rules and account lockout. Both live in the `IdentityOptions` passed to `AddIdentity` (or `AddDefaultIdentity`), so you set them once at startup and they apply to every registration, password change, and sign-in.

## Password Policy

The defaults are strict: a password must be at least 6 characters, contain a digit, a lowercase letter, an uppercase letter, and a non-alphanumeric character, and have at least 1 unique character. Tighten or relax them in `AddIdentity`:

```csharp
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 12;
    options.Password.RequiredUniqueChars = 4;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();
```

Each setting:

- `RequiredLength`: minimum total length. 12 is a reasonable modern minimum; length matters more than character classes.
- `RequiredUniqueChars`: minimum number of distinct characters. Setting this to 4 rejects `aaaaaaaaaaaa` even though it is 12 characters long.
- `RequireDigit`, `RequireLowercase`, `RequireUppercase`, `RequireNonAlphanumeric`: each forces at least one character of that class. Requiring all four frustrates users and pushes them toward predictable patterns like `Password1!`, so many apps now require only length plus a uniqueness check.

Validation runs through `PasswordValidator<TUser>`. To add your own rule, for example rejecting passwords found in a breach list, implement `IPasswordValidator<TUser>` and register it:

```csharp
public sealed class BreachedPasswordValidator : IPasswordValidator<IdentityUser>
{
    public Task<IdentityResult> ValidateAsync(UserManager<IdentityUser> manager,
        IdentityUser user, string? password)
    {
        if (password is not null && BreachedList.Contains(password))
            return Task.FromResult(IdentityResult.Failed(new IdentityError
            {
                Code = "BreachedPassword",
                Description = "This password has appeared in a data breach."
            }));
        return Task.FromResult(IdentityResult.Success);
    }
}

builder.Services.AddTransient<IPasswordValidator<IdentityUser>, BreachedPasswordValidator>();
```

## Lockout

Lockout blocks sign-in after repeated failed attempts, which stops credential-stuffing and brute-force attacks. It is off by default for new users (`AllowedForNewUsers = false`), so enable it deliberately:

```csharp
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();
```

- `MaxFailedAccessAttempts`: failed sign-ins before lockout. 5 is the common default.
- `DefaultLockoutTimeSpan`: how long the account stays locked. 15 minutes is typical; longer spans (an hour or more) punish real users who mistype.
- `AllowedForNewUsers`: when true, every new account gets lockout enabled. Individual users can also be opted in or out with `UserManager.SetLockoutEnabledAsync`.

How it behaves at sign-in: `SignInManager.PasswordSignInAsync` returns `SignInResult.LockedOut` once the limit is hit, and `IsLockedOut` stays true until the lockout end time passes. A successful sign-in resets the failed-access counter to zero. Note that lockout only counts failures against the same user record, so an attacker can still try one password against many accounts; rate limiting at the endpoint is the complementary defence.

An administrator can unlock an account immediately:

```csharp
await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow);
await userManager.ResetAccessFailedCountAsync(user);
```

## Two-Factor Interplay

Two-factor authentication (2FA) and lockout work together: the password check happens first, and only a correct password leads to the 2FA prompt. Failed 2FA code entries do **not** count toward `MaxFailedAccessAttempts` by default; they are counted separately by the authenticator flow, and repeated 2FA failures return the user to the password step. This means lockout protects the password, while 2FA protects the account even if the password is known.

Enable 2FA for a user with `UserManager.SetTwoFactorEnabledAsync(user, true)`, and check it at sign-in with `SignInManager.GetTwoFactorAuthenticationUserAsync()`. Recovery codes should be offered alongside authenticator apps, because a user who loses their phone and has no recovery code is locked out permanently without admin help.

## Key Takeaways

- Configure password rules and lockout in the `IdentityOptions` passed to `AddIdentity`.
- Prefer a longer minimum length (12+) over demanding every character class.
- Enable lockout explicitly with `AllowedForNewUsers = true`, 5 attempts, and a 15-minute lockout as a sane starting point.
- A successful sign-in resets the failed-access counter; admins can clear a lockout with `SetLockoutEndDateAsync`.
- Lockout guards the password step; 2FA guards the account after the password, and the two combine rather than overlap.
