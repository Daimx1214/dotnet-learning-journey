# Recursive CTEs in SQL Server

A recursive common table expression (CTE) walks a hierarchy stored in a table — an org chart, a category tree, a bill of materials — one level at a time, using only SQL.

## Anchor + Recursive Member

A recursive CTE has two parts joined by `UNION ALL`: the anchor member (the starting rows) and the recursive member (which joins back to the CTE itself to find the next level down).

```sql
WITH OrgChart AS (
    -- Anchor: the root of the hierarchy
    SELECT EmployeeId, ManagerId, Name, 0 AS Level
    FROM Employees
    WHERE ManagerId IS NULL

    UNION ALL

    -- Recursive member: employees whose manager is already in the CTE
    SELECT e.EmployeeId, e.ManagerId, e.Name, o.Level + 1
    FROM Employees e
    INNER JOIN OrgChart o ON e.ManagerId = o.EmployeeId
)
SELECT EmployeeId, Name, Level
FROM OrgChart
ORDER BY Level, Name;
```

SQL Server runs the anchor once, then repeats the recursive member until it returns no new rows. The `Level` column is computed by hand — it is just `Level + 1` at each step, so it is always correct as long as the anchor starts at 0.

## Employee Org-Chart Example

Given this data:

| EmployeeId | Name   | ManagerId |
|------------|--------|-----------|
| 1          | Aisha  | NULL      |
| 2          | Bilal  | 1         |
| 3          | Chen   | 1         |
| 4          | Dina   | 2         |
| 5          | Omar   | 2         |
| 6          | Sara   | 4         |

The query returns:

| EmployeeId | Name  | Level |
|------------|-------|-------|
| 1          | Aisha | 0     |
| 2          | Bilal | 1     |
| 3          | Chen  | 1     |
| 4          | Dina  | 2     |
| 5          | Omar  | 2     |
| 6          | Sara  | 3     |

Aisha is the CEO (level 0), Bilal and Chen report to her (level 1), Dina and Omar report to Bilal (level 2), and Sara reports to Dina (level 3). Verified by counting edges from the root: Sara is three edges below Aisha.

## MAXRECURSION and Cycle Guards

By default SQL Server stops a recursive CTE after 100 levels and raises an error. Raise the limit only when the hierarchy is genuinely deeper:

```sql
SELECT EmployeeId, Name, Level
FROM OrgChart
OPTION (MAXRECURSION 1000);   -- 0 = no limit; use with care
```

A cycle in the data (A manages B, B manages A) makes the recursion run forever until `MAXRECURSION` stops it. Guard against it by carrying the path so far and refusing to revisit a node:

```sql
WITH OrgChart AS (
    SELECT EmployeeId, ManagerId, Name, 0 AS Level,
           CAST('/' + CAST(EmployeeId AS varchar(10)) + '/' AS varchar(1000)) AS Path
    FROM Employees
    WHERE ManagerId IS NULL
    UNION ALL
    SELECT e.EmployeeId, e.ManagerId, e.Name, o.Level + 1,
           o.Path + CAST(e.EmployeeId AS varchar(10)) + '/'
    FROM Employees e
    INNER JOIN OrgChart o ON e.ManagerId = o.EmployeeId
    WHERE o.Path NOT LIKE '%/' + CAST(e.EmployeeId AS varchar(10)) + '/%'
)
SELECT EmployeeId, Name, Level FROM OrgChart;
```

## Key Takeaways

- A recursive CTE is an anchor member plus a recursive member joined by `UNION ALL` (never `UNION`, which would de-duplicate and break the recursion).
- The `Level` column starts at 0 for the root and increments by 1 per edge — CEO 0, direct reports 1, their reports 2.
- The default recursion limit is 100 levels; raise it with `OPTION (MAXRECURSION n)` only when the data really is that deep.
- Cycles in the data cause runaway recursion — carry a path string and skip nodes already in it.
- Recursive CTEs are ideal for org charts, trees, and bills of materials; for very deep or frequently queried hierarchies, consider a persisted path or hierarchyid column instead.
