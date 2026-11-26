# SQL Server Cursors

A cursor lets you process rows one at a time in T-SQL, fetching each row
into variables and running procedural logic on it. Cursors exist because
some tasks are genuinely row-by-row, but most cursor code is a slow way of
doing something a single set-based statement could do in one pass.

## Declaring and Using a Cursor

The basic pattern is `DECLARE`, `OPEN`, `FETCH NEXT` in a loop, then
`CLOSE` and `DEALLOCATE`:

```sql
DECLARE @CustomerId INT;
DECLARE @Name NVARCHAR(100);

DECLARE CustomerCursor CURSOR FOR
SELECT CustomerId, Name
FROM Customers
WHERE IsActive = 1
ORDER BY Name;

OPEN CustomerCursor;

FETCH NEXT FROM CustomerCursor INTO @CustomerId, @Name;

WHILE @@FETCH_STATUS = 0
BEGIN
    PRINT 'Processing ' + @Name;

    FETCH NEXT FROM CustomerCursor INTO @CustomerId, @Name;
END

CLOSE CustomerCursor;
DEALLOCATE CustomerCursor;
```

`@@FETCH_STATUS` is `0` while a row was returned, `-1` when the fetch went
past the last row, and `-2` when a row that was requested no longer exists.
The loop ends when the status is no longer `0`.

## Cursor Types

The type decides what the cursor sees and how much work the server does:

- **Forward-only** (the default): rows are fetched once, in order, and the
  cursor cannot move backwards. This is the cheapest type.
- **Static**: SQL Server copies the result set into `tempdb` when the cursor
  opens. Later changes by other sessions are invisible, and the data cannot
  change under you, but the copy costs memory and disk.
- **Keyset**: the set of keys is fixed at open time, but the row values are
  re-read on each fetch, so updates by other sessions are visible while
  inserts are not. Deleted rows appear as `@@FETCH_STATUS = -2`.
- **Dynamic**: every fetch re-reads the data, so inserts, updates, and
  deletes by other sessions are all visible. This is the most expensive and
  the least predictable type.

Add `LOCAL` so the cursor is scoped to the batch and cleaned up
automatically, and `FAST_FORWARD` for a read-only forward-only cursor that
the optimizer can run efficiently:

```sql
DECLARE CustomerCursor CURSOR LOCAL FAST_FORWARD FOR
SELECT CustomerId, Name FROM Customers WHERE IsActive = 1;
```

## Why Cursors Are Usually Slow

A cursor turns one set operation into N separate operations: one fetch, one
round of logic, and often one `UPDATE` or `INSERT` per row. Each step has
overhead, and the server cannot parallelize the work or optimize across
rows. A cursor over 100,000 rows does 100,000 fetches, while a set-based
statement scans the same data once.

Cursors also hold locks longer. A cursor that updates rows as it goes keeps
its transaction open for the whole loop, which increases blocking and makes
lock escalation more likely. The `tempdb` copy for a static cursor adds I/O
on top of that.

## A Set-Based Alternative

Most cursor logic is a per-row calculation or update that a single statement
expresses directly. Compare the two versions of the same task:

```sql
-- Cursor version: one UPDATE per customer
DECLARE @CustomerId INT;
DECLARE CustomerCursor CURSOR LOCAL FAST_FORWARD FOR
SELECT CustomerId FROM Customers WHERE IsActive = 1;

OPEN CustomerCursor;
FETCH NEXT FROM CustomerCursor INTO @CustomerId;

WHILE @@FETCH_STATUS = 0
BEGIN
    UPDATE Customers
    SET LoyaltyPoints = LoyaltyPoints + 10
    WHERE CustomerId = @CustomerId;

    FETCH NEXT FROM CustomerCursor INTO @CustomerId;
END

CLOSE CustomerCursor;
DEALLOCATE CustomerCursor;

-- Set-based version: one statement, same result
UPDATE Customers
SET LoyaltyPoints = LoyaltyPoints + 10
WHERE IsActive = 1;
```

The set-based version is shorter, runs in one pass, and lets the optimizer
pick the best plan. If the per-row logic is a calculation, a `CASE`
expression or a join to a lookup table usually replaces the loop.

## Legitimate Uses

Cursors are not always wrong. They are the right tool when:

- Each row needs a call to a stored procedure or external process that
  cannot run set-based, such as sending one email per customer.
- The logic for a row depends on the result of the previous row in a way
  that window functions cannot express.
- You are running a maintenance task, such as rebuilding indexes database by
  database, where the work is inherently one object at a time.
- You are calling `sp_executesql` with a different statement per row.

Even then, keep the cursor `LOCAL FAST_FORWARD`, keep the transaction short,
and process in small batches where possible.

## Deallocation

Always close and deallocate a cursor when you are done. `CLOSE` releases
the result set but keeps the cursor definition; `DEALLOCATE` removes the
cursor entirely and frees its resources. A global cursor that is never
deallocated holds its locks and `tempdb` space until the session ends.
Declaring the cursor `LOCAL` avoids this class of leak, because the cursor
is removed automatically when the batch finishes.

## Key Takeaways

- The cursor pattern is `DECLARE`, `OPEN`, `FETCH NEXT` in a `WHILE @@FETCH_STATUS = 0` loop, then `CLOSE` and `DEALLOCATE`.
- Forward-only is cheapest; static copies to `tempdb`; keyset fixes the keys; dynamic re-reads everything.
- Cursors are slow because they turn one set operation into N per-row operations with no parallelism.
- Replace cursor loops with a single set-based `UPDATE`, `CASE`, or join wherever the logic allows it.
- Use a cursor only for genuinely sequential work, keep it `LOCAL FAST_FORWARD`, and always deallocate it.
