# HybridCache in ASP.NET Core

HybridCache is the caching abstraction introduced in .NET 9 that combines a fast local in-memory cache (L1) with a shared distributed cache (L2), such as Redis, behind one simple API. Instead of choosing between `IMemoryCache` and `IDistributedCache` and wiring them together yourself, you get a single `HybridCache` service that reads from memory first, falls back to the distributed cache, and populates both on a miss.

It matters because most real applications run on more than one server. A memory-only cache is fast but per-process, so each server re-fetches the same data and users can see stale or inconsistent values. A distributed-only cache is shared but pays a network round trip on every read. HybridCache gives you both: sub-millisecond reads from local memory for hot data, plus a shared source of truth so every instance sees the same values.

## Getting Started

Register HybridCache in `Program.cs`. The default setup uses in-memory L1 only; add a distributed cache (Redis is the common choice) to enable L2.

```csharp
var builder = WebApplication.CreateBuilder(args);

// L2: Redis as the shared distributed cache.
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
});

// HybridCache sits on top of both levels.
builder.Services.AddHybridCache(options =>
{
    options.DefaultEntryOptions = new HybridCacheEntryOptions
    {
        Expiration = TimeSpan.FromMinutes(30),
        LocalCacheExpiration = TimeSpan.FromMinutes(5)
    };
});

var app = builder.Build();
```

If you skip `AddStackExchangeRedisCache`, HybridCache still works — it just runs with the local cache only. That makes local development easy and lets you add Redis later without changing application code.

## GetOrCreateAsync

The main API is `GetOrCreateAsync`. You give it a key and a factory delegate that produces the value on a cache miss. HybridCache handles the rest: check L1, check L2, call the factory, then store the result in both levels.

```csharp
public class ProductService(HybridCache cache, AppDbContext db)
{
    public async Task<Product?> GetProductAsync(int id, CancellationToken ct = default)
    {
        return await cache.GetOrCreateAsync(
            $"product:{id}",
            async token => await db.Products.FindAsync([id], token),
            new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(30),
                LocalCacheExpiration = TimeSpan.FromMinutes(5)
            },
            cancellationToken: ct);
    }
}
```

The factory receives a `CancellationToken` of its own — use that token, not the outer one, for the database call. Notice the key is a plain string: keep keys short, descriptive, and namespaced (`product:42`, `user:7:profile`) so they never collide across features.

## Two-Level Reads and Writes

A read follows a fixed order:

1. **L1 (local memory):** fastest, per-process. A hit returns immediately.
2. **L2 (distributed cache):** a hit returns the value and backfills L1, so the next read is local.
3. **Factory:** a miss in both levels calls your factory, then writes the result to L1 and L2.

Expiration works at two levels too. `Expiration` is the overall lifetime of the entry in L2. `LocalCacheExpiration` is how long L1 keeps its copy — deliberately shorter, so each server re-validates against the shared cache regularly and stale local copies do not linger. A common split is 30 minutes overall and 5 minutes local.

## Stampede Protection

A cache stampede happens when a popular entry expires and dozens of concurrent requests all miss at once, hammering the database with identical queries. HybridCache prevents this: when multiple callers ask for the same key at the same time, only one factory invocation runs; the others wait and share the result.

```csharp
// 100 concurrent requests for the same key trigger ONE database call.
var tasks = Enumerable.Range(0, 100)
    .Select(_ => cache.GetOrCreateAsync("homepage:stats", GetStatsAsync));
var results = await Task.WhenAll(tasks);
```

This is built in — there is no lock or semaphore to write yourself. It is one of the strongest reasons to prefer HybridCache over hand-rolled caching, where stampede protection is easy to get wrong.

## Invalidation

Cached data goes stale when the underlying data changes. HybridCache offers two tools:

- `RemoveAsync(key)` — delete one entry from both levels.
- `RemoveByTagAsync(tag)` — delete every entry tagged with a value, so one write can invalidate a whole group.

```csharp
public async Task UpdateProductAsync(Product product, CancellationToken ct = default)
{
    db.Products.Update(product);
    await db.SaveChangesAsync(ct);

    // Invalidate the single entry and the whole product list.
    await cache.RemoveAsync($"product:{product.Id}", ct);
    await cache.RemoveByTagAsync("products", ct);
}
```

Tagging happens at write time, via `HybridCacheEntryOptions.Tags`:

```csharp
await cache.GetOrCreateAsync(
    "products:all",
    GetAllProductsAsync,
    new HybridCacheEntryOptions { Tags = ["products"] },
    cancellationToken: ct);
```

Prefer tag-based invalidation for lists and grouped data. Removing a single key is fine for one entity, but a list cache keyed `"products:all"` would otherwise stay stale until it expires.

## HybridCache vs IDistributedCache

| Concern | HybridCache | IDistributedCache |
|---|---|---|
| Local memory layer | Built in (L1) | None — every read is a network call |
| Stampede protection | Built in | You write it yourself |
| Tag invalidation | Built in | Not supported |
| API style | `GetOrCreateAsync` with a factory | Manual `GetAsync` / `SetAsync` with byte arrays |
| Serialization | Handles it for you | You serialize to `byte[]` yourself |
| Control | Less fine-grained | Full control over raw bytes |

Choose `IDistributedCache` when you need exact control over serialization or you are working with a legacy cache layout. Choose HybridCache for almost everything else — it is the recommended default for new ASP.NET Core apps on .NET 9 and later.

## When Not to Use It

- **Per-request data:** values that change on every request (the current user, a request id) should never be cached.
- **Huge objects:** caching a multi-megabyte payload in memory on every server wastes RAM; cache the query result or a summary instead.
- **Strong consistency needs:** cache is eventually consistent by nature. If a read must reflect a write that happened a millisecond ago, read the database directly.
- **Secrets:** never cache tokens, passwords, or per-user secrets under a shared key.

## Key Takeaways

- HybridCache is a two-level cache (local memory L1 + distributed L2, e.g. Redis) behind one API, introduced in .NET 9.
- `GetOrCreateAsync` handles the read-through flow: L1 hit, L2 hit with L1 backfill, or factory call that populates both levels.
- `LocalCacheExpiration` should be shorter than overall `Expiration` so servers re-validate against the shared cache.
- Stampede protection is built in: concurrent callers for the same key share a single factory invocation.
- Invalidate with `RemoveAsync` for single entries and `RemoveByTagAsync` with `Tags` for groups such as lists.
- Prefer HybridCache over raw `IDistributedCache` for new apps; keep raw distributed access only when you need byte-level control.
