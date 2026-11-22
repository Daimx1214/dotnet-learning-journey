# SQL Server Wait Statistics

When SQL Server is slow, the question is not "is the CPU busy?" but "what is
it *waiting* for?" **Wait statistics** answer that: every time a session
cannot proceed, SQL Server records what it waited on and for how long.
Reading waits tells you whether the bottleneck is CPU, disk, locks, or the
application itself.

## sys.dm_os_wait_stats

The DMV aggregates waits since the last server restart (or the last manual
reset):

```sql
SELECT TOP 15
    wait_type,
    wait_time_ms,
    waiting_tasks_count,
    wait_time_ms * 1.0 / NULLIF(waiting_tasks_count, 0) AS avg_wait_ms,
    signal_wait_time_ms,
    wait_time_ms - signal_wait_time_ms AS resource_wait_ms
FROM sys.dm_os_wait_stats
WHERE wait_time_ms > 0
ORDER BY wait_time_ms DESC;
```

Useful columns: `wait_time_ms` is total time waited; `signal_wait_time_ms`
is time spent waiting for a CPU after the resource became available (a high
signal wait means CPU pressure); the difference is time actually waiting on
the resource. Ignore waits with huge counts but tiny averages — they are
usually benign background noise.

## Common Waits and What They Mean

| Wait type | Means | Typical cause / fix |
|-----------|-------|---------------------|
| `CXPACKET` | A parallel query waits for its threads to finish | Skewed parallelism; check `MAXDOP` and cost threshold, look for a bad plan |
| `CXCONSUMER` | The consumer thread of a parallel query waits | Same as CXPACKET in modern versions — the two usually appear together |
| `PAGEIOLATCH_SH` / `PAGEIOLATCH_EX` | Waiting to read a data page from disk into memory | Slow storage or a missing index forcing large scans; check buffer cache hit ratio |
| `LCK_M_*` (e.g. `LCK_M_X`) | Waiting to acquire a lock | Blocking — find the blocking session and the long transaction holding the lock |
| `SOS_SCHEDULER_YIELD` | A task voluntarily yielded the CPU | CPU pressure; many runnable tasks competing for few schedulers |
| `ASYNC_NETWORK_IO` | SQL Server has results ready but the client is not consuming them | Slow client, row-by-row fetching, or an app that reads results lazily |
| `WRITELOG` | Waiting for the transaction log to be flushed to disk | Slow log disk or too many small transactions; commit less often per batch |
| `PAGELATCH_EX` | Contention on a page in memory (often tempdb allocation pages) | Tempdb contention; add tempdb files or fix allocation patterns |
| `RESOURCE_SEMAPHORE` | Waiting for query-execution memory | Memory pressure; large sorts/hashes, check memory grants |

A single wait dominating is a strong signal. Several waits of similar size
usually means the problem is elsewhere — a bad plan can show up as
`CXPACKET` *and* `PAGEIOLATCH_SH` at once.

## Reading Waits Without Guessing

Rules that keep the diagnosis honest:

1. **Sample, don't snapshot.** Take two readings an hour apart (or during
   the slow period) and look at the *delta* — cumulative totals since
   startup hide what is happening now.
2. **Correlate.** A high `LCK_M_X` is meaningless until you find the blocker
   with `sys.dm_exec_requests` (`blocking_session_id`) and see what it is
   doing.
3. **Check averages, not just totals.** A wait with 10 million occurrences
   and a 1 ms average is background chatter; a wait with 100 occurrences
   and a 5 second average is the problem.
4. **Mind benign waits.** `XE_TIMER_EVENT`, `BROKER_*`, `SLEEP_TASK`,
   `LAZYWRITER_SLEEP` and similar idle waits are normal and should be
   filtered out of the top list.

```sql
-- Find the blocking chain right now
SELECT session_id, blocking_session_id, wait_type,
       wait_time, status, command
FROM sys.dm_exec_requests
WHERE blocking_session_id <> 0;
```

## Resetting the Counters

Wait stats accumulate since startup, so after a fix you may want a clean
baseline:

```sql
DBCC SQLPERF ('sys.dm_os_wait_stats', CLEAR);
```

This resets the DMV only — it does not touch data or hurt anything — but it
destroys history, so record the current snapshot first. Never clear it on a
server you do not own without noting the before-values.

## Key Takeaways

- Wait statistics show *what* SQL Server is waiting on; `sys.dm_os_wait_stats`
  aggregates them since startup or the last reset.
- `CXPACKET` points at parallelism, `PAGEIOLATCH` at disk/index problems,
  `LCK_M_*` at blocking, `SOS_SCHEDULER_YIELD` at CPU pressure, and
  `ASYNC_NETWORK_IO` at a slow client — not at the server.
- Compare deltas over time, check average waits, and correlate with the
  blocking chain before acting.
- Filter out benign idle waits, and use `DBCC SQLPERF (... , CLEAR)` only
  after saving the current snapshot.
