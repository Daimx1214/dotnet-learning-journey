# SQL Server Join Algorithms

A join is not one operation. SQL Server has three physical join operators,
and the query optimizer picks one per join based on row counts, indexes, and
sort order. Reading an execution plan means recognizing which one was chosen
and asking whether that choice makes sense for the data.

## Nested Loops Join

A nested loops join scans the outer input once and, for each outer row, seeks
into the inner input. It is the only join that works well when the outer side
is small and the inner side has a useful index:

```sql
SELECT o.OrderId, c.Name
FROM Orders o
JOIN Customers c ON c.CustomerId = o.CustomerId
WHERE o.OrderDate >= '2026-11-01';
```

If the filter leaves 50 orders and `Customers` has an index on `CustomerId`,
the plan does 50 index seeks. Cost grows with the outer row count, so a
nested loops join over a million outer rows with a seek per row is usually a
bad plan.

## Hash Join

A hash join builds a hash table from the smaller (build) input, then scans
the larger (probe) input once and looks each row up in the table. It needs no
index and no sorted input, which makes it the optimizer's choice for large,
unsorted inputs:

```sql
SELECT s.ProductId, SUM(s.Quantity) AS TotalSold
FROM Sales s
JOIN Products p ON p.ProductId = s.ProductId
GROUP BY s.ProductId;
```

When neither side is indexed on the join key and both sides are large, a hash
join is usually the fastest option. Its weakness is memory: the hash table
lives in memory, and if it does not fit, SQL Server spills it to `tempdb`
(a hash spill), which shows up as a warning on the operator in the plan.
Hash joins cannot be used for inequality joins, and they block streaming until the build phase finishes.

## Merge Join

A merge join walks two sorted inputs side by side, advancing whichever side
has the smaller key. It needs both inputs sorted on the join key, either
because an index provides the order or because a Sort operator is added:

```sql
SELECT a.AccountId, t.Amount
FROM Accounts a
JOIN Transactions t ON t.AccountId = a.AccountId
ORDER BY a.AccountId;
```

If both tables have a clustered index on `AccountId`, the merge join reads
each input once, in order, with no hash table and no per-row seek. It is the
best choice when both inputs are large and already sorted, and it streams
rows out as it goes. If the inputs are not sorted, the added Sort operator
can cost more than the join saves, so the optimizer only picks merge join
when sorting is cheap or already free.

## How the Optimizer Chooses

The optimizer estimates row counts from statistics and compares the cost of
each algorithm:

| Situation | Likely choice |
|---|---|
| Small outer input, indexed inner input | Nested loops |
| Large inputs, no useful index, no sort order | Hash join |
| Large inputs, both sorted on the join key | Merge join |
| One input is tiny (a few rows) | Nested loops or hash, depending on the other side |

Stale statistics or a bad cardinality estimate can push the optimizer to the
wrong algorithm. A nested loops join chosen because the estimate said 10 rows
when the real count is 1,000,000 is a classic slow-query cause.

## Reading Joins in an Execution Plan

In SQL Server Management Studio, each join operator shows its type, the
estimated and actual row counts, and warnings. Compare estimated vs actual
rows first: a large gap means the statistics misled the optimizer. Look for
a hash spill warning on a hash join, or a Sort operator feeding a merge join
that costs more than the join itself. A nested loops join with a huge number
of executions on its inner side (one per outer row) is the signature of a
missing index on the inner table.

## Influencing the Choice (Last Resort)

Query hints can force an algorithm, but they should be the last resort,
because they freeze a choice that may be wrong as data grows:

```sql
SELECT o.OrderId, c.Name
FROM Orders o
INNER HASH JOIN Customers c ON c.CustomerId = o.CustomerId
WHERE o.OrderDate >= '2026-11-01';

SELECT o.OrderId, c.Name
FROM Orders o
JOIN Customers c ON c.CustomerId = o.CustomerId
WHERE o.OrderDate >= '2026-11-01'
OPTION (HASH JOIN);
```

Prefer fixing the root cause first: add the missing index, update statistics
with `UPDATE STATISTICS`, or rewrite the query so the estimate is accurate.
Use a hint only when you can prove the optimizer is wrong and you re-check
the plan after every significant data change.

## Key Takeaways

- Nested loops is best for a small outer input with an indexed inner input; cost grows per outer row.
- Hash join is best for large unsorted inputs, but a hash table that spills to `tempdb` is a warning sign.
- Merge join is best when both inputs are already sorted on the join key; otherwise the sort may cost more than it saves.
- Compare estimated vs actual row counts in the plan; a big gap usually means stale statistics, not a bad algorithm.
- Fix indexes and statistics first; use join hints only as a last resort and re-check them as data grows.
