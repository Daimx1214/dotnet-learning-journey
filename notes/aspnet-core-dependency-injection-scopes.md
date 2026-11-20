# ASP.NET Core Dependency Injection Scopes

Dependency injection is the backbone of ASP.NET Core. Choosing the wrong service lifetime is one of the most common sources of subtle bugs: stale state, disposed objects, or memory that grows without bound.

## The Three Lifetimes

Every registered service gets one of three lifetimes, and the container enforces the rules around them.

```csharp
builder.Services.AddTransient<IReportBuilder, ReportBuilder>();   // new instance every time
builder.Services.AddScoped<IOrderService, OrderService>();        // one per HTTP request
builder.Services.AddSingleton<ICache, MemoryCache>();             // one for the app lifetime
```

Transient services are created fresh on every resolution. Scoped services live for one request (or one explicit scope). Singletons are created once and shared everywhere. A scoped service resolved twice in the same request returns the same instance; in a different request it is a different instance.

## The Captive Dependency Problem

A singleton that depends on a scoped service captures it forever. The scoped service was designed to live for one request, but the singleton holds it for the lifetime of the app — so request-specific state (like a DbContext) leaks across requests, and worse, it never gets disposed.

```csharp
// BAD: singleton captures a scoped DbContext
builder.Services.AddSingleton<ReportService>();           // depends on AppDbContext
builder.Services.AddScoped<AppDbContext>();

// GOOD: depend on a factory instead
public class ReportService(IServiceScopeFactory scopes)
{
    public void Run()
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // use db inside this scope only
    }
}
```

ASP.NET Core can catch this at startup. `ValidateScopes` throws when a singleton (directly or indirectly) depends on a scoped service, and `ValidateOnBuild` catches other registration mistakes early.

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;      // fail fast on captive dependencies
    options.ValidateOnBuild = true;
});
```

Enable validation in development at minimum; the small startup cost is worth the safety.

## Scoped Services in Background Services

A hosted service is a singleton, so it cannot inject a scoped service directly. Create a scope per unit of work instead — one scope per message processed, per timer tick, or per batch.

```csharp
public class OrderWorker(IServiceScopeFactory scopes, ILogger<OrderWorker> log)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            using var scope = scopes.CreateScope();
            var processor = scope.ServiceProvider
                .GetRequiredService<IOrderProcessor>();
            await processor.ProcessPendingAsync(ct);
            await Task.Delay(TimeSpan.FromMinutes(1), ct);
        }
    }
}
```

Each iteration gets a fresh scope, so a DbContext is created and disposed cleanly per batch and never shared across concurrent work.

## Key Takeaways

- Transient: new instance on every resolution; scoped: one per request or explicit scope; singleton: one for the whole app.
- Never let a singleton capture a scoped service — it leaks request state and prevents disposal.
- Enable `ValidateScopes` (and `ValidateOnBuild`) in development to catch captive dependencies at startup.
- In background services, create a scope per unit of work with `IServiceScopeFactory`.
- When a singleton needs scoped data, inject the scope factory and resolve inside a short-lived scope.
- Prefer scoped for request-bound state (DbContext, unit of work) and singleton only for genuinely stateless or thread-safe shared services.
