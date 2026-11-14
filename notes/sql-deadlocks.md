# Deadlocks in SQL Server (SQL)

A deadlock is a standoff: transaction A holds a lock on row 1 and waits for row 2, while transaction B holds a lock on row 2 and waits for row 1. Neither can proceed, and neither will ever give up on its own. SQL Server detects this cycle in the wait-for graph, picks one transaction as the victim, kills it, and returns error 1205 so the other can finish.

## How a Deadlock Forms

```sql
-- Session 1
BEGIN TRANSACTION;
UPDATE Accounts SET Balance = Balance - 100 WHERE Id = 1; -- locks row 1
WAITFOR DELAY '00:00:02';
UPDATE Accounts SET Balance = Balance + 100 WHERE Id = 2; -- waits for row 2
COMMIT;

-- Session 2 (running at the same time, opposite order)
BEGIN TRANSACTION;
UPDATE Accounts SET Balance = Balance - 50 WHERE Id = 2;  -- locks row 2
WAITFOR DELAY '00:00:02';
UPDATE Accounts SET Balance = Balance + 50 WHERE Id = 1;  -- waits for row 1
COMMIT;
```

Each session holds what the other needs. SQL Server's deadlock monitor wakes every few seconds, finds the cycle, and ends one session with:

```
Msg 1205, Level 13, State 51
Transaction (Process ID 57) was deadlocked on lock resources
with another process and has been chosen as the deadlock victim.
Rerun the transaction.
```

The victim's transaction is rolled back entirely; the survivor commits normally. Error 1205 is expected and recoverable — the application should catch it and retry the transaction.

## Reading a Deadlock Graph

SQL Server records deadlocks as deadlock graphs, available through Extended Events (`xml_deadlock_report`) or the `system_health` session. The graph has three parts worth reading:

- **Processes** — each blocked session, with its SPID, the statement it was running, and the locks it already holds.
- **Resources** — the locked objects (a key, a page, a table) each process is waiting on.
- **Victim** — the process SQL Server chose to kill, usually the one whose rollback is cheapest (fewest log records to undo).

Read the graph from the victim backwards: the victim's waiting resource is held by the other process, whose waiting resource is held by the victim. That loop is the cycle. The statements in the graph show exactly which two operations collided, which is what you need to fix the code.

```sql
-- Pull recent deadlock graphs from the system_health session.
SELECT CAST(xet.target_data AS XML) AS DeadlockGraph
FROM sys.dm_xe_session_targets AS xet
JOIN sys.dm_xe_sessions AS xe
    ON xe.address = xet.event_session_address
WHERE xe.name = 'system_health';
```

## Prevention

Most deadlocks are a code problem, not a server problem:

- **Always access tables and rows in the same order.** If every transaction updates `Accounts` row 1 before row 2, the cycle above cannot form. This single rule prevents most deadlocks.
- **Keep transactions short.** A transaction that does two updates and commits immediately holds locks briefly; one that calls an API or waits for user input mid-transaction holds them for seconds.
- **Use snapshot isolation** (`READ_COMMITTED_SNAPSHOT` or `SNAPSHOT`) for read-heavy workloads. Readers then use row versions instead of shared locks, so readers and writers stop blocking each other — removing the most common deadlock pattern entirely.
- **Avoid unnecessary lock escalation.** Updating a whole table when only a few rows need it can escalate row locks to a table lock, which then blocks everything.
- **Retry on error 1205.** Even perfect code can deadlock under load. A small retry loop (two or three attempts with a short backoff) turns a fatal error into a hiccup.

```csharp
// Retry a transaction that hit a deadlock victim error.
for (int attempt = 1; attempt <= 3; attempt++)
{
    try
    {
        await TransferAsync(1, 2, 100m);
        break;
    }
    catch (SqlException ex) when (ex.Number == 1205 && attempt < 3)
    {
        await Task.Delay(100 * attempt);
    }
}
```

`SET DEADLOCK_PRIORITY LOW` can mark a background or reporting transaction as the preferred victim, so an important transaction survives when a deadlock does occur — use it on the expendable side, never as a substitute for fixing lock order.

## Key takeaways

- A deadlock is a cycle in the wait-for graph; SQL Server kills one victim with error 1205 and lets the other finish.
- Read the deadlock graph: the two statements and the two resources in the cycle show exactly what collided.
- Prevent deadlocks by accessing rows in a consistent order, keeping transactions short, and using snapshot isolation for readers.
- Always retry error 1205 — it is a normal, recoverable event under concurrency.
