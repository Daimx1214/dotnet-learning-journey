# EF Core Temporal Tables (EF Core)

A temporal table keeps the full history of every row automatically. SQL Server stores the current rows in the main table and moves every old version into a paired history table, stamped with the period during which that version was valid. EF Core 6 and later can configure this with `IsTemporal`, so you get point-in-time auditing — "what did this customer's address look like on 3 March?" — without writing audit triggers or a separate audit log.

## How Temporal Tables Work

A temporal table has two extra columns, usually `PeriodStart` and `PeriodEnd`, of type `datetime2`. They are system-versioned: SQL Server sets them automatically on every insert, update and delete. When a row is updated, the old version is copied to the history table with its period closed at the update time, and the new version becomes current with an open-ended period (`9999-12-31 23:59:59.9999999`). When a row is deleted, its final version is moved to history with the period closed at the delete time. The main table always holds exactly the current state, so ordinary queries are unaffected.

```csharp
public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Customer>()
        .ToTable("Customers", b => b.IsTemporal(
            b =>
            {
                b.UseHistoryTable("CustomersHistory");
                b.HasPeriodStart("PeriodStart");
                b.HasPeriodEnd("PeriodEnd");
            }));
}
```

With this configuration, `DbContext` migrations create the table with `SYSTEM_VERSIONING = ON`, the two period columns, and the history table. Your entity class does not need the period properties at all — EF Core manages them behind the scenes.

## Querying History with FOR SYSTEM_TIME

EF Core exposes the temporal query operators as LINQ extension methods on `DbSet<T>`. The most common is `TemporalAsOf`, which returns the state of every row as it was at a given instant.

```csharp
var asOf = new DateTime(2026, 3, 3, 12, 0, 0, DateTimeKind.Utc);

// What did the customers table look like at noon on 3 March?
List<Customer> snapshot = await context.Customers
    .TemporalAsOf(asOf)
    .OrderBy(c => c.Id)
    .ToListAsync();

// Every version of one customer between two dates, oldest first.
List<Customer> versions = await context.Customers
    .TemporalFromTo(new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                    new DateTime(2026, 3, 31, 23, 59, 59, DateTimeKind.Utc))
    .Where(c => c.Id == 42)
    .OrderBy(c => EF.Property<DateTime>(c, "PeriodStart"))
    .ToListAsync();
```

The generated SQL uses `FOR SYSTEM_TIME AS OF` and `FOR SYSTEM_TIME BETWEEN` respectively. `TemporalAll` returns every version of every row, `TemporalContainedIn` returns versions whose whole period falls inside a range, and `TemporalBetween` is inclusive of the start and exclusive of the end. One trap: `TemporalAsOf` uses the *database server's* clock semantics, so pass UTC values and make sure the app and the database agree on what "now" means.

## A Point-in-Time Auditing Example

Suppose a customer's email changes twice in March. The history table ends up with three versions:

| Version | Email | PeriodStart | PeriodEnd |
|---|---|---|---|
| 1 | old@example.com | 2026-02-10 09:00 | 2026-03-05 14:22 |
| 2 | new@example.com | 2026-03-05 14:22 | 2026-03-20 11:03 |
| 3 (current) | newest@example.com | 2026-03-20 11:03 | 9999-12-31 23:59:59 |

A support agent asking "which email did we have on file on 10 March?" runs `TemporalAsOf(2026-03-10)` and gets version 2, because 10 March falls inside its period (5 March 14:22 to 20 March 11:03). No audit table, no trigger, no application code wrote any of this — the database did it. This is the cheapest way to answer "who changed what, and when?" for regulated data, and it survives application bugs because the history is maintained at the storage layer.

## Retention and Cleanup

History grows forever by default, and that is the main operational cost. Every update doubles the storage of the affected row, so a table updated thousands of times a day accumulates history fast. SQL Server 2016+ supports a retention policy: set `HISTORY_RETENTION_PERIOD` on the table and a background cleanup task deletes history older than the period.

```sql
-- Keep at most 2 years of history; the cleanup task runs automatically.
ALTER TABLE dbo.Customers
SET (SYSTEM_VERSIONING = ON (HISTORY_RETENTION_PERIOD = 2 YEARS));
```

A few practical rules: keep retention as short as your compliance needs allow; put the history table on cheaper storage if the workload is large; and remember that retention cleanup is asynchronous — it runs on a schedule, not the instant a row ages out. Also note that temporal tables forbid `TRUNCATE TABLE` on the main table while versioning is on, and schema changes (adding a column, for example) must be applied to both tables — EF Core migrations handle this, but hand-written `ALTER TABLE` scripts often forget the history table.

## Key Takeaways

- Temporal tables give you full row history for free: SQL Server copies old versions to a history table with `PeriodStart`/`PeriodEnd` stamps on every update and delete.
- Configure them in EF Core with `IsTemporal`, naming the history table and the two period columns; the entity class itself needs no changes.
- Query history with `TemporalAsOf`, `TemporalFromTo`, `TemporalBetween`, `TemporalContainedIn` and `TemporalAll`, which translate to `FOR SYSTEM_TIME` SQL.
- They are ideal for point-in-time auditing — "what did this row look like on this date?" — because history is maintained at the storage layer, not by application code.
- History grows without bound, so set a `HISTORY_RETENTION_PERIOD` that matches your compliance needs and monitor history-table size.
- Watch the gotchas: pass UTC instants to temporal queries, remember `TRUNCATE` is blocked while versioning is on, and apply schema changes to the history table too.
