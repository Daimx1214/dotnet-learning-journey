# Multi-Tenancy Basics in ASP.NET Core

Multi-tenancy means one application instance serves many tenants (customers, organizations), each with isolated data. The three data-isolation strategies are database per tenant, shared database with a shared schema, and shared database with a schema per tenant. The choice is a trade-off between isolation, cost, and operational complexity, and it should be made before the first line of data-access code is written.

## The Three Strategies

**Database per tenant** gives the strongest isolation: each tenant gets its own database, so a bug can never leak another tenant's rows, backups and restores are per tenant, and a large tenant can be moved to its own server. The cost is operational: 1,000 tenants means 1,000 databases to migrate, monitor, and back up, and connection pooling becomes a problem (a pool per database multiplies connections).

**Shared database, shared schema** is the cheapest and most common for SaaS with many small tenants: one database, every table has a `TenantId` column, and every query filters on it. It scales to tens of thousands of tenants with one migration set, but isolation depends entirely on the application never forgetting the filter — one missed `Where` leaks data.

**Schema per tenant** sits in the middle: one database, one schema per tenant (`tenant_a.Orders`, `tenant_b.Orders`). Isolation is enforced by the database, migrations run once per schema, and cross-tenant queries are impossible by accident. It works well up to a few hundred tenants; beyond that, schema management and connection-string switching get painful.

| Strategy | Isolation | Ops cost | Tenant count |
|---|---|---|---|
| Database per tenant | Strongest | Highest | Up to ~100s |
| Shared schema | App-enforced | Lowest | 1,000s+ |
| Schema per tenant | DB-enforced | Medium | Up to ~100s |

## Tenant Resolution

Before any query runs, the app must know which tenant the request belongs to. Common resolvers: subdomain (`acme.app.com`), a header (`X-Tenant-Id`), a claim in the JWT, or the first path segment (`/acme/orders`). Resolution happens in middleware, early in the pipeline, and the result is stored in a scoped `ITenantContext` service.

```csharp
// Middleware resolves the tenant from the subdomain and stores it scoped.
public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ITenantContext tenants)
    {
        string host = context.Request.Host.Host; // acme.app.com
        string slug = host.Split('.')[0];
        Tenant? tenant = await tenants.ResolveAsync(slug);
        if (tenant is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsync($"Unknown tenant: {slug}");
            return;
        }
        tenants.Current = tenant;
        await next(context);
    }
}

public interface ITenantContext
{
    Tenant? Current { get; set; }
    Task<Tenant?> ResolveAsync(string slug);
}

public sealed record Tenant(Guid Id, string Slug);
```

## EF Core Query Filters

For the shared-schema strategy, EF Core global query filters enforce tenant isolation at the query level so no query can forget the filter.

```csharp
public sealed class AppDbContext(
    DbContextOptions<AppDbContext> options,
    ITenantContext tenants) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Every Order query automatically filters to the current tenant.
        modelBuilder.Entity<Order>()
            .HasQueryFilter(o => o.TenantId == tenants.Current!.Id);
    }

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        // Stamp the tenant on every new entity so inserts cannot forget it.
        foreach (var entry in ChangeTracker.Entries<ITenantOwned>()
                     .Where(e => e.State == EntityState.Added))
        {
            entry.Entity.TenantId = tenants.Current!.Id;
        }
        return base.SaveChangesAsync(ct);
    }
}

public interface ITenantOwned { Guid TenantId { get; set; } }

public sealed class Order : ITenantOwned
{
    public int Id { get; set; }
    public Guid TenantId { get; set; }
    public string Customer { get; set; } = "";
    public decimal Total { get; set; }
}
```

Query filters apply to `Find`, LINQ queries, and `Include` — but not to raw SQL (`FromSqlRaw`) or to `IgnoreQueryFilters()` calls, which should be restricted to admin code paths and code-reviewed every time they appear.

## Per-Tenant Connection Strings

For database-per-tenant, the `DbContext` gets its connection string from the resolved tenant instead of configuration. Override `OnConfiguring` or register the context with a factory that reads `ITenantContext`:

```csharp
builder.Services.AddDbContext<AppDbContext>((sp, options) =>
{
    var tenants = sp.GetRequiredService<ITenantContext>();
    string connectionString = tenants.Current!.ConnectionString;
    options.UseSqlServer(connectionString);
});
```

## Common Pitfalls

- A query filter that reads `tenants.Current` when it is null throws at runtime — resolve the tenant before the first query, always.
- Caching shared data without a tenant key: a cache key like `orders:recent` shared across tenants leaks data; always include the tenant id (`orders:{tenantId}:recent`).
- Background jobs (Hangfire, hosted services) run outside a request, so there is no tenant in scope — pass the tenant id explicitly into every job.
- `IgnoreQueryFilters()` in generic repository code defeats the entire isolation model — grep for it in code review.

## Key Takeaways

- Three strategies: database per tenant (strongest isolation, highest ops cost), shared schema (cheapest, app-enforced), schema per tenant (middle ground).
- Resolve the tenant early in middleware (subdomain, header, or JWT claim) and fail fast with 404 on unknown tenants — never fall back to a default.
- EF Core global query filters plus tenant stamping in `SaveChangesAsync` make isolation automatic instead of relying on every query remembering the filter.
- Cache keys and background jobs must carry the tenant id explicitly; both are classic leak vectors.
