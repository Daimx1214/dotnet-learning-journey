# CROSS APPLY and OUTER APPLY

`CROSS APPLY` and `OUTER APPLY` are SQL Server's lateral joins: they run a table expression once per row of the left side, and that expression can reference columns from the current left row. They are the way to call a table-valued function per row and the cleanest way to write a TOP N per group query.

## The Idea: A Subquery That Sees the Outer Row

A normal derived table cannot reference the outer query. An `APPLY` can:

```sql
SELECT c.CustomerName, o.OrderId, o.OrderDate
FROM Customers c
CROSS APPLY (
    SELECT TOP 3 OrderId, OrderDate
    FROM Orders
    WHERE Orders.CustomerId = c.CustomerId
    ORDER BY OrderDate DESC
) o;
```

For each customer, the subquery runs with that customer's id and returns their three most recent orders. There is no way to write this as a plain join, because the `TOP 3` depends on the current row.

## CROSS APPLY vs OUTER APPLY

The difference is what happens when the right side returns no rows:

- `CROSS APPLY` behaves like an inner join: rows with an empty right side disappear.
- `OUTER APPLY` behaves like a left join: rows with an empty right side survive, with NULLs on the right.

```sql
-- Customers with no orders are dropped.
SELECT c.CustomerName, o.OrderId
FROM Customers c
CROSS APPLY (
    SELECT TOP 1 OrderId FROM Orders
    WHERE Orders.CustomerId = c.CustomerId
    ORDER BY OrderDate DESC
) o;

-- Customers with no orders appear with a NULL OrderId.
SELECT c.CustomerName, o.OrderId
FROM Customers c
OUTER APPLY (
    SELECT TOP 1 OrderId FROM Orders
    WHERE Orders.CustomerId = c.CustomerId
    ORDER BY OrderDate DESC
) o;
```

Pick `CROSS APPLY` when the right side must exist for the row to matter, and `OUTER APPLY` when you want every left row regardless.

## Table-Valued Functions Per Row

The original purpose of `APPLY` is calling a table-valued function with a value from each row:

```sql
-- A TVF that splits a delimited string into rows.
SELECT p.ProductName, s.Value AS Tag
FROM Products p
CROSS APPLY STRING_SPLIT(p.Tags, ',') s;
```

Without `APPLY` you would need a join on a function call that cannot take the row's column as an argument. Inline table-valued functions are the best fit here; multi-statement TVFs force a less efficient plan, so prefer inline ones when performance matters.

## TOP N Per Group

The most common `APPLY` pattern in reporting is TOP N per group. It replaces a correlated subquery with `ROW_NUMBER()` in many cases, and it is often easier to read:

```sql
-- The latest order per customer, plus its total.
SELECT c.CustomerName, latest.OrderDate, latest.Total
FROM Customers c
CROSS APPLY (
    SELECT TOP 1 OrderDate, Total
    FROM Orders
    WHERE Orders.CustomerId = c.CustomerId
    ORDER BY OrderDate DESC
) latest;
```

The equivalent with `ROW_NUMBER()` needs a derived table and a filter on the row number, which is more code for the same result. `APPLY` also shines when the per-group logic is not just ordering: the subquery can aggregate, filter, and join freely.

## APPLY vs Correlated Subquery

A correlated subquery in the `SELECT` list returns one scalar per row:

```sql
SELECT
    c.CustomerName,
    (SELECT MAX(OrderDate) FROM Orders WHERE CustomerId = c.CustomerId) AS LastOrderDate
FROM Customers c;
```

`APPLY` can return multiple columns and multiple rows, which a scalar subquery cannot. When you need one value, the scalar subquery is fine. When you need several columns from the same per-row computation, `APPLY` computes them once instead of repeating a subquery per column.

## Performance Notes

SQL Server usually implements `APPLY` as a nested loop: one execution of the right side per left row. That is fast when the left side is small and the right side is indexed on the correlated column. If the left side is large, the repeated executions add up, and a window-function rewrite may be faster. Always check the execution plan: look for an index seek inside the `APPLY` branch, which means each per-row call is cheap.

## Key Takeaways

- `APPLY` runs a table expression once per left row, and that expression can reference the current row's columns.
- `CROSS APPLY` drops left rows with no matching right rows, like an inner join; `OUTER APPLY` keeps them with NULLs, like a left join.
- The TOP N per group pattern is the signature use case: `TOP 3` orders per customer in one readable query.
- `APPLY` is the only clean way to call a table-valued function with a per-row argument.
- It can return multiple columns and rows where a correlated scalar subquery returns one value; check the plan for an index seek inside the `APPLY` branch.
