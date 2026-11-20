# SQL Server Query Store

Query Store is a flight-data recorder for your database. It captures query text, execution plans, and runtime statistics, so you can see what changed when performance regresses — even after the plan cache has been flushed.

## What Query Store Captures

For every query it records the text, each plan the optimizer produced, and runtime stats per plan: execution count, duration, CPU, logical reads, and memory grants. This history survives restarts and plan-cache evictions, which is exactly what the plan cache cannot do.

Enable it per database and choose sensible retention:

```sql
ALTER DATABASE ShopDb SET QUERY_STORE = ON
(
    OPERATION_MODE = READ_WRITE,
    CLEANUP_POLICY = (STALE_QUERY_THRESHOLD_DAYS = 30),
    DATA_FLUSH_INTERVAL_SECONDS = 900,
    MAX_STORAGE_SIZE_MB = 1000
);
```

Check the current state with `SELECT * FROM sys.database_query_store_options;`.

## Finding Regressed Queries

The built-in reports in SSMS ("Regressed Queries", "Top Resource Consuming Queries") read Query Store, but the DMVs give you the same data in T-SQL. This query lists the slowest queries by average duration:

```sql
SELECT TOP 10
    qst.query_sql_text,
    rs.avg_duration / 1000 AS avg_duration_ms,
    rs.count_executions,
    rs.avg_logical_io_reads
FROM sys.query_store_query_text AS qst
JOIN sys.query_store_query AS q
    ON qst.query_text_id = q.query_text_id
JOIN sys.query_store_plan AS p
    ON q.query_id = p.query_id
JOIN sys.query_store_runtime_stats AS rs
    ON p.plan_id = rs.plan_id
ORDER BY rs.avg_duration DESC;
```

A query whose average duration suddenly doubled usually means the optimizer picked a different plan — compare the plans for that `query_id` across time.

## Forcing a Good Plan

When you know which plan is best, force it so the optimizer stops experimenting:

```sql
-- Force plan 42 for query 7
EXEC sys.sp_query_store_force_plan @query_id = 7, @plan_id = 42;

-- Remove the forcing later
EXEC sys.sp_query_store_unforce_plan @query_id = 7, @plan_id = 42;
```

Forcing is a safety net, not a fix: it pins one plan while you investigate the root cause (stale statistics, a bad parameter sniff, a missing index). Check forced plans regularly — a forced plan can itself go stale as data grows. Automatic plan correction (available in SQL Server 2017+) can force the last known good plan for you when it detects a regression.

## Key Takeaways

- Query Store persists query text, plans, and runtime stats across restarts and cache flushes.
- Enable it per database with sensible retention and storage limits.
- Use `sys.query_store_query_text`, `sys.query_store_plan`, and `sys.query_store_runtime_stats` to find regressed queries.
- `sp_query_store_force_plan` pins a known-good plan while you fix the root cause.
- Forcing is a temporary safety net; investigate stale stats, parameter sniffing, or missing indexes.
- Automatic plan correction can detect regressions and force the last good plan for you.
