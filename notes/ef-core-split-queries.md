# EF Core Split Queries

When an EF Core query loads an entity together with several related collections, the default single SQL query can return far more rows than you expect. Split queries trade extra round trips for smaller, cheaper result sets.

## The Cartesian Explosion Problem

A single query that includes two collections multiplies the rows: one order with 10 items and 5 shipments returns 50 rows, and EF Core has to de-duplicate them in memory.

```csharp
// Default: ONE query, JOINs both collections — rows multiply.
var orders = await db.Orders
    .Include(o => o.Items)
    .Include(o => o.Shipments)
    .ToListAsync();
```

With one collection the duplication is usually tolerable. With two or more collections, the row count grows multiplicatively and the query can become slower than issuing separate queries.

## Using AsSplitQuery()

`AsSplitQuery()` tells EF Core to run one query per collection and stitch the results together in memory.

```csharp
var orders = await db.Orders
    .Include(o => o.Items)
    .Include(o => o.Shipments)
    .AsSplitQuery()
    .ToListAsync();

// Equivalent global setting in OnConfiguring / AddDbContext:
// options.UseSqlServer(conn).UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
```

The generated SQL is three queries: one for `Orders`, one for `OrderItems` (filtered by the order ids), and one for `Shipments`. Each returns only its own rows, so no duplication happens on the wire.

## The Trade-Off

Split queries are not free. Each extra query is an extra round trip to the database, and the separate queries are not executed in a single snapshot unless you wrap them in a transaction — data could change between them on a busy database.

Use split queries when:

- You include **two or more collections** in one query.
- The collections are large, so the multiplied row count dwarfs the real data.
- You have measured the single query being slow (check the row count, not just the query count).

Stick with the default single query when:

- You include only one collection, or only reference (single-entity) navigations.
- The database is remote and round-trip latency is high — extra trips may cost more than the duplication.
- You need a perfectly consistent snapshot and cannot wrap the query in a transaction.

## Key Takeaways

- A single query with two collection `Include`s multiplies rows (cartesian explosion); EF Core de-duplicates them in memory at a cost.
- `AsSplitQuery()` runs one query per collection and avoids the duplication, at the price of extra round trips.
- Split queries are best with multiple large collections; single queries are fine for one collection or reference navigations.
- Separate queries are not snapshot-consistent by default — use an explicit transaction if that matters.
- Measure before choosing: compare row counts and timings, not just the number of queries.
