# SQL Set Operations (SQL)

Set operations combine the result sets of two or more `SELECT` queries into one: `UNION`, `UNION ALL`, `INTERSECT`, and `EXCEPT`. They treat query results as sets of rows, and they are the right tool whenever the question is about overlap or difference between two groups — customers who also ordered, products never sold, and so on. Each operator answers a different set question, and mixing them up silently changes the result.

## UNION vs UNION ALL

`UNION` returns the distinct rows from both queries; `UNION ALL` returns every row, duplicates included. The difference is both semantic and performance: `UNION` must sort or hash the combined rows to remove duplicates, while `UNION ALL` just concatenates.

```sql
-- Every city that has a customer OR a supplier (each city listed once).
SELECT City FROM Customers WHERE Country = 'Germany'
UNION
SELECT City FROM Suppliers WHERE Country = 'Germany';

-- Every city occurrence: a city with 3 customers and 1 supplier appears 4 times.
SELECT City FROM Customers WHERE Country = 'Germany'
UNION ALL
SELECT City FROM Suppliers WHERE Country = 'Germany';
```

Default to `UNION ALL` unless you actually need deduplication. A common mistake is writing `UNION` out of habit and paying for a sort the query never needed — or worse, silently dropping legitimate duplicate rows (for example, counting order lines) that `UNION ALL` would have kept.

## INTERSECT and EXCEPT

`INTERSECT` returns only the rows present in both queries; `EXCEPT` returns rows present in the first query but not in the second. Both deduplicate their results.

```sql
-- Cities that have BOTH a customer and a supplier.
SELECT City FROM Customers WHERE Country = 'Germany'
INTERSECT
SELECT City FROM Suppliers WHERE Country = 'Germany';

-- Cities with a customer but NO supplier.
SELECT City FROM Customers WHERE Country = 'Germany'
EXCEPT
SELECT City FROM Suppliers WHERE Country = 'Germany';
```

`EXCEPT` is order-sensitive — swapping the two queries gives a different answer — while `INTERSECT` is symmetric. A practical use: `EXCEPT` is a quick way to diff two tables (or a table before and after a migration) by comparing all columns. Both operators are often clearer than the equivalent `EXISTS` / `NOT EXISTS` formulation, and the query optimizer frequently turns them into the same plan.

## Column Count and Type Rules

Every query in a set operation must return the same number of columns, and the columns must be type-compatible position by position — column names do not have to match, only positions matter. The result takes its column names from the *first* query. Type compatibility follows the usual precedence rules: `INT` and `BIGINT` combine fine, but `INT` and `VARCHAR` do not, so cast explicitly when mixing sources.

```sql
-- Two columns in both branches; names come from the first branch.
SELECT CustomerId AS Id, CompanyName AS Name FROM Customers
UNION ALL
SELECT SupplierId, CompanyName FROM Suppliers;
```

`NULL` values are considered equal to each other for deduplication purposes in `UNION`, `INTERSECT`, and `EXCEPT` — unlike in a `WHERE` clause, where `NULL = NULL` is unknown. Watch out for implicit conversions too: combining a `DECIMAL(10,2)` column with a `FLOAT` column silently promotes the result to `FLOAT`, which can lose precision.

## ORDER BY Placement

`ORDER BY` applies to the combined result, so it goes only at the very end of the whole statement — one `ORDER BY`, after the last query. It sorts by the output column names (from the first query) or by ordinal position. Putting `ORDER BY` inside an earlier branch is a syntax error unless that branch is wrapped in parentheses as a subquery, and even then it only affects that branch's ordering before the set operation, not the final output.

```sql
SELECT City FROM Customers WHERE Country = 'Germany'
UNION
SELECT City FROM Suppliers WHERE Country = 'Germany'
ORDER BY City;   -- Sorts the combined, deduplicated result.
```

To sort by something that is not in the output, add it as an extra column in each branch and sort by its position — or wrap the whole set operation in a subquery and order the outer query.

## Key Takeaways

- `UNION` deduplicates, `UNION ALL` keeps every row — prefer `UNION ALL` unless deduplication is actually required.
- `INTERSECT` returns rows in both queries; `EXCEPT` returns rows in the first but not the second, and it is order-sensitive.
- All branches must return the same number of type-compatible columns; result column names come from the first query.
- `NULL`s count as equal to each other for deduplication in set operations, unlike in `WHERE` comparisons.
- `ORDER BY` goes once, at the end, and sorts the combined result — not inside individual branches.
- Set operations are often the clearest way to express overlap/difference questions and to diff two tables with `EXCEPT`.
