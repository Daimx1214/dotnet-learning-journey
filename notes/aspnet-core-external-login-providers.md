# External Login Providers in ASP.NET Core

External login providers let users sign in to your application with an account they already have — Google, GitHub, Microsoft, Facebook — instead of creating yet another password. ASP.NET Core supports this through its authentication system, and ASP.NET Core Identity adds the account-linking layer: an external login is stored as a link on a local user account, so one person can sign in with Google today and a password tomorrow.

It matters because users abandon registration forms that demand a new password, and because a well-known provider's sign-in is usually more secure than a homegrown password flow: the provider handles multi-factor authentication, breach detection, and account recovery.

## How the OAuth Flow Works

External login uses OAuth 2.0 (with OpenID Connect on top for Google and Microsoft). The flow has four steps:

1. **Redirect out:** your app sends the user to the provider's sign-in page, with your app's client id and a return URL.
2. **User signs in:** the user authenticates with the provider (and approves your app if asked).
3. **Callback:** the provider redirects back to your app with an authorization code.
4. **Exchange and sign in:** your app exchanges the code for tokens, reads the user's profile (name, email), finds or creates a local user, and signs them in with a cookie.

You never see the user's provider password. The only things your app receives are the tokens and the profile claims the provider chooses to share.

## Registering with the Provider

Before writing code, register your application with each provider to get a **client id** and **client secret**:

- **Google:** Google Cloud Console → APIs & Services → Credentials → Create OAuth client id (Web application). Add your callback URL, e.g. `https://localhost:5001/signin-google`.
- **GitHub:** Settings → Developer settings → OAuth Apps → New OAuth App. Authorization callback URL: `https://localhost:5001/signin-github`.

Store the secret in user secrets or an environment variable, never in source control:

```bash
dotnet user-secrets set "Authentication:Google:ClientId" "YOUR_GOOGLE_CLIENT_ID"
dotnet user-secrets set "Authentication:Google:ClientSecret" "YOUR_GOOGLE_CLIENT_SECRET"
dotnet user-secrets set "Authentication:GitHub:ClientId" "YOUR_GITHUB_CLIENT_ID"
dotnet user-secrets set "Authentication:GitHub:ClientSecret" "YOUR_GITHUB_CLIENT_SECRET"
```

## Wiring Up the Providers

Register the providers alongside your cookie scheme. Each `AddGoogle` / `AddGitHub` call adds an authentication handler whose default callback path is `/signin-google` and `/signin-github` respectively — those paths must match what you registered with the provider.

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie()
    .AddGoogle(options =>
    {
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"]!;
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]!;
        // Ask for the email claim explicitly (Google includes it by default).
        options.Scope.Add("email");
    })
    .AddGitHub(options =>
    {
        options.ClientId = builder.Configuration["Authentication:GitHub:ClientId"]!;
        options.ClientSecret = builder.Configuration["Authentication:GitHub:ClientSecret"]!;
        options.Scope.Add("user:email");
    });

builder.Services.AddAuthorization();
```

With plain cookie authentication (no Identity), you handle the callback yourself. With Identity, the framework provides `ExternalLogin` pages and the linking logic, which is the recommended route for real apps.

## Starting the Login

The login button issues a challenge to the provider scheme. `ChallengeAsync` builds the redirect to the provider and stores a return URL so the user lands back where they started.

```csharp
[HttpPost]
public IActionResult ExternalLogin(string provider, string? returnUrl)
{
    var redirectUrl = Url.Action("ExternalLoginCallback", "Account",
        new { returnUrl });
    var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
    return Challenge(properties, provider); // provider is "Google" or "GitHub"
}
```

In a Razor view, the provider buttons are just forms posting the provider name:

```html
<form asp-action="ExternalLogin" method="post">
    <button type="submit" name="provider" value="Google">Sign in with Google</button>
    <button type="submit" name="provider" value="GitHub">Sign in with GitHub</button>
