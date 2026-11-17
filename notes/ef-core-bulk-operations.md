# EF Core Bulk Operations (EF Core)

Before EF Core 7, updating many rows meant loading every entity into the change tracker, modifying it, and calling `SaveChanges` — one `UPDATE` per row, plus the memory and round-trip cost of loading them all. `ExecuteUpdate` and `ExecuteDelete` (EF Core 7+) do the job as a single set-based SQL statement, without loading any entities. They are the right tool for "update all rows matching this filter" jobs: bulk status changes, soft deletes, and data fixes.

## ExecuteUpdate: Set-Based Updates

`ExecuteUpdate` takes a query and a set of property assignments, and translates them into one `UPDATE` statement. It returns the number of affected rows, and there is no `SaveChanges` — the statement executes immediately.

```csharp
// Give every inactive customer a 10% loyalty discount in one statement.
int affected = await db.Customers
    .Where(c => !c.IsActive)
    .ExecuteUpdateAsync(setters => setters
        .SetProperty(c => c.DiscountPercent, 10)
        .SetProperty(c => c.UpdatedAt, DateTime.UtcNow));

// Equivalent SQL (SQL Server):
// UPDATE [c] SET [c].[DiscountPercent] = 10, [c].[UpdatedAt] = @now
// FROM [Customers] AS [c] WHERE [c].[IsActive] = 0
```

The setters can use values computed from the entity itself — `.SetProperty(c => c.Salary, c => c.Salary * 1.05m)` raises every matching salary by 5% — but only expressions the provider can translate to SQL. Anything that cannot be translated (a call to a local method, for example) fails at runtime, so keep setter expressions to property access and operators the provider supports.

## ExecuteDelete: Set-Based Deletes

`ExecuteDelete` is the same idea for deletion: one `DELETE` statement for every row the query matches.

```csharp
// Delete all audit-log rows older than a year in one statement.
int deleted = await db.AuditLogs
    .Where(log => log.CreatedAt < DateTime.UtcNow.AddYears(-1))
    .ExecuteDeleteAsync();

// Equivalent SQL (SQL Server):
// DELETE FROM [a] FROM [AuditLogs] AS [a] WHERE [a].[CreatedAt] < @cutoff
```

There is no `SaveChanges`, no change tracker involvement, and no entity materialization — the rows are gone in a single round trip.

## Limitations

Set-based operations deliberately bypass the parts of EF Core that only make sense for tracked entities:

- **No change tracker:** nothing is loaded, so no entity events fire and nothing appears in `ChangeTracker.Entries()`.
- **No SaveChanges:** the statement runs immediately; it is not part of a unit-of-work batch. Wrap it in an explicit transaction (`await using var tx = await db.Database.BeginTransactionAsync()`) if it must be atomic with other work.
- **No validation or interceptors:** data annotations, `SaveChanges` interceptors, and value converters' in-memory logic do not run — the SQL goes straight to the database. (Value *conversion* to the store type still applies, since it is part of the mapping.)
- **No concurrency tokens:** `ExecuteUpdate` does not check a `RowVersion`/concurrency token, so optimistic concurrency does not apply. If two writers race, the last statement wins.
- **No navigation updates:** setters cannot assign through navigation properties (`SetProperty(c => c.Address.City, ...)` is not supported); update the related table with its own `ExecuteUpdate`.
- **Deletes do not cascade through EF:** dependent rows are removed only if the database has a cascade configured; EF Core's client-side cascade delete does not run, so without a database cascade you get a foreign-key violation.

## Loading + SaveChanges vs ExecuteUpdate

| Aspect | Load + `SaveChanges` loop | `ExecuteUpdate` / `ExecuteDelete` |
|--------|---------------------------|-----------------------------------|
| Round trips | 1 SELECT + 1 statement per row | 1 statement total |
| Memory | All entities loaded and tracked | None |
| Validation, interceptors, concurrency | Runs normally | Bypassed |
| Per-row logic | Full C# logic per entity | Only translatable SQL expressions |
| Best for | Small sets, per-row rules, audited changes | Large sets, uniform changes, data fixes |

The choice is about the shape of the work, not just speed. Updating 20 orders with per-order business rules belongs in the `SaveChanges` loop, where validation and concurrency protection apply. Marking 50,000 stale sessions expired belongs in `ExecuteUpdate`. A common middle ground is to use `ExecuteUpdate` for the bulk change and a separate tracked update for the few rows that need special handling.

## Key Takeaways

- `ExecuteUpdate` and `ExecuteDelete` (EF Core 7+) run one set-based SQL statement for all matching rows, without loading any entities.
- They return the affected row count and execute immediately — there is no `SaveChanges` call.
- Setter expressions must be translatable to SQL; they can reference the entity's own properties (for example, a 5% salary raise).
- They bypass the change tracker, validation, `SaveChanges` interceptors, and optimistic concurrency — wrap them in an explicit transaction when atomicity matters.
- `ExecuteDelete` relies on database cascades; EF Core's client-side cascade does not run.
- Use them for large uniform changes; keep the load-and-`SaveChanges` loop for small sets with per-row rules or audit requirements.
