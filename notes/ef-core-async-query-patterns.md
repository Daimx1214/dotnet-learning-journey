# Async EF Core Query Patterns

Entity Framework Core is fully async-capable, and on ASP.NET Core using the async methods is not a style choice — it is how you keep the thread pool healthy under load.

## The Async Query Methods

Every common query operation has an async twin:

```csharp
var users = await db.Users
    .Where(u => u.IsActive)
    .OrderBy(u => u.Name)
    .ToListAsync(ct);

var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

var count = await db.Users.CountAsync(u => u.IsActive, ct);
var exists = await db.Users.AnyAsync(u => u.Email == email, ct);

await db.SaveChangesAsync(ct);
```

Pass a `CancellationToken` (usually from the controller action's parameter or `HttpContext.RequestAborted`) so a client disconnect cancels the in-flight database work instead of letting it run to completion for nobody.

## Why Async Matters: Thread-Pool Starvation

ASP.NET Core handles each request on a thread-pool thread. A synchronous database call blocks that thread for the whole round trip — while it waits on the network, the thread cannot serve any other request. Under load, all pool threads end up blocked waiting on the database, new requests queue, and latency explodes even though the CPU is mostly idle. This is thread-pool starvation.

An `await` releases the thread back to the pool while the query runs, so the same small pool can serve many concurrent requests. The database work itself takes the same time — async does not make a query faster — but it stops one slow query from starving every other request.

## Never Use .Result or .Wait()

Blocking on an async method defeats the whole point and can deadlock:

```csharp
// DO NOT DO THIS — blocks the thread and can deadlock in ASP.NET.
var users = db.Users.ToListAsync().Result;
db.SaveChangesAsync().Wait();
```

`.Result` and `.Wait()` block the calling thread until the task completes. In classic ASP.NET this could deadlock outright; in ASP.NET Core it usually "only" causes thread-pool starvation — the exact problem async exists to prevent. If a method needs data from the database, make the method `async` all the way up: async controller → async service → async EF Core call.

## Key Takeaways

- Use `ToListAsync`, `FirstOrDefaultAsync`, `CountAsync`, `AnyAsync`, and `SaveChangesAsync` instead of their sync versions in ASP.NET Core.
- Always pass a `CancellationToken` so cancelled requests stop their database work.
- Async does not make a single query faster — it frees the thread while the query runs, preventing thread-pool starvation under load.
- Never call `.Result` or `.Wait()` on EF Core tasks; go `async` all the way up the call stack.
- Keep a single `DbContext` per request and `await` each operation; `DbContext` is not thread-safe, so do not run concurrent operations on the same instance.