</form>
```

## Handling the Callback

When the provider redirects back, the external scheme holds a temporary identity containing the provider's claims. Read it with `AuthenticateAsync` on the external scheme, extract the provider key (the user's id at that provider) and the email, then decide: link to an existing local user, or create a new one.

```csharp
public async Task<IActionResult> ExternalLoginCallback(string? returnUrl)
{
    // Read the temporary identity from the external provider.
    var result = await HttpContext.AuthenticateAsync(
        IdentityConstants.ExternalScheme);
    if (!result.Succeeded)
        return RedirectToAction("Login");

    var externalPrincipal = result.Principal!;
    var providerKey = externalPrincipal.FindFirstValue(ClaimTypes.NameIdentifier)!;
    var provider = result.Properties?.Items["LoginProvider"] ?? "Google";
    var email = externalPrincipal.FindFirstValue(ClaimTypes.Email);

    var user = await _userManager.FindByLoginAsync(provider, providerKey);
    if (user is null)
    {
        // No linked account yet — create the user and link the login.
        user = await _userManager.FindByEmailAsync(email ?? "");
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true // the provider already verified the email
            };
            await _userManager.CreateAsync(user);
        }

        var loginInfo = new UserLoginInfo(provider, providerKey, provider);
        await _userManager.AddLoginAsync(user, loginInfo);
    }

    await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
    await _signInManager.SignInAsync(user, isPersistent: false);
    return LocalRedirect(returnUrl ?? "/");
}
```

The important details:

- `FindByLoginAsync(provider, providerKey)` is the lookup — the (provider, provider key) pair is the stable identity, not the email. Emails can change at the provider; the key does not.
- If no linked account exists but a local user with the same email does, link the external login to that existing account rather than creating a duplicate. Only do this after the email is confirmed by the provider, otherwise anyone could claim someone else's account by registering with their email at a provider that does not verify it.
- Clear the temporary external cookie (`SignOutAsync` on the external scheme) once you have read it.

## Linking an Additional Provider to a Signed-In User

A signed-in user can connect a second provider from their account settings. The flow is the same challenge/callback, but the callback links the new provider to the *current* user instead of signing anyone in.

```csharp
[Authorize]
[HttpPost]
public IActionResult LinkLogin(string provider)
{
    var redirectUrl = Url.Action("LinkLoginCallback", "Account");
    var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
    return Challenge(properties, provider);
}

[Authorize]
public async Task<IActionResult> LinkLoginCallback()
{
    var result = await HttpContext.AuthenticateAsync(
        IdentityConstants.ExternalScheme);
    if (!result.Succeeded)
        return RedirectToAction("Manage");

    var info = await _signInManager.GetExternalLoginInfoAsync();
    if (info is null)
        return RedirectToAction("Manage");

    var user = await _userManager.GetUserAsync(User);
    var addResult = await _userManager.AddLoginAsync(user!, info);
    // addResult.Succeeded is false if the provider account is already linked
    // to a different user — surface that as an error, not a silent success.

    await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
    return RedirectToAction("Manage");
}
```

## Security Checklist

- **Never trust email alone:** the (provider, provider key) pair identifies the user; treat email as a hint, and only auto-link accounts when the provider has verified the email.
- **Use HTTPS everywhere:** the callback carries authorization codes; over plain HTTP they can be intercepted.
- **Keep secrets out of source control:** client secrets belong in user secrets, environment variables, or a key vault.
- **Validate the callback:** ASP.NET Core checks the `state` parameter (anti-CSRF) automatically — do not disable it.
- **Handle provider errors:** a user can deny consent at the provider; the callback then carries `error=access_denied`, so handle the failure case instead of crashing.
- **Revoke on unlink:** when a user removes a linked provider, remove the login record with `RemoveLoginAsync` so the old link cannot be reused.

## Key Takeaways

- External logins use OAuth 2.0 / OpenID Connect: your app never sees the user's provider password, only tokens and profile claims.
- Register your app with each provider first to get a client id and secret, and keep the secret out of source control.
- `AddGoogle` / `AddGitHub` register handlers whose callback paths (`/signin-google`, `/signin-github`) must match the provider registration.
- Identify users by the (provider, provider key) pair, not by email — emails can change, the provider key is stable.
- Only auto-link an external login to an existing local account when the provider has verified the email address.
- A signed-in user can link extra providers from account settings; removing a link requires `RemoveLoginAsync`.
