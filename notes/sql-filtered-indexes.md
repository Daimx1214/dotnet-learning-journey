# SQL Server Filtered Indexes

A filtered index is a nonclustered index with a `WHERE` clause: it only indexes the rows that satisfy the predicate. Instead of indexing all 2 million rows of a table, you index just the 3,000 active ones your queries actually touch. The result is a smaller index that is cheaper to maintain, faster to scan, and gives the optimizer better statistics for the subset it covers.

This matters in .NET backends because real tables are almost always skewed: most orders are completed, most users are active, most tickets are closed, most rows are old. Your queries usually target the small, interesting slice — and a filtered index is built exactly for that slice.

## Creating one

```sql
-- Only index orders that are still open (Status 1 = Open, 2 = Processing).
CREATE NONCLUSTERED INDEX IX_Orders_Open_Status
ON dbo.Orders (CustomerId, OrderDate)
INCLUDE (TotalAmount)
WHERE Status IN (1, 2);
```

The predicate can use `=`, `IN`, `IS NULL`, `IS NOT NULL`, comparisons, and `AND`/`OR` combinations of those. Only rows matching the predicate are stored in the index; other rows are invisible to it.

A query can use the index only when its `WHERE` clause implies the filter predicate — the optimizer must be able to prove that every row the query could return is in the index. In practice this means the query should contain the same condition (or a stricter one):

```sql
-- Uses IX_Orders_Open_Status: the query's predicate matches the filter.
SELECT OrderId, CustomerId, OrderDate, TotalAmount
FROM dbo.Orders
WHERE Status = 1 AND CustomerId = 42;

-- Does NOT use it: the query can return rows outside the filter.
SELECT OrderId, CustomerId, OrderDate, TotalAmount
FROM dbo.Orders
WHERE CustomerId = 42;
```

EF Core can create filtered indexes in migrations with `HasFilter`:

```csharp
modelBuilder.Entity<Order>()
    .HasIndex(o => new { o.CustomerId, o.OrderDate })
    .HasFilter("[Status] IN (1, 2)")
    .IncludeProperties(o => o.TotalAmount);
```

## When they help

- **Sparse values.** A `Status` column where 98% of rows are "Completed" and your queries only ever look at "Open" rows. A full index wastes space on rows nobody queries; a filtered index holds only the 2%.
- **NULL-heavy columns.** A nullable column like `ShippedDate` or `DeletedAt` where most rows are NULL. A filtered index `WHERE ShippedDate IS NULL` (or `IS NOT NULL`) covers exactly the rows you query, such as "orders not yet shipped".
- **Soft deletes.** Tables with an `IsDeleted` flag: `WHERE IsDeleted = 0` indexes only live rows, so every query on live data gets a small, dense index.
- **Unique constraints on a subset.** A filtered unique index enforces uniqueness only where it matters — for example, only one active discount per product, while historical discounts can repeat:

```sql
CREATE UNIQUE NONCLUSTERED INDEX UX_ProductDiscount_Active
ON dbo.ProductDiscounts (ProductId)
WHERE IsActive = 1;
```

## Maintenance savings

Every index costs something on every write: each insert, update, and delete must maintain it. A filtered index only pays that cost for rows matching its predicate. If 2% of rows are active, the filtered index is roughly 2% of the size of a full index, takes 2% of the maintenance work, and leaves more room in the buffer pool for data that matters. On write-heavy tables this is often the bigger win than the read speed.

Statistics are also better: the optimizer's statistics on a filtered index describe only the filtered rows, so its row estimates for queries on that subset are far more accurate than statistics over the whole table. This is one reason filtered indexes sometimes fix parameter sniffing-style bad plans for skewed columns.

## Verifying the optimizer actually uses it

A filtered index only helps if the optimizer picks it. Check the actual execution plan (Ctrl+M in SSMS, or "Include Actual Execution Plan") and look for an Index Seek on the filtered index. If the query uses a scan instead, the usual causes are:

- The query's predicate does not imply the filter — for example, the index is filtered on `Status IN (1, 2)` but the query filters on a parameter that could be any status. Make the query's predicate at least as strict as the filter.
- The session settings are wrong — `QUOTED_IDENTIFIER` or one of the ANSI settings is OFF, so the optimizer cannot consider the index at all. This is the most common cause when a query uses the index in SSMS but not from the application.
- The filtered index is not selective enough for this particular query, and the optimizer correctly prefers a different plan. A filtered index is not a guarantee — it is one more option for the optimizer.

