# Dynamic SQL in SQL Server

Dynamic SQL builds a SQL statement as a string at runtime and executes it. It is the only way to handle truly dynamic requirements — a variable table name, a user-chosen sort column — but building that string by concatenating user input is one of the easiest ways to get hacked.

## sp_executesql With Parameters (Safe)

`sp_executesql` accepts a parameterized statement, so user values travel as parameters, never as SQL text:

```sql
DECLARE @sql nvarchar(max) = N'
    SELECT OrderId, Total
    FROM Orders
    WHERE CustomerId = @customerId
      AND Total > @minTotal;';

EXEC sp_executesql
    @sql,
    N'@customerId int, @minTotal decimal(10,2)',
    @customerId = 42,
    @minTotal = 100.00;
```

Because `@customerId` and `@minTotal` are typed parameters, SQL Server treats their values as data only. A value like `42 OR 1=1` cannot break out of the parameter — it is simply not a valid `int`, and the query fails instead of returning every row. As a bonus, the parameterized statement can be cached and reused in the plan cache.

## EXEC With String Concatenation (Injection)

The dangerous version pastes the value straight into the SQL text:

```sql
-- DANGEROUS — do not do this.
DECLARE @customerId nvarchar(50) = N'42 OR 1=1';
DECLARE @sql nvarchar(max) = N'SELECT OrderId, Total FROM Orders WHERE CustomerId = ' + @customerId;
EXEC (@sql);
-- Executes: SELECT OrderId, Total FROM Orders WHERE CustomerId = 42 OR 1=1
-- Returns EVERY order, not just customer 42's.
```

Worse, an attacker can inject a second statement. If `@customerId` is `42; DROP TABLE Orders; --`, the executed batch drops the table. This is SQL injection: user input is executed as code.

```sql
-- What the attacker makes the server run:
SELECT OrderId, Total FROM Orders WHERE CustomerId = 42; DROP TABLE Orders; -- 
```

## Parameter Sniffing Note

`sp_executesql` plans are cached per parameter signature, and SQL Server "sniffs" the first parameter values it sees to build the plan. If the first call filters on a rare value and later calls filter on a very common one, the cached plan may be wrong for them. If you see that pattern — one plan fast for some values and slow for others — consider `OPTION (RECOMPILE)` on the statement, or `OPTIMIZE FOR UNKNOWN`, rather than abandoning parameters.

## Key Takeaways

- Use `sp_executesql` with typed parameters for dynamic SQL — values stay data, never code.
- Never concatenate user input into a SQL string and run it with `EXEC(...)`; that is SQL injection, and it can read or destroy data.
- Parameterized dynamic SQL also gets plan-cache reuse, so it is usually faster than rebuilding a string every time.
- Watch for parameter sniffing: a plan built for one value may be poor for another — `OPTION (RECOMPILE)` is the usual escape hatch.
- If only a table or column name is dynamic, validate it against a whitelist (e.g. `QUOTENAME` plus a check against `sys.columns`) — identifiers cannot be parameterized.
