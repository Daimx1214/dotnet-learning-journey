# EF Core Compiled Queries (EF Core)

Every LINQ query EF Core executes goes through a pipeline: the expression tree is translated to SQL, the SQL is cached, and then it runs. The translation step is repeated whenever the cache misses, and for a query executed thousands of times per second that translation cost adds up. Compiled queries let you pre-compile a LINQ query into a delegate once, so every call skips the translation step and goes straight to execution. The win is real but narrow: it helps only when the same query runs very frequently with different parameter values.

## EF.CompileQuery and EF.CompileAsyncQuery

A compiled query is a static delegate created once — typically as a `static readonly` field — and invoked with a `DbContext` plus its parameters. The query itself is written exactly like a normal LINQ query, but it must be a lambda whose first parameter is the context.

```csharp
using Microsoft.EntityFrameworkCore;

public static class ProductQueries
{
    // Compiled once; every call reuses the translated SQL.
    public static readonly Func<AppDbContext, string, IQueryable<Product>> ByCategory =
        EF.CompileQuery((AppDbContext db, string category) =>
            db.Products
              .Where(p => p.Category == category && p.IsActive)
              .OrderBy(p => p.Name));

    public static readonly Func<AppDbContext, int, Task<Product?>> ByIdAsync =
        EF.CompileAsyncQuery((AppDbContext db, int id) =>
            db.Products.FirstOrDefaultAsync(p => p.Id == id));
}

// Usage in a service or endpoint:
List<Product> books = ProductQueries.ByCategory(db, "Books").ToList();
Product? product = await ProductQueries.ByIdAsync(db, 42);
```

Note that `EF.CompileQuery` returns a delegate producing `IQueryable<T>` — calling `.ToList()` on the result is what actually executes it, and further LINQ operators can still be chained after the call. `EF.CompileAsyncQuery` returns a `Func<..., Task<T>>` for single-result async queries.

## When Compiled Queries Help

Compiled queries pay off only when a query is executed many times with the same shape but different parameter values — hot read paths such as "product by id", "orders by customer", or a lookup inside a tight loop. The translation they skip is usually measured in tens of microseconds, so a query run once per request on a normal web endpoint will not show a difference; a query run 10,000 times in a batch import will. Measure with a profiler before and after — do not compile queries speculatively across the whole codebase.

## Parameters and Limitations

- Parameters must be passed as lambda arguments (`category`, `id` above). Closing over a local variable or a captured field defeats the caching, because the expression tree then embeds a constant.
- The query is compiled against a specific `DbContext` type; a derived context with a different model cannot reuse it.
- Compiled queries do not support every LINQ feature: queries using `Include` chains with complex filters, `FromSql`, or dynamic `Expression` composition may not compile, and EF Core will throw at delegate-creation time rather than silently falling back.
- The delegate is thread-safe and can be shared across requests, but it is bound to the model snapshot of the context type at compile time — changing the model (e.g. a new migration applied at runtime) requires an app restart.
- In EF Core 8 and later, most of the benefit is already captured automatically: the query cache is efficient enough that explicit compilation is rarely needed outside genuinely hot paths.

## Compiled Queries vs the Built-in Query Cache

EF Core already caches the translated SQL for every LINQ query, keyed by the expression tree. A compiled query goes one step further: it caches the *shaper* — the code that turns database rows into entity objects — as a compiled delegate. That is why the benefit is small: the SQL cache already removes most of the repeated work, and the shaper compilation is only a fraction of total query time. In EF Core 8 the query pipeline was reworked so compiled queries rarely beat the cache at all, which is why profiling before adopting them matters more than the API itself.

## A Realistic Hot-Path Example

A lookup inside a loop is the textbook case where compilation earns its keep:

```csharp
// Seeding 10,000 order lines, each needing its product's price:
foreach (var line in orderLines)
{
    // Without compilation: translation + shaper build on every call.
    // With compilation: both are paid once, up front.
    Product? product = await ProductQueries.ByIdAsync(db, line.ProductId);
    line.UnitPrice = product?.Price ?? 0m;
}
```

Even here, the bigger win is usually batching the lookups into one `Where(p => ids.Contains(p.Id))` query — one round trip beats 10,000 compiled ones. Compiled queries are a fine-tuning tool, not a substitute for fixing N+1 patterns.

## Key Takeaways

- `EF.CompileQuery` / `EF.CompileAsyncQuery` turn a LINQ query into a reusable delegate, skipping expression-tree translation on every call.
- Define the delegate once as a `static readonly` field and pass all varying values as lambda parameters — never close over locals.
- The win matters only for hot, frequently executed queries; profile first and compile selectively.
- Some LINQ features are not supported in compiled queries, and the delegate is tied to one `DbContext` type and model snapshot.
- On EF Core 8+, the built-in query cache already covers most cases, so reach for compiled queries only where measurements justify it.
