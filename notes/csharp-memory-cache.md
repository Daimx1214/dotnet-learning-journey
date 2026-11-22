# IMemoryCache in C#

`IMemoryCache` is ASP.NET Core's built-in in-process cache. It stores
objects in the memory of the running process — fast, simple, and ideal
for reference data, computed results, and anything expensive to rebuild
that fits in RAM.

## Setup and Basic Use

Register it with `AddMemoryCache()` and inject `IMemoryCache` where you
need it. `GetOrCreate` (or the async `GetOrCreateAsync`) is the main
entry point: it returns the cached value or runs the factory to build it.

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddMemoryCache();

public class ProductService
{
    private readonly IMemoryCache _cache;

    public ProductService(IMemoryCache cache) => _cache = cache;

    public Product GetProduct(int id)
    {
        return _cache.GetOrCreate($"product:{id}", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            return LoadProductFromDatabase(id);
        })!;
    }
}
```

## Sliding vs Absolute Expiration

`MemoryCacheEntryOptions` controls how long an entry lives:

```csharp
var options = new MemoryCacheEntryOptions
{
    // Removed 10 minutes after the last access.
    SlidingExpiration = TimeSpan.FromMinutes(10),

    // Removed at most 1 hour after creation, no matter how often used.
    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1),

    // Highest priority: evicted last when memory pressure hits.
    Priority = CacheItemPriority.High
};

_cache.Set("key", value, options);
```

Use sliding expiration for data that stays hot while it is used, and
absolute expiration as a ceiling so stale data can never live forever.
Combining both is common: slide for 10 minutes, but never older than
an hour.

## Eviction Callbacks and Size Limits

Register a callback to know when an entry disappears, and set a size
limit so the cache cannot grow without bound:

```csharp
builder.Services.AddMemoryCache(options =>
{
    options.SizeLimit = 1024; // total size budget in abstract units
});

var entryOptions = new MemoryCacheEntryOptions
{
    Size = 1, // every entry must declare a size when SizeLimit is set
    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
};
entryOptions.RegisterPostEvictionCallback(
    (key, value, reason, state) =>
        Console.WriteLine($"Evicted {key}: {reason}"));

_cache.Set("report", report, entryOptions);
```

Eviction reasons include `Expired`, `Capacity` (size limit hit),
`Removed` (explicit `Remove` call), and `Replaced` (same key set again).
Note that entries are only evicted when the cache is accessed or a
background scan runs — an expired entry can linger until then.

## Thread Safety

`IMemoryCache` is thread-safe: concurrent `GetOrCreate` calls for the
same key are safe. But the factory delegate can run more than once under
heavy contention — two threads can both miss and both build the value.
If the factory is expensive or has side effects, guard it with a lock or
a `Lazy<T>` / `SemaphoreSlim` per key.

```csharp
private static readonly SemaphoreSlim _gate = new(1, 1);

public async Task<Product> GetProductAsync(int id)
{
    if (_cache.TryGetValue($"product:{id}", out Product? cached))
        return cached!;

    await _gate.WaitAsync();
    try
    {
        if (_cache.TryGetValue($"product:{id}", out cached))
            return cached!;
        var product = await LoadProductFromDatabaseAsync(id);
        _cache.Set($"product:{id}", product,
            TimeSpan.FromMinutes(10));
        return product;
    }
    finally { _gate.Release(); }
}
```

## When to Prefer IDistributedCache

`IMemoryCache` lives in one process. That is fine for a single server,
but it breaks down when you scale out:

| | IMemoryCache | IDistributedCache |
| --- | --- | --- |
| Scope | One process | Shared across servers |
| Survives restart | No | Yes (Redis / SQL Server) |
| Consistency | Per-instance | Same value everywhere |
| Speed | Fastest | Network round-trip |

Pick `IDistributedCache` (backed by Redis or SQL Server) when you run
multiple instances, need cache to survive restarts, or share cached data
between services. A common pattern is both: `IMemoryCache` as a fast
local layer in front of a distributed cache.

## Key Takeaways

- `IMemoryCache` is the built-in in-process cache; `GetOrCreate` is the
  usual way to read or populate it.
- Sliding expiration resets on each access; absolute expiration caps the
  total lifetime — combine them.
- Set `SizeLimit` and a `Size` per entry to bound memory, and use
  post-eviction callbacks to observe why entries disappear.
- The cache is thread-safe, but the factory can run twice under
  contention — guard expensive factories if that matters.
- Scale-out, restart survival, or cross-service sharing means
  `IDistributedCache`, not `IMemoryCache`.
