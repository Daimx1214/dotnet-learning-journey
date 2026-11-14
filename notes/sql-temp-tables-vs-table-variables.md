# Temp Tables vs Table Variables vs CTEs in SQL Server (SQL)

SQL Server offers three ways to hold intermediate results: `#temp` tables, `@table` variables, and common table expressions (CTEs). They look interchangeable, but they differ in scope, statistics, and how the optimizer treats them — and picking the wrong one is a classic cause of slow queries.

## #Temp Tables

A temp table is a real table in `tempdb`, visible to the current session, dropped automatically when the session ends (or explicitly with `DROP TABLE`).

```sql
-- Create and fill a temp table.
SELECT CustomerId, SUM(Total) AS TotalSpent
INTO #TopCustomers
FROM Orders
WHERE OrderDate >= '2026-01-01'
GROUP BY CustomerId;

-- Temp tables support indexes and statistics, just like real tables.
CREATE INDEX IX_TopCustomers_Total ON #TopCustomers (TotalSpent DESC);

SELECT TOP 10 * FROM #TopCustomers ORDER BY TotalSpent DESC;
DROP TABLE #TopCustomers;
```

Because temp tables have statistics and can be indexed, the optimizer can choose good plans for them. They are the right choice for large intermediate result sets (thousands of rows or more) and when the same result is reused in several later statements.

## @Table Variables

A table variable lives in memory (spilling to `tempdb` if large) and is scoped to the batch or procedure that declares it — it disappears when the batch ends.

```sql
DECLARE @TopCustomers TABLE (
    CustomerId INT PRIMARY KEY,
    TotalSpent DECIMAL(18, 2)
);

INSERT INTO @TopCustomers (CustomerId, TotalSpent)
SELECT CustomerId, SUM(Total)
FROM Orders
WHERE OrderDate >= '2026-01-01'
GROUP BY CustomerId;

SELECT TOP 10 * FROM @TopCustomers ORDER BY TotalSpent DESC;
```

Table variables have no column statistics, so the optimizer assumes they hold exactly one row. That assumption produces terrible plans for large sets: nested loops where a hash join was needed, or a full scan where a seek would do. They also cannot be indexed after creation (only inline `PRIMARY KEY` / `UNIQUE` constraints) and cannot be altered. Use them only for small sets — tens to a few hundred rows — where the one-row estimate is harmless.

## CTEs (Common Table Expressions)

A CTE is not a table at all. It is a named subquery, visible only to the single statement that follows it, and SQL Server may re-evaluate it every time it is referenced.

```sql
WITH TopCustomers AS (
    SELECT CustomerId, SUM(Total) AS TotalSpent
    FROM Orders
    WHERE OrderDate >= '2026-01-01'
    GROUP BY CustomerId
)
SELECT TOP 10 CustomerId, TotalSpent
FROM TopCustomers
ORDER BY TotalSpent DESC;
```

CTEs are perfect for readability and for recursive queries (hierarchies, org charts). They are the wrong tool when the intermediate result is referenced twice in the same statement — SQL Server can compute it twice — or when the result is large and deserves statistics. A CTE also cannot be indexed.

## Choosing Between Them

| Situation | Pick |
|---|---|
| Large intermediate result, reused in several statements | `#temp` table |
| Small set (< ~100 rows), single use | `@table` variable |
| Readability, or a recursive query, single statement | CTE |
| Result referenced more than once in one statement | `#temp` table (a CTE may be recomputed) |

One more difference: temp tables cause recompiles. Creating a `#temp` table inside a stored procedure and then altering it can force the procedure to recompile on each run. Table variables do not cause recompiles, which is a genuine (if narrow) reason to prefer them in hot procedures with small data.

## Key takeaways

- `#temp` tables are real tables with statistics and indexes — best for large, reused intermediate results.
- `@table` variables assume one row in the plan and cannot be indexed — only for small sets.
- CTEs are readable named subqueries, possibly recomputed per reference — best for readability and recursion, not for large reused data.
