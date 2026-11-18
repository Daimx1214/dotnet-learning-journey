# SQL Server Parameter Sniffing

Parameter sniffing is SQL Server's habit of building a query plan using the actual parameter values passed on the first execution of a stored procedure or parameterized query, and then reusing that plan for every later call. That sounds sensible — the optimizer uses real values instead of guessing — but it becomes a problem when later calls pass very different values. A plan that is perfect for one parameter value can be terrible for another, and SQL Server keeps reusing the cached plan anyway.

This matters in real .NET backends because almost every query your API runs is parameterized: EF Core generates parameters for every `Where` clause, and Dapper and ADO.NET do the same. A report endpoint that is fast for one customer and times out for another is often a parameter sniffing problem, not a missing index.

## How it works

When a stored procedure or parameterized query runs for the first time, SQL Server compiles an execution plan. During compilation it "sniffs" the parameter values — it looks at the actual values and checks the statistics histogram to estimate how many rows each value will return. That estimate drives the plan: a small estimate gets a nested loop join and a key lookup; a large estimate gets a hash join and a scan.

The compiled plan is then stored in the plan cache. Every later execution with different parameter values reuses the same plan without recompiling. The optimizer never re-checks whether the plan still fits the new values.

```sql
CREATE PROCEDURE dbo.GetOrdersByStatus
    @Status INT
AS
SELECT OrderId, CustomerId, TotalAmount, OrderDate
FROM dbo.Orders
WHERE Status = @Status;
GO

-- First call: Status = 5 (cancelled) matches only 12 rows out of 2 million.
-- The optimizer builds a plan with an index seek + key lookup. Perfect.
EXEC dbo.GetOrdersByStatus @Status = 5;

-- Second call: Status = 1 (completed) matches 1.4 million rows.
-- SQL Server reuses the seek + key lookup plan: 1.4 million key lookups.
-- This call runs for minutes instead of seconds.
EXEC dbo.GetOrdersByStatus @Status = 1;
```

## Symptoms

The classic symptom is a query that is fast sometimes and painfully slow other times, with the same query text and the same data, depending only on the parameter value. Other clues:

- The stored procedure is slow from the application but fast when you run it in SSMS with the same parameters (SSMS usually uses different settings, and the "slow" plan is the cached one — this is the well-known "parameter sniffing in SSMS" confusion).
- The query runs fast right after `DBCC FREEPROCCACHE` or after a statistics update, then gets slow again once a bad plan is cached.
- The execution plan shows a huge gap between estimated and actual rows: estimated 10 rows, actual 1,000,000 rows, with a nested loop join doing millions of lookups.
- One specific parameter value is always slow while the rest are fine.

You can confirm it by comparing the plan for the slow value against a plan compiled fresh for that value:

```sql
-- See the cached plan and the parameter values it was compiled with.
SELECT
    cp.plan_handle,
    qp.query_plan,
    cp.usecounts
FROM sys.dm_exec_cached_plans AS cp
CROSS APPLY sys.dm_exec_query_plan(cp.plan_handle) AS qp
WHERE qp.query_plan.exist('//*[contains(local-name(), "GetOrdersByStatus")]') = 1;
```

## Fix 1: OPTION (RECOMPILE)

Adding `OPTION (RECOMPILE)` to the query forces SQL Server to compile a fresh plan on every execution, using the current parameter values. The plan is never cached, so it can never go stale.

```sql
CREATE PROCEDURE dbo.GetOrdersByStatus
    @Status INT
AS
SELECT OrderId, CustomerId, TotalAmount, OrderDate
FROM dbo.Orders
WHERE Status = @Status
OPTION (RECOMPILE);
```

This is the simplest and most reliable fix, and it is usually the right first choice. The cost is compilation on every call, which is typically a few milliseconds — trivial for a query that runs a few times a second, but wasteful for a query that runs hundreds of times a second. Use it when the query runs at low-to-moderate frequency, or when the data distribution is so skewed that no single plan can ever be good for all values.

In EF Core you can apply it with a query hint via `FromSqlRaw`, or with an interceptor that appends the hint. There is no built-in LINQ operator for it.

## Fix 2: OPTIMIZE FOR

`OPTIMIZE FOR` tells the optimizer to build the plan as if a specific value (or the "typical" value) was passed, instead of whichever value happened to run first. The plan is still cached and reused, but at least it is built for a representative value.

