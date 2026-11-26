# SQL Server Lock Escalation

Lock escalation is SQL Server converting many fine-grained locks (row or page
locks) into a single coarse lock, usually a table lock. It is a memory
management feature, not a bug, but it can turn a well-behaved batch update
into a blocking incident for every other session touching the table.

## Why It Happens

The lock manager keeps every lock in memory. When a single transaction holds
too many locks on one object, keeping them all becomes more expensive than
the concurrency they protect. SQL Server therefore escalates: it releases
the row and page locks and takes one lock on the whole table (or partition).

The threshold is 5,000 locks on a single table or index within one statement
of a transaction. Once a statement crosses that count, escalation is
attempted. The statement does not need to touch 5,000 rows: one row can hold
several locks (row lock, page lock, index key locks), so an update touching
2,000 rows across two indexes can cross the threshold.

## Symptoms

Lock escalation usually shows up as sudden, widespread blocking:

- Many sessions stuck in `LCK_M_X` or `LCK_M_S` waits on the same table.
- A batch job that ran fine for months suddenly blocks the application
  during a larger-than-usual run.
- Blocking chains where one session (the escalated transaction) is the head
  blocker for dozens of others.
- Timeouts and deadlocks that disappear when the batch is smaller.

Because the escalated lock is held until the transaction ends, a long
transaction that escalates early blocks the table for its whole duration.

## How to Monitor It

Check current locks with the dynamic management views:

```sql
SELECT
    request_session_id AS SessionId,
    resource_type AS ResourceType,
    resource_description AS Resource,
    request_mode AS Mode,
    request_status AS Status
FROM sys.dm_tran_locks
WHERE resource_database_id = DB_ID()
ORDER BY request_session_id;
```

A single session holding one `OBJECT` lock in `X` mode is the fingerprint of
escalation. For history, capture the `lock_escalation` event in Extended
Events, which records the object, the lock count, and the statement.

## Mitigation: Batching

The most reliable fix is to keep each transaction below the threshold by
processing rows in small batches:

```sql
DECLARE @BatchSize INT = 1000;

WHILE 1 = 1
BEGIN
    UPDATE TOP (@BatchSize) Orders
    SET Status = 'Archived'
    WHERE Status = 'Closed'
      AND OrderDate < '2025-01-01';

    IF @@ROWCOUNT = 0 BREAK;
END
```

Each `UPDATE` is its own statement, so each stays well under 5,000 locks,
and other sessions get a chance to run between batches. The tradeoff is
total runtime: many small transactions take longer than one big one, but
they do not block the table.

## Mitigation: Index Design

A good index reduces the number of locks a statement needs. If the `WHERE`
clause can seek on an index instead of scanning the table, fewer rows are
locked in the first place. Conversely, every nonclustered index on the table
adds locks for the same update, because each index entry for a changed row
is locked too. Dropping a redundant index can be enough to stay under the
threshold. Keep indexes that serve real queries; remove ones that only add
lock overhead.

## Trace Flags 1211 and 1224

Two trace flags disable escalation, and both come with a warning:

- Trace flag 1211 disables lock escalation entirely, even when memory
  pressure makes it necessary. The lock manager can run out of memory and
  SQL Server may fail statements with out-of-memory errors.
- Trace flag 1224 disables escalation only when memory pressure is absent,
  which is safer but still allows escalation under pressure.

Neither flag is a fix. Use batching and index design first.

## Lock Escalation and Partitioning

On a partitioned table, escalation can target a single partition instead of
the whole table. This is controlled by the `LOCK_ESCALATION` table option:

```sql
ALTER TABLE Orders SET (LOCK_ESCALATION = AUTO);
```

With `AUTO`, SQL Server escalates to the partition lock when the table is
partitioned. The default is `TABLE`. `DISABLE` turns escalation off, with
the same memory risks as trace flag 1211.

## Key Takeaways

- Lock escalation converts many row or page locks into one table lock once a statement holds 5,000 locks on one object.
- The main symptom is sudden widespread blocking with one session as the head blocker.
- Monitor with `sys.dm_tran_locks` for live locks and the `lock_escalation` Extended Events event for history.
- Batch large updates into small transactions to stay under the threshold.
- Prefer batching and index design over trace flags 1211 and 1224, which only hide the problem.
