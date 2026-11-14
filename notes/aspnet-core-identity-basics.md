# ASP.NET Core Identity Basics (ASP.NET Core)

ASP.NET Core Identity is the membership system built into ASP.NET Core: user accounts, password hashing, roles, claims, email confirmation, two-factor authentication, and external logins. It stores users in a database through EF Core by default, and plugs into cookie authentication so a signed-in user stays signed in across requests.

## The Core Types

`IdentityUser` carries the account fields — `UserName`, `Email`, `PasswordHash`, `EmailConfirmed`, `PhoneNumber`, lockout and two-factor flags. `IdentityRole` represents a role such as `Admin` or `Editor`. Both can be subclassed to add custom columns, for example a `DisplayName` on the user. `IdentityDbContext<TUser>` adds the `DbSet`s for users, roles, claims, logins, and tokens, and configures their relationships.

```csharp
// Custom user with one extra profile field.
public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
}

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }
}
```

## Registering Identity

`AddIdentity` registers `UserManager<TUser>`, `SignInManager<TUser>`, the stores, and the token providers, and wires cookie authentication as the default scheme. `AddEntityFrameworkStores` tells Identity to persist through your `DbContext`. `AddDefaultTokenProviders` registers the providers used for email confirmation, password reset, and two-factor codes — without it, `GenerateEmailConfirmationTokenAsync` throws at runtime.

```csharp
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 8;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.SignIn.RequireConfirmedEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();
```

## UserManager and SignInManager

`UserManager<TUser>` manages accounts: creating users, checking and changing passwords, assigning roles, and generating confirmation tokens. `SignInManager<TUser>` handles the sign-in session: password sign-in, sign-out, two-factor flows, and external logins. Passwords are never stored in plain text — `UserManager` hashes them with PBKDF2 via `IPasswordHasher<TUser>` before they reach the database.

```csharp
public class AccountController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signIn;
    public AccountController(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn)
        => (_users, _signIn) = (users, signIn);

    [HttpPost("register")]
    public async Task<IActionResult> Register(string email, string password)
    {
        var user = new ApplicationUser { UserName = email, Email = email };
        var result = await _users.CreateAsync(user, password);
        if (!result.Succeeded)
            return BadRequest(result.Errors.Select(e => e.Description));
        await _users.AddToRoleAsync(user, "Member");
        return Ok("Account created. Confirm your email to sign in.");
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(string email, string password)
    {
        var result = await _signIn.PasswordSignInAsync(
            email, password, isPersistent: false, lockoutOnFailure: true);
        if (result.Succeeded) return Ok("Signed in.");
        if (result.IsLockedOut) return StatusCode(423, "Account locked out.");
        return Unauthorized("Invalid credentials.");
    }
}
```

`PasswordSignInAsync` with `lockoutOnFailure: true` counts failed attempts toward the configured lockout — the main defence against brute-force password guessing. Always return the same generic "invalid credentials" message for a wrong password and a non-existent user, so an attacker cannot enumerate accounts.

## Cookie Authentication

Identity's sign-in writes an encrypted authentication cookie holding a claims principal (user id, name, roles), validated on every request. Configure its lifetime and paths in `Program.cs` — the login path is where unauthenticated users are redirected, and sliding expiration renews the cookie on activity. `HttpOnly` keeps JavaScript from reading the cookie, which blocks the most common cookie-theft path from XSS; in production the cookie must also be `Secure` (HTTPS only).

```csharp
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/account/login";
    options.AccessDeniedPath = "/account/denied";
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
});
```

## Roles and Authorization

Roles are claims in the cookie, so `[Authorize(Roles = "Admin")]` works with no extra code once the role exists. Seed roles at startup with a scoped `RoleManager<IdentityRole>`, and check them in code with `UserManager.IsInRoleAsync` or `User.IsInRole`. For finer control, prefer policy-based authorization (`[Authorize(Policy = "CanEditPosts")]`) over scattering role names through controllers — policies are defined once in `Program.cs` and can combine roles, claims, and custom requirements.

```csharp
// Seed the Admin role at startup.
using (var scope = app.Services.CreateScope())
{
    var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    if (!await roles.RoleExistsAsync("Admin"))
        await roles.CreateAsync(new IdentityRole("Admin"));
}

[Authorize(Roles = "Admin")]
[HttpGet("admin/users")]
public IActionResult ListUsers([FromServices] UserManager<ApplicationUser> users)
    => Ok(users.Users.Select(u => new { u.Id, u.Email, u.DisplayName }));
```

## Key Takeaways

- Identity gives you users, roles, password hashing, and cookie sign-in through `UserManager` and `SignInManager`.
- Register with `AddIdentity` + `AddEntityFrameworkStores` + `AddDefaultTokenProviders`, and configure password and lockout policy in the same call.
- Never store or compare plain-text passwords; `UserManager` hashes with PBKDF2.
- Lock out after a few failed attempts, require confirmed email, and keep the auth cookie `HttpOnly` and HTTPS-only.
- Roles are claims: seed them once, authorize with `[Authorize(Roles = ...)]`, and move to policies when rules get complex.
