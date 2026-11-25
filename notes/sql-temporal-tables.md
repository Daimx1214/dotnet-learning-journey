# SQL Server Temporal Tables

A system-versioned temporal table keeps a full history of every row change automatically. The current table always holds the latest version of each row, and a history table holds the old versions with their validity period. This gives point-in-time queries without writing audit triggers by hand.

## How It Works

Two `datetime2` columns mark the validity period of each row version:

- `ValidFrom`: when this version became current.
- `ValidTo`: when this version stopped being current. The current version has `ValidTo` set to `9999-12-31 23:59:59.9999999`.

SQL Server fills these columns itself. An `UPDATE` moves the old version into the history table and inserts the new version with a fresh `ValidFrom`. A `DELETE` moves the row to history and removes it from the current table.

## Creating One

```sql
CREATE TABLE Products
(
    Id INT PRIMARY KEY,
    Name NVARCHAR(100) NOT NULL,
    Price DECIMAL(10, 2) NOT NULL,
    ValidFrom DATETIME2 GENERATED ALWAYS AS ROW START NOT NULL,
    ValidTo DATETIME2 GENERATED ALWAYS AS ROW END NOT NULL,
    PERIOD FOR SYSTEM_TIME (ValidFrom, ValidTo)
)
WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = dbo.ProductsHistory));
```

`PERIOD FOR SYSTEM_TIME` declares the two columns as the validity period, and `SYSTEM_VERSIONING = ON` turns history capture on. The history table can be created explicitly first, or SQL Server creates it with a default name.

An existing table can be converted with `ALTER TABLE ... ADD PERIOD FOR SYSTEM_TIME` followed by `SET (SYSTEM_VERSIONING = ON)`. The table must have a primary key, and the period columns must be `datetime2`.

## Querying History

Plain queries read only current rows. Temporal clauses read history:

```sql
-- The price of product 1 as it was on 1 January 2026
SELECT * FROM Products
FOR SYSTEM_TIME AS OF '2026-01-01'
WHERE Id = 1;

-- Every version of product 1 that was valid at any point in January 2026
SELECT * FROM Products
FOR SYSTEM_TIME BETWEEN '2026-01-01' AND '2026-01-31'
WHERE Id = 1;

-- All versions, current and historical
SELECT * FROM Products
FOR SYSTEM_TIME ALL
WHERE Id = 1;

-- Versions valid from a start date up to, but not including, an end date
SELECT * FROM Products
FOR SYSTEM_TIME FROM '2026-01-01' TO '2026-02-01'
WHERE Id = 1;
```

`AS OF` is the most common form: one point in time, one row per key. `BETWEEN` includes versions valid at either boundary. `CONTAINED IN` returns only versions fully inside the range.

## Retention and Cleanup

History grows forever unless retention is set. SQL Server can clean old history automatically:

```sql
ALTER TABLE Products
SET (SYSTEM_VERSIONING = ON (HISTORY_RETENTION_PERIOD = 6 MONTHS));
```

The cleanup task runs in the background and removes history older than the retention period. Retention periods use units like `DAY`, `WEEK`, `MONTH`, or `YEAR`, with a maximum of 10 years. If the business needs history kept indefinitely, leave retention off and archive manually.

## Schema Changes

Most schema changes work normally, but a few rules apply:

- The period columns cannot be dropped or altered while versioning is on. Turn versioning off, alter the table, then turn it on again.
- Adding a column to the current table adds it to the history table automatically.
- Dropping a column removes it from the history table too, which destroys that part of history, so plan drops carefully.
- Data changes to the history table directly are blocked while versioning is on; turn it off first if a correction is truly needed.

## When to Use It

- Audit trails: who changed a price, and when.
- "As of" reporting: reconstruct a report exactly as it looked last quarter.
- Undo support: restore a row to its previous values from history.

Temporal tables are not a replacement for an audit log that records who made the change, because they store no user information. Combine them with an audit column such as `ModifiedBy` when the actor matters.

## Key Takeaways

- A temporal table keeps current rows in the main table and old versions in a history table, with `ValidFrom` and `ValidTo` managed by SQL Server.
- `FOR SYSTEM_TIME AS OF` answers point-in-time questions; `BETWEEN` and `ALL` cover ranges and full history.
- Set a `HISTORY_RETENTION_PERIOD` or history grows without bound.
- Schema changes to period columns require versioning to be switched off first.
- Temporal tables record what changed and when, but not who changed it.
