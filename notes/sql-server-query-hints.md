# SQL Server Query Hints

Query hints let you override the query optimizer's choices for a single
statement. They are a scalpel, not a default: the optimizer is usually
right, and a hint that helps today can hurt after data or statistics
change. Reach for hints only after measuring a real problem.

## OPTION Hints

`OPTION` hints apply to the whole query and go at the end of the
statement.

```sql
-- Force a fresh plan instead of reusing a cached one (fixes bad plans
-- caused by parameter sniffing on skewed data).
SELECT * FROM Orders WHERE CustomerId = @id
OPTION (RECOMPILE);

-- Tell the optimizer what value to plan for, without changing the
-- parameter actually used at runtime.
SELECT * FROM Orders WHERE Status = @status
OPTION (OPTIMIZE FOR (@status = 'Shipped'));

-- Cap parallelism for one heavy query so it does not starve the server.
SELECT COUNT(*) FROM BigTable
OPTION (MAXDOP 2);

-- Force join order to match the written order of the tables.
SELECT * FROM A JOIN B ON A.Id = B.AId JOIN C ON B.Id = C.BId
OPTION (FORCE ORDER);

-- Force a specific join algorithm for one query.
SELECT * FROM Orders o JOIN Customers c ON o.CustomerId = c.Id
OPTION (HASH JOIN);
```

## Table Hints

Table hints apply to one table in the `FROM` clause.

```sql
-- NOLOCK reads uncommitted data: dirty reads, missing or duplicated
-- rows are possible. Only acceptable for rough reporting, never for
-- anything that moves money or makes decisions.
SELECT * FROM Orders WITH (NOLOCK);

-- Force the optimizer to use a specific index.
SELECT * FROM Orders WITH (INDEX(IX_Orders_CustomerId))
WHERE CustomerId = 42;

-- Force an index seek instead of a scan when you know the seek is right.
SELECT * FROM Orders WITH (FORCESEEK)
WHERE CustomerId = 42;
```

## When Hints Help

- `RECOMPILE` on a statement whose optimal plan varies wildly with the
  parameter value (classic parameter-sniffing symptom).
- `OPTIMIZE FOR` when one parameter value dominates and the cached plan
  for a rare value is terrible for the common one.
- `MAXDOP` on a reporting query that monopolises all schedulers.
- `FORCE ORDER` / join hints when the optimizer repeatedly picks a nested
  loop over a huge outer table and you can prove a hash join is faster.

## When Hints Hurt

- **They freeze a decision.** A `FORCESEEK` or `INDEX` hint that is
  optimal on 10,000 rows can be disastrous on 10 million rows after the
  table grows.
- **They hide the real problem.** A bad plan is usually a symptom of stale
  statistics, a missing index, or a bad parameter value — fix those first.
- **NOLOCK corrupts results silently.** Dirty reads, rows read twice, rows
  skipped entirely during page splits. If correctness matters at all,
  use `READ COMMITTED SNAPSHOT` isolation instead.
- **They make upgrades painful.** A hint tuned for one SQL Server version
  can fight the optimizer in the next version.

## A Better First Checklist

Before adding any hint, check in order:

1. Are statistics up to date (`UPDATE STATISTICS`)?
2. Is there a missing index the plan is asking for?
3. Is the query written so the predicate is sargable (no function on the
   indexed column)?
4. Is parameter sniffing the cause (test with `RECOMPILE` as a diagnostic
   before committing to it)?

Only if all of those fail to explain the bad plan should a hint be the
answer — and then add a comment explaining why, so the next person does
not remove it blindly.

## Key Takeaways

- `OPTION (RECOMPILE, OPTIMIZE FOR, MAXDOP, FORCE ORDER)` and join
  hints override the optimizer for one statement.
- Table hints such as `INDEX` and `FORCESEEK` pin a single table's access
  path; `NOLOCK` trades correctness for speed and is rarely worth it.
- Hints help with parameter sniffing, runaway parallelism, and provably
  wrong join choices.
- Hints hurt when data grows, statistics change, or they mask a missing
  index or stale statistics.
- Measure first, fix statistics and indexes second, and hint last — with
  a comment saying why.
