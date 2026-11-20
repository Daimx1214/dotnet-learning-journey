# SQL Server Index Maintenance

Indexes fragment as data changes: page splits scatter rows, and queries that used to read sequentially start jumping around the disk. Regular maintenance keeps seeks fast and statistics fresh.

## Measuring Fragmentation

`sys.dm_db_index_physical_stats` reports fragmentation per index. `avg_fragmentation_in_percent` is the number to watch:

```sql
SELECT
    OBJECT_NAME(ips.object_id) AS table_name,
    i.name AS index_name,
    ips.avg_fragmentation_in_percent,
    ips.page_count,
    ips.avg_page_space_used_in_percent
FROM sys.dm_db_index_physical_stats(DB_ID(), NULL, NULL, NULL, 'LIMITED') AS ips
JOIN sys.indexes AS i
    ON ips.object_id = i.object_id AND ips.index_id = i.index_id
WHERE ips.page_count > 1000
  AND ips.avg_fragmentation_in_percent > 10
ORDER BY ips.avg_fragmentation_in_percent DESC;
```

Ignore tiny indexes (under ~1000 pages): rebuilding them costs more than the fragmentation hurts.

## REORGANIZE vs REBUILD

The standard thresholds, based on `avg_fragmentation_in_percent`:

- **5–30%: REORGANIZE.** Defragments leaf pages in place, online, and cheap. Good for regular weekly maintenance.
- **Over 30%: REBUILD.** Drops and recreates the index. More thorough, but heavier — it needs free space and, in Standard Edition, takes an offline lock unless you use `ONLINE = ON` (Enterprise only).

```sql
-- Light defragmentation, stays online
ALTER INDEX IX_Orders_CustomerId ON Orders REORGANIZE;

-- Full rebuild, online where the edition allows it
ALTER INDEX IX_Orders_CustomerId ON Orders
REBUILD WITH (ONLINE = ON, MAXDOP = 4);
```

Rebuilding an index also updates its statistics with a full scan, so a separate `UPDATE STATISTICS` is usually unnecessary right after a rebuild.

## Updating Statistics

Stale statistics make the optimizer pick bad plans even on healthy indexes. Update them when a table has had heavy inserts, updates, or deletes:

```sql
-- All statistics on one table
UPDATE STATISTICS Orders;

-- One statistic, full scan for accuracy on a critical column
UPDATE STATISTICS Orders IX_Orders_CustomerId WITH FULLSCAN;
```

Auto-update statistics handles most cases, but the default threshold (20% of rows changed) is far too high for large tables. Enable trace flag 2371 or use a maintenance job that updates statistics nightly on busy tables.

## Automating with Maintenance Jobs

Ola Hallengren's free Maintenance Solution is the standard approach: one job runs `IndexOptimize` nightly, reorganizing or rebuilding by fragmentation level and updating statistics where needed:

```sql
EXECUTE dbo.IndexOptimize
    @Databases = 'USER_DATABASES',
    @FragmentationLow = NULL,
    @FragmentationMedium = 'INDEX_REORGANIZE',
    @FragmentationHigh = 'INDEX_REBUILD_ONLINE',
    @UpdateStatistics = 'ALL',
    @OnlyModifiedStatistics = 'Y';
```

Schedule it in off-peak hours, log the output, and alert on failures — a maintenance job that silently stops running is worse than no job, because fragmentation creeps back unnoticed.

## Key Takeaways

- Measure fragmentation with `sys.dm_db_index_physical_stats`; ignore indexes under ~1000 pages.
- REORGANIZE for 5–30% fragmentation; REBUILD above 30%.
- Rebuilding updates index statistics automatically; reorganizing does not.
- Update statistics after heavy data changes, with `FULLSCAN` on critical columns.
- The 20% auto-update threshold is too high for large tables — use a maintenance job.
- Automate with a scheduled job (for example Ola Hallengren's IndexOptimize) and monitor it.
