# EF Core DbContext Pooling

`DbContext` is designed to be short-lived: create it, run a unit of work,
dispose it. Creating one is cheap, but not free. The context builds its
internal service provider, model metadata, and change tracker on every
construction. DbContext pooling reuses context instances across requests so
that setup cost is paid once instead of on every request.

## Enabling Pooling

Register the context with `AddDbContextPool` instead of `AddDbContext`:

```csharp
builder.Services.AddDbContextPool<AppDbContext>(options =>
    options.UseSqlServer(connectionString));
```

The default pool keeps up to 128 instances. You can change that with the
`poolSize` parameter:

```csharp
builder.Services.AddDbContextPool<AppDbContext>(
    options => options.UseSqlServer(connectionString),
    poolSize: 256);
```

Each pooled instance is still scoped to one request (or one unit of work).
Pooling changes reuse, not lifetime: the instance is returned to the pool
when the scope ends and handed out again for the next request.

## How Pooling Reuses Instances

When a pooled context is returned, EF Core calls `ResetState` on it: the
change tracker is cleared, tracked entities are detached, and the context is
left in a clean state. The expensive parts are kept: the compiled model,
the internal service provider, and the query cache survive the reset, so the
next request skips the setup work.

The saving is typically a few hundred microseconds per request. That sounds
small, but on a high-throughput API it adds up to a meaningful reduction in
CPU and allocations, especially under load where context creation happens
thousands of times per second.

## OnConfiguring vs Constructor Injection

A pooled context must get its options through constructor injection, because
the pool creates instances from the registered `DbContextOptions<T>`:

```csharp
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Order> Orders => Set<Order>();
}
```

Avoid overriding `OnConfiguring` to set the provider in a pooled context. If
you do, the options passed to the constructor can conflict with the ones set
in `OnConfiguring`, and the pool may hand out an instance configured
differently from what you expect. Keep all configuration in the
`AddDbContextPool` registration.

## What Must Not Be Kept in a Pooled Context

Pooling only works if the context carries no per-instance state between
uses. Never store request-specific data on the context itself:

- Do not add fields or properties that hold a user id, tenant id, or
  request id on the `DbContext` subclass.
- Do not keep a reference to a scoped service inside the context beyond the
  options it was constructed with.
- Do not rely on a tracked entity surviving from one request to the next;
  the reset detaches everything.

If you need per-request data, pass it as a method argument to your queries,
use a separate scoped service, or use an interceptor that reads from the
current scope. A pooled context that leaks state between requests produces
bugs that only appear under load, which makes them hard to reproduce.

## When NOT to Pool

Pooling is not always a win. Do not pool when:

- The context is configured differently per tenant or per request, because
  the pool hands out instances built from one shared options object.
- You use `DbContext` in a long-lived or singleton scenario, which is already
  unsupported; pooling does not fix that.
- Your app is a low-traffic internal tool where the saving is negligible and
  the extra constraint is not worth it.
- You rely on `OnConfiguring` or on per-instance initialization in the
  constructor that must run fresh for every unit of work.

For most ASP.NET Core APIs with a single database and a standard registration,
pooling is a safe default.

## Measuring the Benefit

Do not guess whether pooling helps. Measure allocations and request latency
with and without it under realistic load. The gain is largest when requests
are short and numerous, because context setup is a larger share of the total
work. On long-running requests dominated by a slow query, the saving is
invisible. Benchmark with a tool like BenchmarkDotNet or a load test before
and after the switch.

## Key Takeaways

- `AddDbContextPool` reuses context instances across requests, keeping the compiled model and service provider while resetting tracked state.
- The default pool size is 128; raise it only if you see pool exhaustion under load.
- Configure pooled contexts through constructor-injected `DbContextOptions<T>`, not `OnConfiguring`.
- Never store per-request or per-tenant state on the context itself; pooled instances are shared between requests.
- Skip pooling when the context is configured per tenant, or when the saving is too small to justify the constraint.