You can also confirm an index exists and see its filter definition:

```sql
SELECT
    i.name AS IndexName,
    i.filter_definition AS FilterDefinition,
    i.is_unique,
    ips.page_count,
    ips.record_count
FROM sys.indexes AS i
LEFT JOIN sys.dm_db_index_physical_stats(
    DB_ID(), OBJECT_ID('dbo.Orders'), NULL, NULL, 'DETAILED') AS ips
    ON ips.index_id = i.index_id
WHERE i.object_id = OBJECT_ID('dbo.Orders')
  AND i.filter_definition IS NOT NULL;
```

## Filtered indexes vs included columns

A filtered index and an index with `INCLUDE` columns solve different problems, and they combine well. The filter decides *which rows* are stored; `INCLUDE` decides *which extra columns* are stored with them. The example at the top does both: it stores only open orders (`WHERE Status IN (1, 2)`) and it includes `TotalAmount` so the query is covered without a key lookup. A filtered index without the right included columns still forces a key lookup for every row it returns — on a small filtered set that is usually fine, but for a hot query, cover it.

As a rule of thumb: filter on the column that defines the slice (status, soft-delete flag, NULL-ness), put the columns your `WHERE` and `JOIN` clauses use in the key, and `INCLUDE` the columns your `SELECT` list needs.

## Maintenance and rebuilds

Filtered indexes are maintained like any other index, with one difference: only data changes to rows that match (or newly match) the predicate touch the index. A row that moves into the filtered set — for example, an order changing from Completed back to Open — is inserted into the index; a row that leaves the set is removed.

Fragmentation still happens and the index still needs occasional rebuilds or reorganizes, but on a much smaller structure, so maintenance windows are shorter. The usual guidance applies: rebuild when fragmentation is high and the index is large enough to matter, reorganize for moderate fragmentation, and update statistics after large data changes so the optimizer's estimates for the filtered subset stay accurate.

One caveat: because the index only contains a slice of the table, its statistics describe only that slice. That is normally an advantage, but if the slice changes shape quickly (for example, a status that flips for thousands of rows at once during a batch job), the statistics can go stale faster than a full-table index's would — schedule a statistics update after such batch operations.

## Limitations

- **The predicate must be deterministic.** Only columns, constants, and deterministic functions are allowed. You cannot use `GETDATE()`, `NEWID()`, non-deterministic functions, or subqueries in the filter.
- **SET options matter.** Creating or using a filtered index requires specific session settings: `ANSI_NULLS`, `ANSI_PADDING`, `ANSI_WARNINGS`, `ARITHABORT`, `CONCAT_NULL_YIELDS_NULL`, and `QUOTED_IDENTIFIER` must all be ON, and `NUMERIC_ROUNDABORT` must be OFF. Most clients and EF Core set these correctly by default, but if your application connects with different settings, the optimizer silently ignores the filtered index — a classic "it works in SSMS but not from the app" bug. Check with `SELECT SESSIONPROPERTY('ANSI_NULLS')` and friends if an index is being ignored.
- **The query must imply the filter.** If the query's predicate is broader than the index filter, the index cannot be used. Parameterized queries need care: a parameter like `@Status = 1` still implies the filter `Status IN (1, 2)`, so it works — but a parameter that could be any value does not.
- **Not for every query.** An index filtered on `Status IN (1, 2)` is useless to a query filtering on `Status = 3`. Do not create a filtered index per value; use them for the one or two slices your workload actually hammers.
- **Online operations and some features have restrictions.** Filtered indexes cannot be created on certain column types (for example, they cannot include `xml` or full-text columns in the key), and filtered unique indexes cannot be used as the target of a foreign key.

## Key Takeaways

- A filtered index adds a `WHERE` clause to a nonclustered index and stores only the matching rows, making it small, cheap to maintain, and fast for queries on that slice.
- They shine for sparse values, NULL-heavy columns, soft deletes, and enforcing uniqueness on a subset (e.g. one active discount per product).
- A query can use the index only when its own predicate implies the filter predicate; a broader query falls back to other access paths.
- Maintenance cost scales with the filtered row count, not the table size, and the statistics on the filtered subset are more accurate.
- The filter predicate must be deterministic — no `GETDATE()`, no subqueries — and the session must have `QUOTED_IDENTIFIER` and the other ANSI settings ON, or the optimizer silently ignores the index.
- Use filtered indexes for the one or two hot slices of a skewed table, not as a replacement for a well-chosen full index.
