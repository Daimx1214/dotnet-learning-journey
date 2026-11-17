# Feature Flags in ASP.NET Core (ASP.NET Core)

A feature flag is a runtime switch that turns a feature on or off without redeploying. The classic alternative — a boolean in configuration — works until you need a gradual rollout ("enable the new checkout for 10% of users"), a kill switch you can flip in production in seconds, or a flag that depends on who the user is. `Microsoft.FeatureManagement` adds exactly that: flags defined in configuration, evaluated through `IFeatureManager`, and enforceable at the controller or endpoint level with `[FeatureGate]`.

## Defining Flags in Configuration

Flags live in `appsettings.json` under `FeatureManagement`. The simplest flag is a boolean; richer flags add filters that decide *for whom* the flag is on.

```json
{
  "FeatureManagement": {
    "NewCheckout": true,
    "BetaDashboard": {
      "EnabledFor": [
        {
          "Name": "Percentage",
          "Parameters": {
            "Value": 25
          }
        }
      ]
    }
  }
}
```

`NewCheckout` is a plain on/off switch. `BetaDashboard` uses the built-in `Percentage` filter: it is enabled for roughly 25% of evaluation contexts. The percentage filter hashes a context value (by default the user identity, or a fixed value when there is none), so the same user consistently sees the same result instead of flipping randomly on every request.

## Checking Flags in Code

Register the library once in `Program.cs`, then inject `IFeatureManager` wherever a decision is needed. `IsEnabledAsync` returns the flag's evaluated state for the current request.

```csharp
// Program.cs
builder.Services.AddFeatureManagement();

// A service that branches on a flag.
public sealed class CheckoutService
{
    private readonly IFeatureManager _features;

    public CheckoutService(IFeatureManager features) => _features = features;

    public async Task<string> GetCheckoutPageAsync()
    {
        bool useNewCheckout = await _features.IsEnabledAsync("NewCheckout");
        return useNewCheckout ? "CheckoutV2" : "CheckoutV1";
    }
}
```

For a cleaner API, the library also supports strongly-typed flags via an enum, so flag names are compile-time checked instead of magic strings — worth adopting once a project has more than a handful of flags.

## Gating Endpoints with [FeatureGate]

When a whole controller or action belongs to the feature, `[FeatureGate]` is cleaner than an `if` inside the action. A request to a gated endpoint while the flag is off returns 404 by default, so the feature is invisible rather than erroring.

```csharp
[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    // Only reachable while the BetaDashboard flag is on.
    [HttpGet("beta")]
    [FeatureGate("BetaDashboard")]
    public IActionResult Beta() => Ok(new { Message = "Welcome to the beta dashboard." });

    [HttpGet("stable")]
    public IActionResult Stable() => Ok(new { Message = "Stable dashboard." });
}
```

## Percentage Rollouts and Custom Filters

The built-in filters cover the common rollout shapes: `Percentage` (a share of users), `TimeWindow` (on between two dates — ideal for a launch at a scheduled time), and `Targeting` (on for specific users, groups, or a percentage of a group, backed by `ITargetingContextAccessor`). When none of those fits, a custom filter is a small class implementing `IFeatureFilter`:

```csharp
public sealed class WeekdayOnlyFilter : IFeatureFilter
{
    public Task<bool> EvaluateAsync(FeatureFilterEvaluationContext context)
    {
        // Enable the feature on weekdays only.
        DayOfWeek today = DateTime.UtcNow.DayOfWeek;
        bool isWeekday = today is not DayOfWeek.Saturday and not DayOfWeek.Sunday;
        return Task.FromResult(isWeekday);
    }
}

// Registration:
builder.Services.AddFeatureManagement()
    .AddFeatureFilter<WeekdayOnlyFilter>();
```

Registering the filter makes it available by name — the class name minus the `Filter` suffix (`"WeekdayOnly"`) — in the `EnabledFor` list. Custom filters can read anything from DI, so a filter can consult the database, the current user's claims, or an external config store.

## Why Flags Beat Config Booleans

A config boolean is a flag with exactly one filter ("always on" or "always off") and no evaluation context. Feature flags add the missing pieces: per-user targeting, percentage rollouts, time windows, and a single evaluation API that every layer shares. The practical payoff is safe rollouts — ship the new code behind a flag at 0%, raise it to 10%, watch the metrics, then ramp to 100% — and an instant kill switch: if the new checkout breaks at 2 a.m., flipping one value in configuration (or in Azure App Configuration, which the library integrates with) turns it off without a redeploy. The discipline that makes flags safe is cleanup: a flag that has been at 100% for weeks should be removed and its old code path deleted, or the codebase accumulates dead branches.

## Key Takeaways

- Feature flags (`Microsoft.FeatureManagement`) turn features on or off at runtime, without redeploying.
- Define flags under `FeatureManagement` in `appsettings.json`; check them with `IFeatureManager.IsEnabledAsync`, or gate whole endpoints with `[FeatureGate]`.
- Built-in filters cover the common shapes: `Percentage` for gradual rollouts, `TimeWindow` for scheduled launches, `Targeting` for specific users or groups.
- Custom filters implement `IFeatureFilter` and can base the decision on anything — the calendar, the user's claims, or a database lookup.
- Flags beat plain config booleans because they support per-user evaluation and gradual rollouts, plus an instant kill switch in production.
- Clean up finished flags: once a feature is at 100%, remove the flag and delete the old code path, or dead branches accumulate.