```sql
SELECT OrderId, CustomerId, TotalAmount, OrderDate
FROM dbo.Orders
WHERE Status = @Status
OPTION (OPTIMIZE FOR (@Status = 1));  -- 1 is the common value (80% of rows)
```

`OPTIMIZE FOR UNKNOWN` is a variation: the optimizer uses average density statistics instead of any specific value, producing a middle-of-the-road plan. Use `OPTIMIZE FOR` when one value dominates the workload and the other values are rare, or when you know the distribution well. The risk is that it is a guess frozen in the query — if the data distribution changes, the chosen value may stop being representative.

## Fix 3: Local variables

Assigning the parameter to a local variable before using it in the query hides the value from the optimizer. SQL Server then builds the plan from average statistics instead of the sniffed value.

```sql
CREATE PROCEDURE dbo.GetOrdersByStatus
    @Status INT
AS
BEGIN
    DECLARE @LocalStatus INT = @Status;

    SELECT OrderId, CustomerId, TotalAmount, OrderDate
    FROM dbo.Orders
    WHERE Status = @LocalStatus;
END
```

This produces the same kind of "average" plan as `OPTIMIZE FOR UNKNOWN`, with no query hint and no recompilation cost. It is a good fit when the distribution is fairly even and any reasonable plan works for all values. The downside is the same as its benefit: the plan is generic, so it is never optimal for any specific value, and it can be bad when the distribution is heavily skewed.

## Fix 4: Separate code paths

When the data is truly bimodal — one value returns a handful of rows and another returns half the table — no single plan can serve both. Split the query into branches so each value gets its own plan.

```sql
CREATE PROCEDURE dbo.GetOrdersByStatus
    @Status INT
AS
BEGIN
    IF @Status = 5  -- rare value: seek plan
    BEGIN
        SELECT OrderId, CustomerId, TotalAmount, OrderDate
        FROM dbo.Orders
        WHERE Status = 5;
    END
    ELSE            -- common values: scan/hash plan
    BEGIN
        SELECT OrderId, CustomerId, TotalAmount, OrderDate
        FROM dbo.Orders
        WHERE Status = @Status;
    END
END
```

Each branch compiles its own plan, so the rare value gets a seek and the common value gets a scan. Use this when you have a small number of clearly distinct cases and the plan difference is large. It adds maintenance cost — two copies of the query to keep in sync — so reserve it for the queries that actually hurt.

## Fix 5: Plan guides and Query Store

For queries you cannot change — third-party code, or EF Core-generated SQL you do not want to rewrite — a plan guide can attach a hint like `OPTION (RECOMPILE)` to a query by its text, without touching the code. Query Store (enabled by default in modern SQL Server) lets you force a known-good plan for a query id through SSMS or T-SQL.

```sql
-- Force a good plan that Query Store has already seen.
EXEC sp_query_store_force_plan @query_id = 42, @plan_id = 17;
```

These are administrative fixes, best used when the code is out of your hands or when you need an emergency fix in production without a deployment. They are fragile — a plan guide matches on exact query text, so a whitespace change in the application breaks it — so prefer a code fix when you own the code.

## Which fix fits when

- **Low-frequency query, skewed data:** `OPTION (RECOMPILE)` — simple, always correct, small compilation cost.
- **One dominant value, stable distribution:** `OPTIMIZE FOR (@Param = <common value>)`.
- **Even distribution, any plan works:** local variables or `OPTIMIZE FOR UNKNOWN`.
- **Bimodal data, two very different plans needed:** separate code paths with `IF`.
- **Cannot change the code:** plan guide or Query Store plan forcing.

Also check the basics first: stale statistics cause the same symptoms as parameter sniffing, because the optimizer's row estimates are wrong for every value. Make sure statistics are up to date and that the skewed column actually has an index the optimizer can choose before reaching for hints.

## Key Takeaways

- Parameter sniffing means the first execution's values shape the cached plan, which is then reused for all later values — great when values are similar, disastrous when they are not.
- The signature symptom is the same query being fast for one parameter value and slow for another, with a big estimated-vs-actual row gap in the plan.
- `OPTION (RECOMPILE)` is the simplest reliable fix for low-to-moderate frequency queries: a fresh, correct plan every time at the cost of recompilation.
- `OPTIMIZE FOR` builds the cached plan for a chosen representative value; local variables produce a generic average plan instead.
- When the data is truly bimodal, split the query into separate branches so each case gets its own plan.
- Plan guides and Query Store plan forcing are the fallback when you cannot change the query code.
