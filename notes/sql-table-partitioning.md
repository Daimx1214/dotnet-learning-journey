# SQL Server Table Partitioning (SQL)

Table partitioning splits one large table into smaller physical pieces — partitions — while keeping a single logical table for queries. Each row lives in exactly one partition, decided by a partition function applied to a partitioning column (usually a date). Queries that filter on that column can skip whole partitions (partition elimination), and maintenance operations like archiving old data become a metadata-only switch instead of a slow `DELETE`.

## Partition Function and Scheme

Two objects define the layout. The partition function maps values to partition numbers using boundary values; the partition scheme maps partition numbers to filegroups, which is where the data physically lives. A simple setup for an `Orders` table partitioned by year:

```sql
-- RANGE RIGHT: boundary values belong to the partition on their right.
-- Boundaries 2024-01-01, 2025-01-01, 2026-01-01 create 4 partitions:
--   P1: < 2024-01-01   P2: 2024   P3: 2025   P4: >= 2026-01-01
CREATE PARTITION FUNCTION pf_OrderDate (DATE)
AS RANGE RIGHT FOR VALUES ('2024-01-01', '2025-01-01', '2026-01-01');

-- Each partition sits on its own filegroup (create the filegroups first).
CREATE PARTITION SCHEME ps_OrderDate
AS PARTITION pf_OrderDate
TO (FG_Archive, FG_2024, FG_2025, FG_2026);

CREATE TABLE Orders
(
    OrderId     INT IDENTITY PRIMARY KEY,
    OrderDate   DATE NOT NULL,
    CustomerId  INT NOT NULL,
    TotalAmount DECIMAL(10, 2) NOT NULL
)
ON ps_OrderDate (OrderDate);
```

## RANGE LEFT vs RANGE RIGHT

The choice decides which side owns a boundary value. With `RANGE RIGHT`, the boundary value belongs to the partition on its right, so boundaries are natural "start of period" dates (`'2024-01-01'` opens the 2024 partition). With `RANGE LEFT`, the boundary belongs to the partition on its left, so boundaries are "end of period" values (`'2023-12-31'` closes the 2023 partition). Both describe the same layout if you shift the boundaries by one unit; the common convention is `RANGE RIGHT` with start-of-period boundaries because it reads more naturally and matches how sliding windows add new partitions.

A table with `n` boundary values always has `n + 1` partitions — the extra one holds everything beyond the last boundary, which is why the scheme above needs four filegroups for three boundaries. Forgetting that last filegroup is the most common setup mistake.

## Sliding Window: Adding and Removing Partitions

The sliding window is the maintenance pattern that makes partitioning pay off: each period you split off a new partition for incoming data and switch out the oldest one for archiving. `SWITCH` moves an entire partition between tables as a metadata operation — instant, no data movement — provided the source and target have identical structure, indexes, and constraints.

```sql
-- 1. Split the newest partition to make room for 2027 data.
--    The empty partition beyond the last boundary is split at the new boundary.
ALTER PARTITION FUNCTION pf_OrderDate()
SPLIT RANGE ('2027-01-01');

-- 2. Prepare a staging table with the exact same structure and indexes.
CREATE TABLE Orders_2023_Staging
(
    OrderId     INT IDENTITY PRIMARY KEY,
    OrderDate   DATE NOT NULL,
    CustomerId  INT NOT NULL,
    TotalAmount DECIMAL(10, 2) NOT NULL,
    CONSTRAINT CK_Staging_2023 CHECK (OrderDate < '2024-01-01')
)
ON FG_Archive;

-- 3. Switch the oldest partition out (partition 1 holds pre-2024 rows).
ALTER TABLE Orders SWITCH PARTITION 1 TO Orders_2023_Staging;

-- 4. The staging table is now a standalone archive table: back it up, then drop it.
--    Finally merge the now-empty boundary to keep the partition count tidy.
ALTER PARTITION FUNCTION pf_OrderDate()
MERGE RANGE ('2024-01-01');
```

One hard rule: a partition can only be split if it is empty, or if it contains data only on one side of the new boundary — splitting a full partition is a logged data-moving operation, so always split the empty tail partition and switch data out before merging.

## Partition Elimination and Indexing

Partitioning only speeds up queries that filter on the partitioning column (or join on it). A query like `WHERE OrderDate >= '2025-01-01'` reads only the 2025 and 2026 partitions; a query without an `OrderDate` filter scans all of them, and can even be slower than an unpartitioned table because of the extra partition overhead. Check the execution plan: look for partition elimination in the seek/scan operators, and confirm the `Actual Number of Partitions Accessed` is small.

Indexes need the same decision. An aligned index includes the partitioning column as a key, so each partition carries its own index slice and `SWITCH` still works. A non-aligned index spans the whole table and blocks partition switching. The clustered index of a partitioned table must contain the partitioning column (it is the key the function is applied to), which is why `OrderDate` is part of the table's key structure even though `OrderId` is the identity.

## Benefits and Pitfalls

Partitioning helps when a table is large (tens of millions of rows or more) and has a natural lifecycle — hot recent data queried constantly, cold old data archived or deleted in bulk. It makes bulk loads, index rebuilds, and statistics updates per-partition operations, and lets old filegroups sit on cheaper storage. It does not make a single-row lookup faster, does not replace proper indexing, and adds real operational complexity: partition functions, schemes, and filegroups must be maintained, and a query that forgets the partitioning column pays the overhead with no benefit. If the table is small or the workload is OLTP point lookups, partitioning is the wrong tool — a good clustered index is enough.

## Key Takeaways

- A partition function maps values to partitions via boundaries; a scheme maps partitions to filegroups; the table stays one logical table.
- `RANGE RIGHT` with start-of-period boundaries is the standard convention; `n` boundaries always means `n + 1` partitions.
- The sliding window (SPLIT the empty tail, SWITCH the oldest partition out, MERGE the boundary) makes archiving a metadata-only operation.
- Speed comes from partition elimination, which only happens when queries filter on the partitioning column.
- Keep indexes aligned (include the partitioning column) or partition switching breaks; only partition large tables with a clear data lifecycle.
