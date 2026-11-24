# SQL Server In-Memory OLTP

In-Memory OLTP is a SQL Server engine feature that stores tables in memory and compiles stored procedures to native code. It targets high-throughput, latch-free workloads — tens of thousands of transactions per second on a single server.

## How It Works

Memory-optimized tables live entirely in RAM. There is no locking and no latching: concurrency is handled with multi-version concurrency control (MVCC). A reader never blocks a writer, and a writer never blocks a reader; each transaction sees a consistent snapshot of committed rows.

Durability is a choice per table:

- `SCHEMA_AND_DATA` — data survives a restart (logged to the transaction log and checkpoint files).
- `SCHEMA_ONLY` — only the schema survives; data is lost on restart. Useful for scratch or session state.

## Creating a Memory-Optimized Table

The database needs a memory-optimized filegroup first:

```sql
ALTER DATABASE ShopDB
ADD FILEGROUP ShopDB_mod CONTAINS MEMORY_OPTIMIZED_DATA;

ALTER DATABASE ShopDB
ADD FILE (NAME = 'ShopDB_mod1', FILENAME = 'C:\Data\ShopDB_mod1')
TO FILEGROUP ShopDB_mod;
```

Then the table itself:

```sql
CREATE TABLE dbo.OrderLines
(
    OrderLineId INT NOT NULL PRIMARY KEY NONCLUSTERED HASH WITH (BUCKET_COUNT = 1000000),
    OrderId     INT NOT NULL,
    ProductId   INT NOT NULL,
    Quantity    INT NOT NULL,
    INDEX IX_OrderId NONCLUSTERED HASH (OrderId) WITH (BUCKET_COUNT = 100000)
)
WITH (MEMORY_OPTIMIZED = ON, DURABILITY = SCHEMA_AND_DATA);
```

Hash indexes need a `BUCKET_COUNT` sized to roughly 1–2x the expected row count. Too few buckets causes long chains; too many wastes memory. Nonclustered indexes also exist for range scans, which hash indexes cannot do.

## Natively Compiled Stored Procedures

A natively compiled procedure is translated to machine code at creation time, removing interpretation overhead:

```sql
CREATE PROCEDURE dbo.InsertOrderLine
    @OrderLineId INT, @OrderId INT, @ProductId INT, @Quantity INT
WITH NATIVE_COMPILATION, SCHEMABINDING, EXECUTE AS OWNER
AS
BEGIN ATOMIC WITH
(
    TRANSACTION ISOLATION LEVEL = SNAPSHOT,
    LANGUAGE = N'English'
)
    INSERT INTO dbo.OrderLines (OrderLineId, OrderId, ProductId, Quantity)
    VALUES (@OrderLineId, @OrderId, @ProductId, @Quantity);
END;
```

The `BEGIN ATOMIC` block is mandatory. Only a restricted T-SQL subset is allowed inside: no `SELECT` with joins across memory-optimized and disk-based tables in the same statement in older versions, no dynamic SQL, no cursors.

## When to Use It

Good fits:

- High-frequency inserts and updates on narrow rows (session state, queues, counters, trading ticks).
- Hot lookup tables read millions of times per second.
- Staging tables for ETL where the data is transient.

Bad fits:

- Wide rows or tables over a few hundred GB — memory is finite and expensive.
- Reporting queries with large scans and aggregations — columnstore is better.
- Workloads needing features In-Memory OLTP lacks (some data types, triggers on memory-optimized tables in older versions, foreign keys between memory-optimized tables).

## Limits to Know

- Memory-optimized tables cannot use `IDENTITY` with a large seed gap problem in the same way; they do support identity columns, but gaps after a crash are larger.
- Maximum row size is 8,060 bytes.
- A table must fit in memory — plan for roughly 2x the data size to cover row versions.
- Cross-database transactions involving memory-optimized tables are restricted.

## Key Takeaways

- In-Memory OLTP replaces locks and latches with MVCC, so readers never block writers.
- Choose durability per table: `SCHEMA_AND_DATA` for real data, `SCHEMA_ONLY` for scratch data.
- Hash indexes need a well-chosen `BUCKET_COUNT` (about 1–2x expected rows); use nonclustered indexes for range scans.
- Natively compiled procedures run as machine code but only support a restricted T-SQL subset inside `BEGIN ATOMIC`.
- Use it for hot, narrow, high-frequency OLTP work — not for wide tables, big reporting scans, or data that does not fit in RAM.
