# SQL Server Change Data Capture (CDC)

Change Data Capture records row-level inserts, updates, and deletes in a SQL Server table into separate change tables, so a consumer can read exactly what changed since a given point without triggers or polling the base table.

## Enabling CDC

CDC is enabled at two levels: the database, then each tracked table. Enabling it requires `sysadmin` rights and creates the `cdc` schema plus a capture job.

```sql
-- 1. Enable CDC on the database
USE SalesDB;
GO
EXEC sys.sp_cdc_enable_db;
GO

-- 2. Enable CDC on one table (capture instance defaults to schema_table)
EXEC sys.sp_cdc_enable_table
    @source_schema = N'dbo',
    @source_name   = N'Orders',
    @role_name     = NULL,          -- NULL = no gating role
    @supports_net_changes = 1;     -- also track net changes per row
GO
```

Enabling a table creates `cdc.dbo_Orders_CT` (the change table) and starts the capture job that reads the transaction log asynchronously. The base table itself is not modified; a `__$start_lsn` column and the operation columns live only in the change table.

## Capture Instances and Change Tables

One table can have up to two capture instances (for example, when the schema changes). Each instance has its own change table and its own LSN watermark.

```sql
-- List capture instances and their change tables
SELECT capture_instance, source_object_id, start_lsn, end_lsn
FROM cdc.change_tables;

-- Column layout of a change table (dbo_Orders_CT)
-- __$start_lsn   binary(10)  LSN of the committing transaction
-- __$end_lsn     binary(10)  LSN up to which the change is valid
-- __$seqval      binary(10)  ordering within the transaction
-- __$operation   int         1 = delete, 2 = insert, 3 = update (before image), 4 = update (after image)
-- __$update_mask varbinary   which columns changed
-- <captured columns...>
```

## Reading Changes with cdc.fn_cdc_get_all_changes

Every CDC function takes an LSN range. `sys.fn_cdc_get_min_lsn` and `sys.fn_cdc_get_max_lsn` give the valid window for an instance; querying outside it raises an error.

```sql
DECLARE @from_lsn binary(10) = sys.fn_cdc_get_min_lsn(N'dbo_Orders');
DECLARE @to_lsn   binary(10) = sys.fn_cdc_get_max_lsn(N'dbo_Orders');

SELECT
    sys.fn_cdc_map_lsn_to_time(__$start_lsn) AS ChangeTime,
    __$operation AS Operation,   -- 1 delete, 2 insert, 3/4 update
    OrderId, CustomerId, TotalAmount
FROM cdc.fn_cdc_get_all_changes_dbo_Orders(@from_lsn, @to_lsn, N'all');
```

For consumers that only need the final state of each row, use `N'all update old` (one row per changed row, update = after image only) or query the net-changes function `cdc.fn_cdc_get_net_changes_dbo_Orders`, which returns one row per row key with `__$operation` 1, 2, or 4.

## LSN Ranges and Watermarks

A consumer must persist the last processed LSN and resume from it, otherwise changes are read twice or missed.

```sql
-- Resume pattern: start just after the last processed LSN
DECLARE @last_lsn binary(10) = 0x0000002A000001A00001; -- stored watermark
DECLARE @from_lsn binary(10) = sys.fn_cdc_increment_lsn(@last_lsn);
DECLARE @to_lsn   binary(10) = sys.fn_cdc_get_max_lsn(N'dbo_Orders');

IF @from_lsn <= @to_lsn
BEGIN
    SELECT __$operation, OrderId, TotalAmount
    FROM cdc.fn_cdc_get_all_changes_dbo_Orders(@from_lsn, @to_lsn, N'all');
    -- After processing successfully, store @to_lsn as the new watermark.
END
```

`sys.fn_cdc_increment_lsn` moves past the last consumed transaction; using the raw stored LSN as `@from_lsn` would re-read that transaction. If the stored watermark is older than the retention window, `sys.fn_cdc_get_min_lsn` returns a larger LSN and the gap is unrecoverable from CDC — the consumer must re-snapshot.

## CDC vs Change Tracking vs Triggers

| Approach | What it records | Overhead | Use when |
|---|---|---|---|
| CDC | Full before/after row images, async from the log | Change tables + capture job, log reader | Auditing, ETL, syncing to another system |
| Change Tracking | Only that a row changed (PK + version), no values | Lightweight, synchronous version counter | Syncing clients that re-read current rows |
| DML triggers | Whatever the trigger writes, synchronous | Runs inside the transaction, slows writes | Enforcing a business rule on every write |

CDC is asynchronous, so it never blocks the original transaction, but changes appear only after the capture job reads the log — typically seconds, not instantly. Change Tracking is lighter but cannot tell you the old values. Triggers are synchronous and the most invasive.

## Cleanup Jobs and Retention

CDC keeps change rows only for the retention period (default 3 days). A SQL Agent cleanup job deletes older rows, so a consumer that polls less often than the retention window loses data.

```sql
-- Check and change retention (minutes) for the capture job
EXEC sys.sp_cdc_change_job
    @job_type = N'cleanup',
    @retention = 4320;   -- 4320 minutes = 3 days (the default)

-- Cleanup job status
SELECT job_type, retention, threshold, maxtrans, maxscans
FROM msdb.dbo.cdc_jobs;  -- capture and cleanup jobs are listed here
```

If SQL Server Agent is stopped, the capture job does not run, the transaction log cannot be truncated past the CDC scan point, and the log grows. Monitoring Agent and the `cdc_jobs` thresholds is part of running CDC.

## Worked Example: Counting Change Rows by Hand

Start with an empty change table for `dbo.Orders` and apply three statements in one batch:

1. `INSERT` one new order — CDC writes 1 row, `__$operation = 2`.
2. `UPDATE` that order's `TotalAmount` — CDC writes 2 rows: the before image (`__$operation = 3`) and the after image (`__$operation = 4`).
3. `DELETE` a different, older order — CDC writes 1 row, `__$operation = 1`.

Counting the rows `cdc.fn_cdc_get_all_changes_dbo_Orders` returns: 1 (insert) + 2 (update pair) + 1 (delete) = **4 rows** for 3 statements. The update is the only statement that produces two rows, which is why a consumer that counts rows as "changed rows" overcounts updates unless it collapses operation 3/4 pairs or uses the net-changes function, which would return 2 rows here (one per affected row key).

## Disabling CDC

```sql
EXEC sys.sp_cdc_disable_table
    @source_schema = N'dbo',
    @source_name   = N'Orders',
    @capture_instance = N'dbo_Orders';  -- or N'all'

EXEC sys.sp_cdc_disable_db;
GO
```

Disabling a table drops its change table and all un-read history, so drain the changes first.

## Key Takeaways

- CDC reads the transaction log asynchronously, so it captures full before/after images without slowing writes or needing triggers.
- Always read through an LSN range and persist the watermark with `sys.fn_cdc_increment_lsn`; re-reading or skipping an LSN is the most common consumer bug.
- An update produces two change rows (operations 3 and 4); use the net-changes function when one row per key is wanted.
- Retention defaults to 3 days and depends on SQL Server Agent running; a stopped Agent grows the transaction log and eventually loses history.
- Choose CDC for audit and ETL, Change Tracking for lightweight sync, and triggers only when a rule must run inside the transaction.
