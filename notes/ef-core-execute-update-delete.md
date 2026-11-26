# EF Core 7+ ExecuteUpdate and ExecuteDelete

EF Core 7 introduced bulk update and delete operations that translate directly
to a single `UPDATE` or `DELETE` statement. Earlier versions forced you to load
every entity into memory, change it, and call `SaveChanges`, which is slow and
wasteful when you only need to change a column for many rows.

## The Problem With the Load-Then-Save Loop

The classic pattern loads all matching rows, tracks them, and issues one
`UPDATE` per row:

```csharp
var orders = await context.Orders
    .Where(o => o.Status == OrderStatus.Pending && o.CreatedAt < cutoff)
    .ToListAsync();

foreach (var order in orders)
{
    order.Status = OrderStatus.Expired;
}

await context.SaveChangesAsync();
```

For 10,000 rows this means 10,000 tracked entities, one `SELECT`, and 10,000
`UPDATE` statements.

## ExecuteUpdate

`ExecuteUpdateAsync` runs one `UPDATE` statement on the server. You describe
the change with `SetProperty` calls:

```csharp
int affected = await context.Orders
    .Where(o => o.Status == OrderStatus.Pending && o.CreatedAt < cutoff)
    .ExecuteUpdateAsync(setters => setters
        .SetProperty(o => o.Status, OrderStatus.Expired)
        .SetProperty(o => o.UpdatedAt, DateTime.UtcNow));

Console.WriteLine($"{affected} orders expired.");
```

The return value is the number of rows affected. `Where` must come before
`ExecuteUpdateAsync`; there is no `OrderBy`, `Skip`, or `Take` on the query.

## Setting Values From Other Columns

`SetProperty` can compute a new value from the current row, which lets you do
in-place arithmetic without loading anything:

```csharp
await context.Products
    .Where(p => p.CategoryId == categoryId)
    .ExecuteUpdateAsync(setters => setters
        .SetProperty(p => p.Price, p => p.Price * 1.10m)
        .SetProperty(p => p.Stock, p => p.Stock - p.Reserved));
```

The second lambda is translated to SQL, so `p.Price * 1.10m` becomes part of
the `SET` clause, not client-side code.

## ExecuteDelete

`ExecuteDeleteAsync` deletes matching rows with one `DELETE` statement:

```csharp
int deleted = await context.AuditLogs
    .Where(a => a.CreatedAt < DateTime.UtcNow.AddYears(-1))
    .ExecuteDeleteAsync();

Console.WriteLine($"{deleted} old audit rows removed.");
```

Use it for purging stale data and cleanup jobs.

## What Does Not Happen

Bulk operations bypass the change tracker entirely, so several things you may
expect from `SaveChanges` simply do not run:

- No entities are loaded or tracked, so no `DbContext` state is updated.
- `SaveChanges` interceptors and `SaveChanges` events do not fire.
- Concurrency tokens (`RowVersion`) are not checked.
- Navigation fix-up and cascade delete rules configured in the model do not
  apply; the database enforces its own foreign key actions.
- Value converters still apply, because the values are translated to SQL, but
  value comparers and shadow-property defaults set in `SaveChanges` do not.

If your model relies on a `SaveChanges` interceptor for audit columns, set
those columns explicitly with `SetProperty` in the same call.

## Transactions and Concurrency

Both methods run in the ambient transaction if one exists, and they participate
in `BeginTransactionAsync` like any other command. Because there is no
concurrency check, two concurrent bulk updates can overwrite each other
silently. If that matters, add a predicate that encodes the expected state:

```csharp
await context.Orders
    .Where(o => o.Id == orderId && o.Status == OrderStatus.Pending)
    .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, OrderStatus.Paid));
```

A return value of `0` then tells you the row was already changed.

## When to Prefer ExecuteUpdate Over a SaveChanges Loop

Use `ExecuteUpdate` / `ExecuteDelete` when the change is uniform across many
rows, no per-row business logic is needed, and no tracked entities depend on
the new values. Keep the load-then-save loop when each row needs different
validation, when domain events must be raised per entity, or when the number
of rows is small and the entities are already loaded.

## Key Takeaways

- `ExecuteUpdateAsync` and `ExecuteDeleteAsync` translate to a single SQL `UPDATE` or `DELETE`, with no entity loading.
- The return value is the number of rows affected, which is useful as a lightweight concurrency signal.
- `SetProperty` supports values computed from the current row, so in-place arithmetic works server-side.
- Bulk operations bypass the change tracker, interceptors, and concurrency tokens, so audit columns must be set explicitly.
- Prefer bulk operations for uniform changes over many rows; keep the tracked loop when per-row logic or domain events matter.
