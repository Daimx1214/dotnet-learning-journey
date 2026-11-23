# Finding Missing Indexes with DMVs

SQL Server tracks the indexes the query optimizer wished existed. The missing-index dynamic management views (DMVs) expose those wishes — which columns were used for seeks, which for filtering, and how much the optimizer expected to gain — so a DBA can turn them into real `CREATE INDEX` statements instead of guessing.

## The Three DMVs

The data is split across three views that join on `index_handle`:

```sql
SELECT
    migs.avg_total_user_cost,
    migs.avg_user_impact,
    migs.user_seeks,
    migs.user_scans,
    mid.statement AS TableName,
    mid.equality_columns,
    mid.inequality_columns,
    mid.included_columns
FROM sys.dm_db_missing_index_groups AS mig
INNER JOIN sys.dm_db_missing_index_group_stats AS migs
    ON migs.group_handle = mig.index_group_handle
INNER JOIN sys.dm_db_missing_index_details AS mid
    ON mid.index_handle = mig.index_handle
WHERE mid.database_id = DB_ID()
ORDER BY migs.avg_total_user_cost * migs.avg_user_impact * (migs.user_seeks + migs.user_scans) DESC;
```

- `sys.dm_db_missing_index_details`: the table and the column lists.
- `sys.dm_db_missing_index_group_stats`: the usage counts and the estimated benefit.
- `sys.dm_db_missing_index_groups`: the link between the two (a group can hold several candidate indexes).

These DMVs are cumulative since the last SQL Server restart (or since `DBCC FREEPROCCACHE` cleared the plan cache). After a restart every counter is zero, so a suggestion with `user_seeks = 2` after one hour of uptime is weaker evidence than the same count after a month.

## Interpreting Equality, Inequality, and Included Columns

The three column lists map directly onto the structure of a `CREATE INDEX`:

| DMV column | Meaning | Index role |
|---|---|---|
| `equality_columns` | Columns used with `=` in `WHERE`/`JOIN` | Key columns, first, in this order |
| `inequality_columns` | Columns used with `>`, `<`, `BETWEEN`, `LIKE` | Key columns, after the equality columns |
| `included_columns` | Columns only returned by the `SELECT` list | `INCLUDE` columns (leaf level only) |

Equality columns go first because a B-tree seek can use them exactly; inequality columns go after because only the first inequality column can be seeked efficiently and the rest are filtered. Included columns are not part of the key — they make the index covering so the query never touches the table — but they make the index wider.

```sql
-- DMV output: equality = [CustomerId], inequality = [OrderDate],
-- included = [TotalAmount], table = Sales.Orders
-- becomes:
CREATE NONCLUSTERED INDEX IX_Orders_CustomerId_OrderDate
ON Sales.Orders (CustomerId, OrderDate)
INCLUDE (TotalAmount);
```

## avg_user_impact and user_seeks

Two numbers decide whether a suggestion is worth acting on:

- `avg_user_impact`: the average percentage by which the optimizer expects user queries to improve if this index existed. It is an estimate, not a measurement.
- `user_seeks` (plus `user_scans`): how many times the missing index would have been used. A high impact on a query that runs twice a day is worth less than a moderate impact on a query that runs ten thousand times a day.

Multiply them to rank suggestions: `avg_total_user_cost * avg_user_impact * (user_seeks + user_scans)` estimates the total cost saved. `avg_total_user_cost` is the average cost of the queries that would have used the index, so the product weights expensive, frequent, high-impact suggestions first.

## Worked Example: Turning One DMV Row into an Index

A DMV row for `Sales.Orders` reads:

```text
equality_columns   = [CustomerId]
inequality_columns = [OrderDate]
included_columns   = [TotalAmount]
user_seeks         = 1200
user_scans         = 0
avg_user_impact    = 85.2
avg_total_user_cost = 14.5
```

Step 1 — rank it. Estimated total benefit = 14.5 × 85.2 × (1200 + 0) = 14.5 × 85.2 = 1235.4; 1235.4 × 1200 = **1,482,480**. The unit is the optimizer's abstract cost, not milliseconds, so the number is only for ranking against other rows — but a value near 1.5 million against siblings in the hundreds marks this row as the top candidate.

Step 2 — build the index from the column lists, equality first, then inequality, then `INCLUDE`:

```sql
CREATE NONCLUSTERED INDEX IX_Orders_CustomerId_OrderDate
ON Sales.Orders (CustomerId, OrderDate)
INCLUDE (TotalAmount);
```

Step 3 — verify after creation. `avg_user_impact` 85.2 predicts the query cost falls by about 85%, so a query that cost 14.5 should cost roughly 14.5 × (1 − 0.852) = 14.5 × 0.148 = **2.15** afterwards. Check the actual plan and timings; if the real improvement is far from 85%, the DMV estimate was optimistic and the next suggestion deserves the attention instead.

## Warnings: Do Not Blindly Create Every Suggestion

- The DMVs suggest indexes per query, not per workload. Ten similar queries produce ten near-identical suggestions; creating all ten wastes space and slows every `INSERT`, `UPDATE`, and `DELETE` on the table. Consolidate overlapping suggestions into one index.
- Every new index is a write tax: each write to the table must also write the index. A table with heavy writes and a rarely-read suggestion is a net loss.
- The DMVs never suggest dropping a useless index, and they never consider existing indexes that almost cover the query — check `sys.dm_db_index_usage_stats` first; an existing index may need one added column instead of a whole new index.
- Suggestions are based on the plan cache at the moment of the query. A suggestion for a query that ran once during a one-off report is not production evidence.
- Test in a non-production environment first, and measure before/after with the actual query, not with the DMV estimate alone.

## Key Takeaways

- Join the three missing-index DMVs on `index_handle` and rank by `avg_total_user_cost × avg_user_impact × (user_seeks + user_scans)`.
- Equality columns become leading key columns, inequality columns follow them, and `included_columns` become `INCLUDE` columns.
- In the worked example, 1200 seeks at 85.2% impact produce an estimated benefit of **1,482,480** cost units, and the created index should cut the query cost from 14.5 to about **2.15**.
- DMV counters reset on restart, and `avg_user_impact` is an optimizer estimate — verify with real plans and timings.
- Consolidate overlapping suggestions, check existing indexes first, and never create every suggestion: each index taxes every write on the table.
