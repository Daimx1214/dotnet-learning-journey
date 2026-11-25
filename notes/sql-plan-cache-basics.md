# SQL Server Plan Cache Basics

SQL Server does not execute a query the moment it arrives. It first compiles the query text into an execution plan, and then it caches that plan so the next identical query can skip compilation. The plan cache is why parameterized queries are fast and why ad-hoc SQL with embedded literals can be slow.

## How Compilation and Caching Work

When a batch arrives, SQL Server hashes the query text and looks in the plan cache. On a cache hit it reuses the compiled plan and starts executing immediately. On a cache miss it parses the text, optimizes it, builds a plan, executes it, and stores the plan for reuse.

Compilation is expensive relative to execution for short queries: the optimizer explores join orders, index choices, and statistics. A query that runs in 2 ms might take 20 ms to compile. If that query runs thousands of times a second, skipping compilation is a large win.

## Inspecting the Cache

The plan cache is visible through dynamic management views:

```sql
-- How many plans are cached, and how often each is reused.
SELECT
    objtype,
    cacheobjtype,
    usecounts,
    size_in_bytes / 1024.0 AS size_kb,
    text
FROM sys.dm_exec_cached_plans
CROSS APPLY sys.dm_exec_sql_text(plan_handle)
ORDER BY usecounts DESC;
```

A healthy cache shows a modest number of plans with high `usecounts`: the same plan reused thousands of times. A cache full of thousands of plans each used once is a sign of trouble, usually unparameterized ad-hoc SQL.

```sql
-- Find single-use plans wasting cache memory.
SELECT COUNT(*) AS single_use_plans,
       SUM(size_in_bytes) / 1024.0 / 1024.0 AS size_mb
FROM sys.dm_exec_cached_plans
WHERE usecounts = 1 AND objtype = 'Adhoc';
```

`sys.dm_exec_cached_plans` shows the cached plan handles; `sys.dm_exec_sql_text` returns the SQL text for a handle, and `sys.dm_exec_query_plan` returns the XML plan. `DBCC FREEPROCCACHE` clears the whole cache and is a troubleshooting tool, never a routine operation on a production server.

## Plan Reuse and Parameterization

Two queries with different literal values are different cache entries:

```sql
SELECT * FROM Orders WHERE CustomerId = 101;
SELECT * FROM Orders WHERE CustomerId = 202;
```

Each literal produces its own plan, so a thousand customers means a thousand plans. Parameters fix this: the plan is compiled once with a parameter placeholder, and every execution reuses it:

```sql
SELECT * FROM Orders WHERE CustomerId = @CustomerId;
```

`sp_executesql` is the way to send parameterized SQL from application code, and it is what ADO.NET and EF Core generate under the hood:

```sql
EXEC sp_executesql
    N'SELECT * FROM Orders WHERE CustomerId = @CustomerId',
    N'@CustomerId INT',
    @CustomerId = 101;
```

The first parameter is the query text, the second declares the parameter types, and the rest supply values. Because the text is constant, the plan is cached once and reused for every customer. `sp_executesql` also protects against SQL injection, since values are never concatenated into the text.

## Parameter Sniffing

Plan reuse has a catch: the plan is optimized for the parameter values seen at first compilation. If the first call passes a rare value, the optimizer may pick a plan that is terrible for a common value, and that plan is then reused for every later call. This is parameter sniffing, and it is the most common cause of a query that is fast in testing and slow in production.

Symptoms: the same query runs in milliseconds with one parameter and seconds with another, while the cached plan looks reasonable for the fast case. Fixes include `OPTION (RECOMPILE)`, `OPTIMIZE FOR`, or restructuring the query so the parameter distribution matters less.

## When to Recompile

`OPTION (RECOMPILE)` tells SQL Server to compile a fresh plan on every execution and not to cache it:

```sql
SELECT * FROM Orders
WHERE CustomerId = @CustomerId
  AND (@Status IS NULL OR Status = @Status)
OPTION (RECOMPILE);
```

This is the right choice for queries with optional filters or wildly varying selectivity, where a single cached plan cannot serve all parameter combinations. The cost is compilation on every run, so it suits queries that run occasionally, not thousands of times per second.

Forcing recompilation of a whole stored procedure is possible with `WITH RECOMPILE`, and `sp_recompile` marks a procedure or table for recompilation on its next use, which is useful after a statistics update or an index change.

## Key Takeaways

- SQL Server compiles a query into an execution plan and caches it; reuse skips the expensive compile step.
- `sys.dm_exec_cached_plans` shows cached plans and their reuse counts; many single-use plans signal unparameterized ad-hoc SQL.
- Parameters and `sp_executesql` make one cached plan serve every value, which keeps the cache small and fast.
- Parameter sniffing happens when the first compiled values produce a plan that is bad for later values; watch for one parameter being fast and another slow.
- `OPTION (RECOMPILE)` trades compilation cost for a plan tuned to the current parameters, which is right for optional filters and skewed data.
