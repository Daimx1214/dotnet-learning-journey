# Cookie Authentication in ASP.NET Core

Cookie authentication is the classic way to keep a user signed in to a web application: after login, the server issues an encrypted cookie, and the browser sends it back with every request so the server knows who the user is. ASP.NET Core's cookie authentication handler manages the whole lifecycle — issuing, validating, renewing, and expiring the cookie — so you only write the login and logout logic.

It matters because almost every server-rendered web app (Razor Pages, MVC, Blazor Server) needs it, and it is also the foundation that ASP.NET Core Identity builds on. Understanding the raw handler first makes Identity's behaviour far less mysterious.

## Setting It Up

Register the scheme in `Program.cs` with `AddAuthentication` and `AddCookie`, then add the middleware — `UseAuthentication` must come before `UseAuthorization`.

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
```

The paths matter: when an anonymous user hits a protected page, the handler redirects them to `LoginPath` (and back afterwards via a `ReturnUrl` query string). `AccessDeniedPath` is used when a signed-in user lacks the required role or policy.

## Login and Logout

Signing in means building a `ClaimsPrincipal` and calling `SignInAsync`. The claims inside the cookie are what the rest of the app sees as the user's identity.

```csharp
public class AccountController : Controller
{
    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl)
    {
        var user = await _userService.ValidateAsync(model.Email, model.Password);
        if (user is null)
        {
            ModelState.AddModelError("", "Invalid email or password.");
            return View(model);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.DisplayName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role) // e.g. "Admin"
        };

        var identity = new ClaimsIdentity(claims,
            CookieAuthenticationDefaults.AuthenticationScheme);

        var properties = new AuthenticationProperties
        {
            IsPersistent = model.RememberMe, // survive browser close if true
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            properties);

        return LocalRedirect(returnUrl ?? "/");
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(
            CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }
}
```

A few things worth noting here:

- `IsPersistent` controls whether the cookie survives the browser being closed. Without it, the cookie is a session cookie and disappears when the browser closes, even if `ExpireTimeSpan` is long.
- `LocalRedirect` (not `Redirect`) protects against open-redirect attacks — it refuses to send the user to an external URL smuggled in via `returnUrl`.
- Logout should be a POST, not a GET link, so a malicious page cannot log the user out with a single image tag.

## Expiration and Sliding Expiration

Two settings control lifetime:

- `ExpireTimeSpan` — how long the cookie is valid from issue (default 14 days).
- `SlidingExpiration` — when `true`, each request made after half the lifetime has passed re-issues the cookie with a fresh expiry. An active user stays signed in; an idle user is eventually signed out.

```csharp
options.ExpireTimeSpan = TimeSpan.FromHours(8);
options.SlidingExpiration = true;
```

With an 8-hour expiry and sliding expiration, a request after 4+ hours of activity renews the cookie for another 8 hours. For sensitive applications (banking, admin panels), set `SlidingExpiration = false` and a short `ExpireTimeSpan` so sessions end predictably.

You can also validate the cookie on every request with `options.Events.OnValidatePrincipal` — for example, to check a "security stamp" and reject the cookie if the user changed their password or was disabled. This is how Identity implements its security-stamp check.

## Cookie Settings That Protect You

| Setting | What it does |
|---|---|
| `Cookie.HttpOnly = true` | JavaScript cannot read the cookie — blocks XSS token theft |
| `Cookie.SameSite = Lax` | Cookie is not sent on cross-site POSTs — blocks most CSRF |
| `Cookie.SecurePolicy = Always` | Cookie is only sent over HTTPS — blocks network sniffing |
| `Cookie.Name` | Give it an app-specific name (default `.AspNetCore.Cookies`) |

`SameSiteMode.Lax` is the sensible default: the cookie is sent on top-level navigations (so links from other sites still work) but not on cross-site form posts. `Strict` is safer but breaks legitimate flows like returning from an external login provider, so use it only if you understand the trade-off.

Always set `SecurePolicy.Always` in production. A cookie sent over plain HTTP can be intercepted, and an attacker with your cookie *is* you until it expires.

## Protecting Pages and Endpoints

Use `[Authorize]` on controllers, actions, or Razor Pages. The handler redirects anonymous users to the login page automatically.

```csharp
[Authorize]
public class DashboardController : Controller
{
    public IActionResult Index() => View();

    [Authorize(Roles = "Admin")]
    public IActionResult AdminPanel() => View();

    [Authorize(Policy = "CanManageProducts")]
    public IActionResult ManageProducts() => View();
}

[AllowAnonymous]
public IActionResult PublicPage() => View();
```

Policies are registered alongside authorization and can combine requirements:

```csharp
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("CanManageProducts", policy =>
        policy.RequireRole("Admin").RequireClaim("department", "Sales"));
```

In Razor views and pages, check the user directly with `User.Identity?.IsAuthenticated` or `User.IsInRole("Admin")` to show or hide UI elements — but remember UI hiding is cosmetic; the `[Authorize]` attribute is the real enforcement.

## What Is Inside the Cookie

The cookie does not contain the user's password or a session id pointing at server state. It contains the claims themselves, encrypted and signed with the app's Data Protection keys:

- Encrypted, so the contents cannot be read if intercepted.
- Signed, so tampering is detected and the cookie is rejected.

Because the cookie is self-contained, authentication works across multiple servers — as long as they share Data Protection keys (configure a shared key store, e.g. Redis or Azure Blob Storage, in a multi-server deployment). The trade-off is size: every claim you add makes every request slightly bigger, so keep claims small and look up extra data from the database when needed.

## Key Takeaways

- Register cookie auth with `AddAuthentication` + `AddCookie`, and call `UseAuthentication()` before `UseAuthorization()`.
- Login builds a `ClaimsPrincipal` and calls `SignInAsync`; logout calls `SignOutAsync` and should be a POST.
- `SlidingExpiration` renews the cookie for active users; disable it and shorten `ExpireTimeSpan` for sensitive apps.
- Set `HttpOnly`, `SameSite=Lax`, and `SecurePolicy.Always` — these three settings block the most common cookie attacks.
- Protect endpoints with `[Authorize]`, roles, and policies; hiding UI elements is cosmetic, the attribute is the enforcement.
- The cookie holds encrypted, signed claims and works across servers if Data Protection keys are shared — so keep claims small.
