# IDENTITY Columns and Sequences in SQL Server

Both `IDENTITY` and `SEQUENCE` generate incrementing numbers, but they live
at different levels: an `IDENTITY` column belongs to one table, while a
`SEQUENCE` is an independent database object that any number of tables,
columns, or even ad-hoc queries can draw from.

## IDENTITY Columns

An `IDENTITY(seed, increment)` column auto-generates a value on every insert.
You cannot supply the value yourself unless `IDENTITY_INSERT` is on.

```sql
CREATE TABLE Orders (
    OrderId INT IDENTITY(1, 1) PRIMARY KEY,
    CustomerName NVARCHAR(100) NOT NULL,
    PlacedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);

INSERT INTO Orders (CustomerName) VALUES ('Ayesha'); -- OrderId = 1
INSERT INTO Orders (CustomerName) VALUES ('Bilal');  -- OrderId = 2
```

## SEQUENCE Objects

A sequence is created once and used wherever a number is needed, with
`NEXT VALUE FOR`:

```sql
CREATE SEQUENCE dbo.InvoiceSeq
    START WITH 1000
    INCREMENT BY 1
    CACHE 50;

CREATE TABLE Invoices (
    InvoiceId INT PRIMARY KEY DEFAULT (NEXT VALUE FOR dbo.InvoiceSeq),
    Total DECIMAL(18, 2) NOT NULL
);

SELECT NEXT VALUE FOR dbo.InvoiceSeq; -- 1000, then 1001, ...
```

A single sequence can feed several tables — handy for a shared document
number across invoices, credit notes and receipts, or for generating keys in
application code before the insert happens.

## Gaps

Neither mechanism guarantees gap-free numbers, and you should never assume
otherwise:

- A rolled-back transaction consumes the value — it is not reused.
- A failed insert, a `DELETE`, or a server restart can leave holes.
- Sequences with `CACHE` lose the cached block on restart; identity columns
  can jump by 1000 after an unclean shutdown (the identity cache).

If the business needs gap-free numbers (invoice numbers in some countries),
generate them with a serialized table or a stored procedure under a lock, not
with `IDENTITY` or `SEQUENCE`.

## Caching and Performance

- `IDENTITY` values are cached in memory in blocks; under heavy concurrent
  inserts this avoids a disk write per row, at the cost of the jump-on-restart
  behavior above.
- `SEQUENCE ... CACHE n` pre-allocates `n` values in memory. Larger cache
  means fewer metadata writes and less contention, but more values lost on
  restart. `NO CACHE` guarantees no cache loss, at a performance cost.
- If a sequence becomes a bottleneck, the usual fix is a bigger cache, not
  switching to identity.

## Reseeding and Restarting

```sql
-- Identity: make the next value 5000 (sets current value to 4999)
DBCC CHECKIDENT ('Orders', RESEED, 4999);

-- Check the current identity value without changing it
DBCC CHECKIDENT ('Orders', NORESEED);

-- Sequence: restart from a new value
ALTER SEQUENCE dbo.InvoiceSeq RESTART WITH 5000;
```

`DBCC CHECKIDENT` with `RESEED` is a maintenance operation, not something
application code should call. After a `TRUNCATE TABLE`, identity reseeds to
the original seed automatically.

## When to Use Each

| Need | Choice |
|------|--------|
| Simple surrogate key for one table | `IDENTITY` |
| One number series shared by several tables | `SEQUENCE` |
| Value needed before the insert (e.g. in app code) | `SEQUENCE` |
| Gap-free numbers required | Neither — use a serialized counter |
| Cycling or min/max bounds on the series | `SEQUENCE` (`CYCLE`, `MINVALUE`, `MAXVALUE`) |

## IDENTITY vs GUID Keys

- `INT IDENTITY` keys are small (4 bytes), sequential, and index-friendly:
  new rows append at the end of the clustered index instead of causing page
  splits all over the B-tree.
- `UNIQUEIDENTIFIER` (`NEWID()`/`NEWSEQUENTIALID()`) keys are globally
  unique and can be generated on the client before insert, but random GUIDs
  fragment indexes and are 16 bytes wide. Prefer `NEWSEQUENTIALID()` over
  `NEWID()` when GUIDs are needed.
- A common middle ground: `INT IDENTITY` as the clustered primary key, with
  a GUID as a separate public-facing identifier if external systems need one.

## Key Takeaways

- `IDENTITY` is tied to one table; `SEQUENCE` is a standalone object usable
  from many tables and from application code.
- Both can produce gaps after rollbacks, deletes, failures and restarts —
  never treat them as gap-free.
- `CACHE` improves throughput but loses values on restart; tune it per
  workload.
- Reseed identity with `DBCC CHECKIDENT`, restart a sequence with
  `ALTER SEQUENCE ... RESTART WITH`.
- For surrogate keys, sequential integers beat random GUIDs on index health;
  use GUIDs only when global uniqueness before insert is required.
