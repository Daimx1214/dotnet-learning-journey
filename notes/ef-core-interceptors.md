# EF Core Interceptors (EF Core)

EF Core interceptors let you hook into the pipeline at defined points without changing your queries or entities. They are the supported way to add cross-cutting behaviour such as logging slow queries, auditing who changed what, or retrying transient failures. An interceptor implements one of the `I*Interceptor` interfaces and is registered on the `DbContextOptionsBuilder` with `AddInterceptors`.

## The Main Interceptor Interfaces

| Interface | Hooks into |
|-----------|------------|
| `IDbCommandInterceptor` | SQL commands: before/after execution, on failure |
| `ISaveChangesInterceptor` | `SaveChanges` / `SaveChangesAsync`: before/after, on failure |
| `IDbConnectionInterceptor` | Opening/closing connections |
| `IDbTransactionInterceptor` | Beginning/committing/rolling back transactions |
| `IMaterializationInterceptor` | Creating entity instances from query results |

Each method comes in sync and async pairs. The command interceptor's `ReaderExecuted` / `NonQueryExecuted` / `ScalarExecuted` methods receive a `CommandExecutedEventData` with the `DbCommand`, the `DbContext`, and the elapsed `Duration` — exactly what slow-query logging needs. Inheriting from `DbCommandInterceptor` instead of implementing the interface directly gives you no-op defaults, so you only override what you need.

## Logging Slow Commands

Returning the `InterceptionResult` from the `*Executing` methods lets you suppress or replace execution entirely, which is how query hints or command rewriting are implemented. The real timing check belongs in the `*Executed` methods, because `Duration` is not yet meaningful before execution.

```csharp
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

public class SlowQueryInterceptor : DbCommandInterceptor
{
    private readonly TimeSpan _threshold = TimeSpan.FromMilliseconds(500);

    public override DbDataReader ReaderExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result)
    {
        if (eventData.Duration > _threshold)
        {
            Console.WriteLine(
                $"[SLOW] {eventData.Duration.TotalMilliseconds:F0} ms — {command.CommandText}");
        }
        return base.ReaderExecuted(command, eventData, result);
    }
}
```

## Auditing with a SaveChanges Interceptor

A `ISaveChangesInterceptor` can stamp audit fields on every tracked entity before it is written. This is cleaner than overriding `SaveChanges` in the context because it composes: several interceptors can be registered and each handles one concern.

```csharp
using Microsoft.EntityFrameworkCore.Diagnostics;

public class AuditInterceptor : SaveChangesInterceptor
{
    private readonly string _currentUser;
    public AuditInterceptor(string currentUser) => _currentUser = currentUser;

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        DbContext? context = eventData.Context;
        if (context is not null)
        {
            foreach (var entry in context.ChangeTracker.Entries<IAuditable>())
            {
                if (entry.State == EntityState.Added)
                {
                    entry.Entity.CreatedBy = _currentUser;
                    entry.Entity.CreatedAt = DateTimeOffset.UtcNow;
                }
                if (entry.State is EntityState.Added or EntityState.Modified)
                {
                    entry.Entity.ModifiedBy = _currentUser;
                    entry.Entity.ModifiedAt = DateTimeOffset.UtcNow;
                }
            }
        }
        return base.SavingChanges(eventData, result);
    }
}

public interface IAuditable
{
    string CreatedBy { get; set; }
    DateTimeOffset CreatedAt { get; set; }
    string ModifiedBy { get; set; }
    DateTimeOffset ModifiedAt { get; set; }
}
```

## Registering Interceptors

Register interceptors on the options builder in `Program.cs`. They are resolved from DI when registered as services, which is how the audit interceptor gets the current user. Interceptors are singleton-scoped for the context's lifetime, so they must be thread-safe and must not hold per-request state in fields unless created per scope. Order matters: interceptors run in registration order for the "before" methods and in reverse order for the "after" methods, like middleware.

```csharp
builder.Services.AddSingleton<SlowQueryInterceptor>();
builder.Services.AddScoped<AuditInterceptor>(sp =>
    new AuditInterceptor(sp.GetRequiredService<IHttpContextAccessor>()
        .HttpContext?.User.Identity?.Name ?? "system"));

builder.Services.AddDbContext<AppDbContext>((sp, options) =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default"))
           .AddInterceptors(
               sp.GetRequiredService<SlowQueryInterceptor>(),
               sp.GetRequiredService<AuditInterceptor>()));
```

## Pitfalls

- Interceptors observe the pipeline; they are **not** a substitute for transactions or concurrency tokens.
- Avoid doing database work inside an interceptor: a `SaveChanges` interceptor that calls `SaveChanges` again will recurse.
- Heavy work in `ReaderExecuted` runs on the hot path of every query, so keep logging cheap.
- `DbCommandInterceptor` only sees commands EF Core sends; raw ADO.NET code that bypasses the context is invisible to it.

## Key Takeaways

- Interceptors hook the EF Core pipeline at command, SaveChanges, connection, transaction, and materialization points.
- `DbCommandInterceptor` + `CommandExecutedEventData.Duration` is the clean way to log slow queries.
- `SaveChangesInterceptor` is the right place to stamp audit fields on tracked entities.
- Register with `AddInterceptors`, resolve dependencies from DI, and keep interceptors thread-safe and cheap.
