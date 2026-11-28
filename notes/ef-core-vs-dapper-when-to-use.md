# EF Core vs Dapper: When to Use Each

EF Core and Dapper are the two most common data access choices in .NET.
EF Core is a full object-relational mapper (ORM); Dapper is a thin
micro-ORM that maps SQL query results to objects. Neither is better in
general: they win in different situations.

## Change tracking

EF Core tracks every entity it loads in a `DbContext`. When you change a
property and call `SaveChanges()`, EF Core works out the UPDATE for you:

```csharp
var product = await db.Products.FindAsync(id);
product.Price = 49.99m;
await db.SaveChangesAsync(); // EF Core issues the UPDATE itself.
```

Dapper tracks nothing. You write the SQL, including the UPDATE, and
Dapper only maps rows to objects:

```csharp
await connection.ExecuteAsync(
    "UPDATE Products SET Price = @Price WHERE Id = @Id",
    new { Price = 49.99m, Id = id });
```

Change tracking is convenient, but it has a cost: the `DbContext` holds
references to every loaded entity, which adds memory and CPU overhead.
For read-only queries, add `.AsNoTracking()` to skip it.

## LINQ vs raw SQL

EF Core queries are written in LINQ and translated to SQL by the
provider:

```csharp
var cheap = await db.Products
    .Where(p => p.Price < 50 && p.InStock)
    .OrderBy(p => p.Name)
    .ToListAsync();
```

Dapper queries are SQL strings, written exactly as the database runs
them:

```csharp
var cheap = await connection.QueryAsync<Product>(
    "SELECT * FROM Products WHERE Price < 50 AND InStock = 1 ORDER BY Name");
```

LINQ is type-safe: a renamed column is a compile error, not a runtime
surprise. Raw SQL gives full control over the exact query, including
window functions, hints, and hand-tuned joins that LINQ either cannot
express or translates poorly. The risk with raw SQL is typos and
string-built queries; always use parameters, never concatenate values.

## Performance characteristics

- **Simple CRUD and small result sets:** the difference is small and
  rarely the bottleneck. Pick the tool that fits the team, not the
  benchmark.
- **Large read-heavy queries:** Dapper is typically faster, often by a
  factor of 2 or more in micro-benchmarks, because it skips change
  tracking, LINQ translation, and expression tree overhead. It also
  allocates less memory per row.
- **Writes with relationships:** EF Core is usually faster to write and
  less error-prone, because it handles inserts, updates, foreign keys,
  and transactions across related entities in one `SaveChanges()` call.
- **N+1 queries:** EF Core makes it easy to accidentally load related
  data one row at a time (lazy loading or a loop of queries). Dapper
  forces you to think about the SQL, so this mistake is less common,
  but EF Core avoids it too with `.Include()` or a single projection.

Always measure with your own queries and data before switching tools
for performance. A missing index costs far more than the ORM choice.

## Migrations

EF Core has built-in migrations: change the model classes, run
`dotnet ef migrations add`, and EF Core generates the schema change.
Dapper has no migration support at all. Teams using Dapper usually pair
it with a separate migration tool (such as DbUp or FluentMigrator) or
manage SQL scripts by hand.

## When each wins

Use EF Core when:

- The application does a lot of writes with related entities.
- The team values type safety, LINQ, and automatic migrations.
- Development speed matters more than squeezing out read performance.

Use Dapper when:

- A query is hot (called very often or over large result sets) and the
  SQL needs to be exactly controlled.
- The query is complex: reporting, aggregations, or SQL that LINQ
  translates badly.
- The database schema already exists and the team is comfortable in SQL.

## The hybrid approach

Most real applications use both: EF Core for writes and ordinary
queries, Dapper for the few hot read paths. They can share one
connection string and even one database transaction:

```csharp
// Writes and normal reads through EF Core.
await db.Orders.AddAsync(order);
await db.SaveChangesAsync();

// One hot reporting query through Dapper on the same connection.
var report = await db.Database.GetDbConnection().QueryAsync<SalesRow>(
    "SELECT Category, SUM(Total) AS Total FROM Orders GROUP BY Category");
```

Keep the split explicit: put Dapper queries behind a repository or a
named query class, so it is obvious which data access path each feature
uses.

## Key Takeaways

- EF Core tracks entities and generates SQL from LINQ; Dapper maps raw
  SQL results to objects and tracks nothing.
- EF Core wins for writes, relationships, type safety, and migrations.
  Dapper wins for hot read queries and complex SQL where exact control
  matters.
- For large read-heavy workloads Dapper is typically faster and allocates
  less, but a missing index costs more than the ORM choice, so measure
  first.
- The pragmatic default is hybrid: EF Core for writes and everyday
  queries, Dapper for the few hot read paths, sharing one connection.
