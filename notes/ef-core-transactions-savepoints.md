# EF Core Transactions and Savepoints

A transaction makes several database operations atomic: either all of them commit, or none of them do. EF Core gives you explicit transactions, savepoints for partial rollback, and execution strategies that retry transient failures.

## Explicit Transactions with BeginTransaction

`SaveChanges` already wraps its changes in a transaction, but when one unit of work spans several `SaveChanges` calls — or mixes EF Core with raw SQL — you need an explicit transaction.

```csharp
await using var transaction = await db.Database.BeginTransactionAsync();
try
{
    var order = new Order { CustomerId = customerId, Total = total };
    db.Orders.Add(order);
    await db.SaveChangesAsync();

    await db.Database.ExecuteSqlRawAsync(
        "UPDATE Inventory SET Stock = Stock - 1 WHERE ProductId = {0}", productId);
    await db.SaveChangesAsync();

    await transaction.CommitAsync();
}
catch
{
    await transaction.RollbackAsync();
    throw;
}
```

`BeginTransactionAsync` starts the transaction on the connection EF Core is using. Commit at the end; roll back (or just dispose without committing) on failure. Note that disposing a transaction without committing rolls it back automatically.

## Savepoints: Partial Rollback

A savepoint marks a point inside a transaction you can roll back to without discarding everything before it. Useful when one step is allowed to fail — for example, an optional audit insert — while the rest must still commit.

```csharp
await using var transaction = await db.Database.BeginTransactionAsync();

db.Orders.Add(order);
await db.SaveChangesAsync();

await transaction.CreateSavepointAsync("BeforeAudit");
try
{
    db.AuditLog.Add(new AuditEntry { Action = "OrderCreated", OrderId = order.Id });
    await db.SaveChangesAsync();
}
catch
{
    // Undo only the audit step; the order is still there
    await transaction.RollbackToSavepointAsync("BeforeAudit");
}

await transaction.CommitAsync();
```

Savepoints are supported by SQL Server, PostgreSQL, and SQLite; the provider must support them, and they only make sense inside an explicit transaction. Rolling back to a savepoint does not end the transaction — you can keep working and still commit afterwards.

## Ambient Transactions with TransactionScope

`TransactionScope` creates an ambient transaction: any connection opened inside the scope automatically enlists in it, which is how you coordinate EF Core with another resource (a second DbContext, a message queue) in one atomic unit.

```csharp
using var scope = new TransactionScope(
    TransactionScopeOption.Required,
    new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted },
    TransactionScopeAsyncFlowOption.Enabled);   // required for async code

await using var db1 = new AppDbContext();
await using var db2 = new ReportingDbContext();
db1.Orders.Add(order);
db2.OrderEvents.Add(new OrderEvent { OrderId = order.Id });
await db1.SaveChangesAsync();
await db2.SaveChangesAsync();

scope.Complete();   // without this, everything rolls back
```

`TransactionScopeAsyncFlowOption.Enabled` is mandatory for async flows — without it the ambient transaction does not flow across awaits and the operations run outside the scope. Enlisting two different databases escalates to a distributed transaction, which many providers and cloud databases do not support; prefer a single database (or the outbox pattern) where possible.

## Execution Strategies and Retries

Transient failures — a dropped connection, a brief failover — should be retried automatically. EF Core's execution strategy retries the whole operation, but a retry is only safe if the operation is idempotent, and a manual transaction must be retried as a unit.

```csharp
var strategy = db.Database.CreateExecutionStrategy();
await strategy.ExecuteAsync(async () =>
{
    await using var transaction = await db.Database.BeginTransactionAsync();
    try
    {
        // ...all the work, including SaveChanges...
        await transaction.CommitAsync();
    }
    catch
    {
        await transaction.RollbackAsync();
        throw;
    }
});
```

The key rule: the transaction must be created *inside* the `ExecuteAsync` delegate, so a retry starts a fresh transaction. Creating it outside means a retry reuses a rolled-back transaction and fails. SQL Server's `SqlServerRetryingExecutionStrategy` is the default when configured; for other providers, register the appropriate strategy.

## Key Takeaways

- One `SaveChanges` is already transactional; use `BeginTransaction` when a unit of work spans several saves or raw SQL.
- Savepoints (`CreateSavepoint` / `RollbackToSavepoint`) allow partial rollback inside a transaction without losing earlier work.
- `TransactionScope` coordinates multiple resources in one ambient transaction; always pass `TransactionScopeAsyncFlowOption.Enabled` for async code.
- Without `scope.Complete()`, a `TransactionScope` rolls back — there is no implicit commit.
- Retries must wrap the whole transaction: create the transaction inside the execution strategy's delegate.
- Avoid distributed transactions across databases where possible; prefer one database or the outbox pattern.
