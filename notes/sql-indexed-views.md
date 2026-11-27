# Indexed Views in SQL Server

A normal view is just a saved query — SQL Server re-runs it every time you query it. An *indexed view* (a materialized view) stores the view's result set physically on disk, maintained automatically as the base tables change. Queries against it can read pre-computed aggregates instead of scanning and grouping the base tables.

## When it pays off

Indexed views shine for expensive, frequently-run aggregations over large tables that change relatively slowly — sales totals per order, monthly revenue per region, inventory balances. The trade-off is write overhead: every insert or update on a base table must also maintain the view's index.

## The example: order totals

Start with two tables and a view that sums line totals per order. The view must use `SCHEMABINDING`, which locks the base tables' schema so the view's stored data can never go stale.

```sql
CREATE TABLE dbo.Orders
(
    OrderId INT PRIMARY KEY,
    CustomerId INT NOT NULL,
    OrderDate DATE NOT NULL
);

CREATE TABLE dbo.OrderDetails
(
    OrderDetailId INT PRIMARY KEY,
    OrderId INT NOT NULL REFERENCES dbo.Orders (OrderId),
    UnitPrice DECIMAL(10, 2) NOT NULL,
    Quantity INT NOT NULL
);
```

```sql
CREATE VIEW dbo.vw_OrderTotals
WITH SCHEMABINDING
AS
SELECT
    od.OrderId,
    COUNT_BIG(*) AS LineCount,
    SUM(od.UnitPrice * od.Quantity) AS OrderTotal
FROM dbo.OrderDetails AS od
GROUP BY od.OrderId;
GO
```

Note `COUNT_BIG(*)` — it is required in any indexed view that uses `GROUP BY`, because the query optimizer needs an exact row count to maintain the aggregate.

## Creating the index

The first index on a view must be unique and clustered; it materializes the view:

```sql
CREATE UNIQUE CLUSTERED INDEX IX_vw_OrderTotals_OrderId
    ON dbo.vw_OrderTotals (OrderId);
GO
```

After this, `SELECT OrderTotal FROM dbo.vw_OrderTotals WHERE OrderId = 101` reads one row from the view's clustered index instead of scanning `OrderDetails` and summing. In Enterprise Edition the optimizer may even use the indexed view automatically for queries against the base tables; in Standard Edition you must reference the view directly (with `NOEXPAND`).

## Worked numbers

Insert two lines for order 101:

```sql
INSERT INTO dbo.OrderDetails (OrderDetailId, OrderId, UnitPrice, Quantity)
VALUES
    (1, 101, 25.00, 2),
    (2, 101, 10.00, 1);
```

The view computes the total by hand as:

```text
Line 1: 2 x 25.00 = 50.00
Line 2: 1 x 10.00 = 10.00
Total:   50.00 + 10.00 = 60.00
```

```sql
SELECT OrderId, LineCount, OrderTotal
FROM dbo.vw_OrderTotals
WHERE OrderId = 101;
-- OrderId | LineCount | OrderTotal
-- 101     | 2         | 60.00
```

## Hard restrictions

Indexed views are picky. The view definition must satisfy all of these:

- **No `OUTER JOIN`s.** Only inner joins are allowed.
- **No subqueries** in the `SELECT` list or `WHERE` clause.
- **No `UNION`, `EXCEPT`, or `INTERSECT`.**
- **Deterministic expressions only.** No `GETDATE()`, no `NEWID()`, no non-deterministic functions.
- **Two-part names everywhere.** Base tables must be referenced as `dbo.OrderDetails`, never just `OrderDetails`.
- **No `SELECT *`.** Every column must be named explicitly.
- **`GROUP BY` requires `COUNT_BIG(*)`.** Aggregates other than `SUM`/`COUNT_BIG` (e.g. `AVG`, `MIN`, `MAX`) cannot be indexed directly — derive `AVG` from `SUM / COUNT_BIG` instead.
- The view must be created with `SCHEMABINDING`, and ANSI settings (`ANSI_NULLS`, `QUOTED_IDENTIFIER`, etc.) must be ON when creating the view and its indexes.

## Key Takeaways

- An indexed view stores its result set physically; the first index must be unique and clustered.
- `WITH SCHEMABINDING` is mandatory, and `GROUP BY` views must include `COUNT_BIG(*)`.
- Worked example: order 101 with lines 2 x 25.00 and 1 x 10.00 totals exactly 60.00.
- Hard limits: no outer joins, no subqueries, no UNION, deterministic expressions only, two-part table names.
- Best for read-heavy aggregates over slowly changing tables; every base-table write pays maintenance cost.
