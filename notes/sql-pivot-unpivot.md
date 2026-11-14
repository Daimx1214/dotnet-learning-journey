# PIVOT and UNPIVOT in SQL Server (SQL)

Relational tables store data in rows, but reports often want it in columns — one column per month, per region, per category. `PIVOT` rotates rows into columns; `UNPIVOT` does the reverse, turning columns back into rows. Both are pure presentation transforms: they do not change the stored data.

## The Sample Data

```sql
CREATE TABLE Sales (
    Region    NVARCHAR(20) NOT NULL,
    Quarter   NVARCHAR(10) NOT NULL,
    Amount    DECIMAL(10, 2) NOT NULL
);

INSERT INTO Sales (Region, Quarter, Amount) VALUES
    ('North', 'Q1', 1000.00), ('North', 'Q2', 1500.00),
    ('North', 'Q3', 1200.00), ('North', 'Q4', 1800.00),
    ('South', 'Q1',  800.00), ('South', 'Q2',  900.00),
    ('South', 'Q3', 1100.00), ('South', 'Q4', 1300.00);
```

## PIVOT: Rows into Columns

`PIVOT` needs three things: an aggregation (usually `SUM`), the column whose values become the new column headers, and the fixed list of those values.

```sql
SELECT Region, Q1, Q2, Q3, Q4
FROM (
    SELECT Region, Quarter, Amount FROM Sales
) AS Source
PIVOT (
    SUM(Amount)
    FOR Quarter IN (Q1, Q2, Q3, Q4)
) AS Pivoted;
```

Result:

| Region | Q1 | Q2 | Q3 | Q4 |
|--------|------|------|------|------|
| North | 1000.00 | 1500.00 | 1200.00 | 1800.00 |
| South | 800.00 | 900.00 | 1100.00 | 1300.00 |

The inner query supplies exactly three columns: the grouping column (`Region`), the spreading column (`Quarter`), and the value (`Amount`). Any extra column in the inner query silently becomes part of the grouping, which is the most common `PIVOT` bug — if the result has more rows than expected, an unneeded column leaked into the source query.

## UNPIVOT: Columns Back into Rows

`UNPIVOT` normalizes a wide table back into tall form — useful when a spreadsheet-shaped table needs to join or aggregate properly.

```sql
CREATE TABLE QuarterlySales (
    Region NVARCHAR(20) NOT NULL,
    Q1 DECIMAL(10, 2) NOT NULL, Q2 DECIMAL(10, 2) NOT NULL,
    Q3 DECIMAL(10, 2) NOT NULL, Q4 DECIMAL(10, 2) NOT NULL
);

INSERT INTO QuarterlySales VALUES
    ('North', 1000.00, 1500.00, 1200.00, 1800.00),
    ('South',  800.00,  900.00, 1100.00, 1300.00);

SELECT Region, Quarter, Amount
FROM QuarterlySales
UNPIVOT (
    Amount FOR Quarter IN (Q1, Q2, Q3, Q4)
) AS Unpivoted;
```

This returns the original eight rows. Note that `UNPIVOT` drops NULLs: a quarter with a NULL amount produces no row at all, which can silently lose data if NULLs are meaningful.

## Limitations

The column list in `PIVOT` is fixed at query-writing time — there is no `PIVOT ... FOR Quarter IN (SELECT DISTINCT ...)` syntax. When the set of values is not known in advance (new products, new months), you must build the query as dynamic SQL: query the distinct values, build the `IN (...)` list as a string, and run it with `sp_executesql`. Dynamic SQL adds injection risk, so whitelist the values from the database rather than concatenating user input. For ad-hoc analysis, conditional aggregation with `CASE` is often clearer than `PIVOT`:

```sql
SELECT Region,
    SUM(CASE WHEN Quarter = 'Q1' THEN Amount ELSE 0 END) AS Q1,
    SUM(CASE WHEN Quarter = 'Q2' THEN Amount ELSE 0 END) AS Q2
FROM Sales
GROUP BY Region;
```

## Key takeaways

- `PIVOT` rotates rows into columns with an aggregate and a fixed `IN (...)` value list; keep the source query to exactly three columns.
- `UNPIVOT` turns columns back into rows, but drops NULL values.
- The pivot column list is static — dynamic values need dynamic SQL built from whitelisted database values.
- For a small fixed set of columns, `CASE` + `GROUP BY` is often simpler and easier to read than `PIVOT`.
