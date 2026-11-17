# Correlated Subqueries (SQL)

A subquery is *correlated* when it references a column from the outer query, so it cannot run on its own — it must be re-evaluated for every row the outer query considers. A *non-correlated* subquery runs once, independently, and its result is reused. The distinction matters because it decides whether the database does one lookup or one lookup per row, and because some problems (like "above the department average") can only be expressed with a correlated subquery.

## Correlated vs Non-Correlated

A non-correlated subquery is self-contained. This one runs once and returns a single number:

```sql
-- Employees earning more than the company-wide average salary.
SELECT EmployeeId, Name, Salary
FROM Employees
WHERE Salary > (SELECT AVG(Salary) FROM Employees);
```

A correlated subquery references the outer row — here `e.DepartmentId` comes from the outer `Employees e`, so the inner query changes for each employee:

```sql
-- Employees earning more than their OWN department's average salary.
SELECT e.EmployeeId, e.Name, e.Salary, e.DepartmentId
FROM Employees e
WHERE e.Salary > (
    SELECT AVG(e2.Salary)
    FROM Employees e2
    WHERE e2.DepartmentId = e.DepartmentId
);
```

The alias is not optional decoration: without `e` and `e2` the query would be ambiguous (or would silently resolve `DepartmentId` to the wrong table). Always alias both sides of a correlated subquery.

## EXISTS vs IN

`EXISTS` is almost always written with a correlated subquery: it returns true as soon as the inner query finds one matching row, and it never returns a value to compare. `IN` compares a value against a list, and is usually paired with a non-correlated subquery.

```sql
-- Departments that have at least one employee earning over 100,000.
SELECT d.DepartmentId, d.Name
FROM Departments d
WHERE EXISTS (
    SELECT 1 FROM Employees e
    WHERE e.DepartmentId = d.DepartmentId AND e.Salary > 100000
);

-- The same question with IN (non-correlated inner query).
SELECT DepartmentId, Name
FROM Departments
WHERE DepartmentId IN (
    SELECT DepartmentId FROM Employees WHERE Salary > 100000
);
```

Two differences decide which to use. First, `NOT IN` is a trap: if the subquery returns even one `NULL`, `NOT IN` returns no rows at all, because `x <> NULL` is unknown rather than true. `NOT EXISTS` has no such problem and is the safe choice for "rows with no match" questions. Second, `EXISTS` can stop at the first match while `IN` must materialize the whole list, which is why `EXISTS` usually wins when the inner result is large.

## The Row-by-Row Cost

Conceptually, a correlated subquery runs once per outer row: for 10,000 employees, the department-average subquery runs 10,000 times. Modern optimizers usually avoid the naive loop — SQL Server typically rewrites a correlated aggregate into a join against a grouped subquery, or uses an index seek per row when an index on `DepartmentId` exists. But the rewrite is not guaranteed: complex correlation (a subquery in the `SELECT` list, or correlation on a non-indexed column) can fall back to genuinely per-row execution. The rule of thumb is to read the execution plan — if you see a nested loop with one seek per outer row, the correlation is costing you per-row work, and the fix is either an index on the correlated column or a manual rewrite.

## Rewriting with Joins

Most correlated subqueries have an equivalent join form that is easier for the optimizer and often easier to read. The department-average example becomes a grouped derived table joined back to the employees:

```sql
SELECT e.EmployeeId, e.Name, e.Salary
FROM Employees e
INNER JOIN (
    SELECT DepartmentId, AVG(Salary) AS AvgSalary
    FROM Employees
    GROUP BY DepartmentId
) dept ON dept.DepartmentId = e.DepartmentId
WHERE e.Salary > dept.AvgSalary;
```

This computes each department average exactly once. Prefer the join form for aggregates-over-groups and for `EXISTS` checks that feed further joins. Keep the correlated form when the join equivalent is awkward — notably `EXISTS` / `NOT EXISTS` anti-joins, where the correlated version states the intent ("has no matching row") more directly than a `LEFT JOIN ... WHERE ... IS NULL`.

## Key Takeaways

- A correlated subquery references the outer query's columns and is evaluated per outer row; a non-correlated subquery runs once.
- `EXISTS` pairs with correlated subqueries and short-circuits at the first match; `IN` compares against a materialized list.
- `NOT IN` returns nothing if the subquery contains a `NULL` — use `NOT EXISTS` for "no match" questions.
- Correlated aggregates can often be rewritten as a grouped derived table joined back, computing each group once instead of once per row.
- Always alias both sides of a correlated subquery, and check the execution plan — an index on the correlated column turns per-row work into cheap seeks.
- Keep the correlated form when it states intent best, especially `EXISTS` / `NOT EXISTS` anti-joins.
