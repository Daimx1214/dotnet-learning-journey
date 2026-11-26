# ASP.NET Core Identity: Roles and Claims

ASP.NET Core Identity stores users, passwords, roles, and claims, and plugs into the authorization system so controllers can require a role or a claim. Roles answer "what group is this user in", claims answer "what is true about this user". Both are checked the same way by the framework, but they model different things.

## Setting Up Roles

Roles are not on by default. Add them when registering Identity:

```csharp
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();
```

`AddIdentity<ApplicationUser, IdentityRole>` registers `UserManager<ApplicationUser>`, `SignInManager<ApplicationUser>`, and `RoleManager<IdentityRole>`. Without the `IdentityRole` type argument, `RoleManager` is not registered and role APIs are unavailable. If the app uses `AddDefaultIdentity` instead, call `.AddRoles<IdentityRole>()` on the returned builder to enable roles.

## RoleManager: Creating and Managing Roles

`RoleManager<IdentityRole>` manages the roles themselves:

```csharp
public sealed class RoleSeeder
{
    private readonly RoleManager<IdentityRole> _roles;

    public RoleSeeder(RoleManager<IdentityRole> roles) => _roles = roles;

    public async Task SeedAsync()
    {
        foreach (var name in new[] { "Admin", "Editor", "Viewer" })
        {
            if (!await _roles.RoleExistsAsync(name))
            {
                var result = await _roles.CreateAsync(new IdentityRole(name));
                if (!result.Succeeded)
                    throw new InvalidOperationException($"Could not create role {name}.");
            }
        }
    }
}
```

`RoleExistsAsync` checks before creating, so seeding is safe to run on every startup.

## UserManager: Assigning Roles to Users

`UserManager<TUser>` assigns roles to a specific user:

```csharp
var user = await _users.FindByEmailAsync("admin@example.com");
if (user is not null)
{
    var result = await _users.AddToRoleAsync(user, "Admin");
    if (!result.Succeeded)
        throw new InvalidOperationException("Could not assign the Admin role.");
}
```

Useful related methods on `UserManager<TUser>`: `GetRolesAsync(user)` returns the user's role names, `IsInRoleAsync(user, "Admin")` checks one role, `RemoveFromRoleAsync(user, "Editor")` removes a role, and `AddToRolesAsync(user, new[] { "Editor", "Viewer" })` assigns several at once.

Role changes take effect on the next sign-in by default, because roles are baked into the authentication cookie at sign-in time. To apply them immediately, call `SignInManager.RefreshSignInAsync(user)` after changing roles.

## Authorizing by Role

The simplest check is the `Authorize` attribute with a role name:

```csharp
[Authorize(Roles = "Admin")]
public sealed class AdminController : ControllerBase
{
    [HttpGet("users")]
    public IActionResult ListUsers() => Ok();
}
```

Multiple roles use a comma, and the comma means OR:

```csharp
[Authorize(Roles = "Admin,Editor")]
[HttpPost("articles")]
public IActionResult CreateArticle() => Ok();
```

A user in either role passes. Requiring two roles at once (AND) needs a policy instead of a comma list.

## Claims: Finer-Grained Facts

A claim is a name-value pair about a user, such as `Department = Sales` or `ClearanceLevel = 3`. Claims fit facts that are not groups: a permission, a department, an age, a subscription tier. Add a claim to a user with `UserManager`:

```csharp
var claim = new Claim("Department", "Sales");
var result = await _users.AddClaimAsync(user, claim);
```

Claims are also baked into the cookie at sign-in, so the same refresh rule applies after changes.

## Claims-Based Policies

Policies turn a claim requirement into a named rule in `Program.cs`:

```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SalesOnly", policy =>
        policy.RequireClaim("Department", "Sales"));
    options.AddPolicy("CanDeleteArticles", policy =>
        policy.RequireClaim("Permission", "Articles.Delete"));
    options.AddPolicy("SeniorEditor", policy =>
        policy.RequireRole("Editor")
              .RequireClaim("ClearanceLevel", "3"));
});
```

Use the policy on a controller or endpoint:

```csharp
[Authorize(Policy = "SalesOnly")]
[HttpGet("sales-report")]
public IActionResult SalesReport() => Ok();
```

The `SeniorEditor` policy shows how policies combine roles and claims: the user must be an Editor AND have clearance level 3.

## Roles vs Claims: Which to Use

- **Roles** for broad groups that change rarely: Admin, Editor, Viewer. They are easy to read in an attribute and easy to explain to non-developers.
- **Claims** for fine-grained permissions and user facts: `Articles.Delete`, `Department = Sales`, a clearance level. They scale better when permissions grow, because adding a new permission is a new claim value, not a new role.
- **Policies** on top of both: name the rule once (`CanDeleteArticles`) and use the name everywhere, so changing the rule means editing one policy instead of every attribute.

A common mistake is creating a role per permission, which leads to dozens of roles. If a check reads like a permission ("can delete articles"), model it as a claim and a policy. If it reads like a job ("is an admin"), model it as a role.

## Key Takeaways

- Enable roles with `AddIdentity<ApplicationUser, IdentityRole>` or `.AddRoles<IdentityRole>()`.
- `RoleManager<IdentityRole>` manages roles; `UserManager<TUser>.AddToRoleAsync` assigns them to users.
- `[Authorize(Roles = "Admin")]` checks a role, and a comma list means OR, not AND.
- Claims model fine-grained facts like permissions and departments; use `AddClaimAsync` to assign them.
- Build policies with `AddPolicy` and `RequireClaim` or `RequireRole`, then authorize by policy name for rules that combine both.
