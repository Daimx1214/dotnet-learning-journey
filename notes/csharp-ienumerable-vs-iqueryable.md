# IEnumerable vs IQueryable

`IEnumerable<T>` and `IQueryable<T>` look similar — both let you write LINQ queries — but they decide *where* your query runs. `IEnumerable<T>` runs in memory, in your process. `IQueryable<T>` builds an expression tree that a provider (usually Entity Framework Core) translates into something else, most often SQL, and runs on the database server. Mixing them up is one of the most common performance mistakes in .NET applications.

The rule of thumb is simple: filter with `IQueryable<T>` while the data is still in the database, and switch to `IEnumerable<T>` only when you deliberately want the remaining work done in memory.

## Deferred Execution

Both interfaces are lazily evaluated. Writing a query does not run it — the query runs when you enumerate it (`foreach`, `ToList()`, `Count()`, `First()`, and so on).

```csharp
// Nothing has hit the database yet. This only builds the query.
var query = db.Products.Where(p => p.Price > 100);

// The SQL runs HERE, when the results are actually needed.
var expensive = await query.ToListAsync();
```

Deferred execution is powerful because it lets you compose a query in stages — add a `Where` here, an `OrderBy` there — and only one SQL statement is generated at the end. But it also means a query variable is not a result: if you never enumerate it, the work never happens, and if you enumerate it twice, the work happens twice.

## Where Filtering Happens

This is the core difference. With `IQueryable<T>`, the `Where` becomes part of the SQL, so the database does the filtering and only matching rows travel over the network. With `IEnumerable<T>`, the filter runs in memory after all rows have already been loaded.

```csharp
// IQueryable: SQL is SELECT ... WHERE Price > 100. Only matching rows return.
var fromDb = db.Products.Where(p => p.Price > 100);

// IEnumerable: AsEnumerable() pulls ALL products into memory first,
// and the Where runs in C# afterwards. Slow and wasteful on big tables.
var inMemory = db.Products.AsEnumerable().Where(p => p.Price > 100);
```

The same trap appears when a method returns `IEnumerable<T>` from a repository. Once the query has been converted to `IEnumerable<T>`, every later `Where`, `OrderBy`, or `Take` runs in memory — the database has already done its part.

```csharp
// Bad: the repository materialises everything, then the caller filters in memory.
public IEnumerable<Product> GetAll() => db.Products.ToList();

// Good: return IQueryable so the caller can keep filtering in SQL.
public IQueryable<Product> Query() => db.Products.AsQueryable();
```

## What IQueryable Can and Cannot Translate

An `IQueryable<T>` provider must translate your LINQ expression into SQL. Most common operations translate fine (`Where`, `OrderBy`, `Select`, `Take`, `Skip`, `Count`, `Any`, `GroupBy`). Arbitrary C# does not.

```csharp
// Works: simple property comparison translates to SQL.
var cheap = db.Products.Where(p => p.Price < 50);

// Fails at runtime: EF Core cannot translate a custom method into SQL.
bool IsBargain(Product p) => p.Price < 50 && p.Stock > 0;
var bargains = db.Products.Where(p => IsBargain(p)); // throws InvalidOperationException
```

When translation fails you get a runtime exception (EF Core 3+ no longer silently falls back to client evaluation). The fix is to either express the logic with translatable operations, or deliberately switch to `IEnumerable<T>` with `AsEnumerable()` after you have narrowed the data down in SQL first.

```csharp
// Narrow in SQL, then apply the custom logic in memory on a small set.
var bargains = db.Products
    .Where(p => p.Price < 50)
    .AsEnumerable()
    .Where(p => IsBargain(p))
    .ToList();
```

## Common Pitfalls

### Multiple Enumeration

Enumerating the same `IEnumerable<T>` twice re-runs the whole pipeline — two database queries for an `IQueryable<T>`, or two full passes for a LINQ-to-Objects chain.

```csharp
var query = db.Products.Where(p => p.InStock);

var count = await query.CountAsync();      // Query 1
var items = await query.ToListAsync();     // Query 2 — the database is hit again

// Fix: materialise once, then reuse the list.
var products = await query.ToListAsync();
var count = products.Count;
```

### ToList Timing

Calling `ToList()` too early ends the SQL part of the query. Everything after it runs in memory.

```csharp
// Bad: loads every product, then filters in memory.
var result = db.Products.ToList().Where(p => p.Price > 100).Take(10);

// Good: filter and page in SQL, then materialise only 10 rows.
var result = await db.Products
    .Where(p => p.Price > 100)
    .Take(10)
    .ToListAsync();
```

### N+1 Queries

Accessing a navigation property inside a loop after the query has materialised can fire one extra query per row. Use `Include` (or a `Select` projection) to load related data in the original query.

```csharp
// Bad: one query for orders, then one per order for its customer.
foreach (var order in await db.Orders.ToListAsync())
    Console.WriteLine(order.Customer.Name);

// Good: one query with a join.
var orders = await db.Orders.Include(o => o.Customer).ToListAsync();
```

## Choosing Between Them

| Situation | Use |
|---|---|
| Querying a database with EF Core | `IQueryable<T>` — keep filtering in SQL |
| In-memory collections (`List<T>`, arrays) | `IEnumerable<T>` — there is no provider |
| After `AsEnumerable()` / `ToList()` | `IEnumerable<T>` — the data is already local |
| Repository methods that callers will filter further | `IQueryable<T>` (or return a specification) |
| Returning data from an API | A materialised `List<T>` / DTO — never a live query |

## Key Takeaways

- `IQueryable<T>` builds an expression tree that a provider translates (usually to SQL); `IEnumerable<T>` executes in memory in your process.
- Both are deferred: the query runs only when enumerated, and enumerating twice runs it twice.
- Filter as much as possible before `ToList()` / `AsEnumerable()` so the database does the work and only matching rows cross the network.
- Returning `IEnumerable<T>` from a repository silently moves all later filtering into memory — return `IQueryable<T>` when callers need to compose further.
- Custom C# methods cannot be translated to SQL; narrow the data in SQL first, then apply custom logic in memory.
- Materialise once with `ToListAsync()` and reuse the list instead of enumerating the same query repeatedly.
