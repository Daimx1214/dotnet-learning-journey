# SQL Server Statistics and Cardinality Estimation

The query optimizer never sees your data — it sees statistics. Statistics are samples of a column's values: a histogram of value distribution plus a density vector for uniqueness. Bad statistics mean bad row estimates, and bad estimates mean bad plans.

## What Statistics Contain

Every statistic object has three parts:

- **Header:** when it was last updated, how many rows were sampled, and how many rows existed.
- **Histogram:** up to 200 steps, each describing a range of values and how many rows fall in it (`RANGE_ROWS`, `EQ_ROWS`, `DISTINCT_RANGE_ROWS`).
- **Density vector:** average duplicates per value, used for estimates on columns without a histogram step match.

Inspect them with:

```sql
DBCC SHOW_STATISTICS ('Orders', 'IX_Orders_CustomerId') WITH HISTOGRAM;
```

Statistics are created automatically for index key columns and for columns used in `WHERE` clauses (if `AUTO_CREATE_STATISTICS` is on). Column statistics without an index are the optimizer's only guide for those filters.

## Auto-Update Thresholds

Statistics go stale as data changes. SQL Server updates them automatically when roughly 20% of the rows have changed (plus 500 rows) — the classic RT threshold. For a table with 10 million rows, that means 2 million changes before an update. Trace flag 2371 lowers the threshold dynamically for large tables, and SQL Server 2016+ enables that behavior by default.

```sql
-- Check when statistics were last updated
SELECT
    OBJECT_NAME(object_id) AS table_name,
    name AS stats_name,
    STATS_DATE(object_id, stats_id) AS last_updated
FROM sys.stats
WHERE OBJECT_NAME(object_id) = 'Orders';
```

## Symptoms of Stale Statistics

Stale statistics show up as a mismatch between estimated and actual rows in the execution plan:

- A plan that uses a nested loop for what turns out to be millions of rows.
- A hash join chosen for a tiny result set, wasting a build phase.
- Memory grants far too small (spills to tempdb) or far too large (wasted memory, fewer concurrent queries).
- A query that was fast yesterday and slow today after a bulk load, with no code change.

When the actual row count is 10x the estimate, suspect statistics before suspecting the query.

## Updating Statistics

Update statistics manually after bulk loads, large deletes, or a schema change:

```sql
-- All statistics on a table, sampled (default)
UPDATE STATISTICS Orders;

-- Full scan for the most accurate histogram on a critical table
UPDATE STATISTICS Orders WITH FULLSCAN;

-- One column statistic only
UPDATE STATISTICS Orders IX_Orders_CustomerId WITH FULLSCAN;
```

`FULLSCAN` is the most accurate but the slowest; use it on tables where plan quality matters more than maintenance time. `SAMPLE 50 PERCENT` is a middle ground for very large tables.

## Key Takeaways

- Statistics are the optimizer's view of your data: histogram plus density vector.
- The default auto-update threshold (20% of rows) is too high for large tables; TF 2371 / modern defaults help.
- Check freshness with `STATS_DATE` and `sys.stats` after bulk data changes.
- Estimated vs actual row mismatches in a plan are the classic stale-statistics symptom.
- Update with `UPDATE STATISTICS`, using `FULLSCAN` on tables where plan quality is critical.
- Statistics are created automatically for indexed columns and for columns in `WHERE` clauses.
