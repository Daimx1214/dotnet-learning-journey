# SQL Server Triggers (SQL)

A trigger is a stored procedure that SQL Server runs automatically when a data-modification event fires on a table or view — an `INSERT`, `UPDATE`, or `DELETE`. Triggers are powerful for auditing and enforcing rules the schema cannot express, but they run silently, inside the same transaction as the statement that fired them, so a slow or buggy trigger slows down (or breaks) every write to the table. Use them deliberately, never as a default.

## AFTER vs INSTEAD OF Triggers

An `AFTER` trigger fires once the statement has modified the data but before the transaction commits; it can roll back the whole statement with `ROLLBACK` if a rule is violated. An `INSTEAD OF` trigger replaces the statement entirely — the trigger's own code decides what actually happens, which makes it the only way to make a view updatable or to intercept a write and redirect it.

```sql
-- AFTER trigger: runs after the INSERT, can still roll it back.
CREATE TRIGGER trg_Products_PreventNegativePrice
ON Products
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM inserted WHERE Price < 0)
    BEGIN
        RAISERROR('Price cannot be negative.', 16, 1);
        ROLLBACK TRANSACTION;
    END
END;
```

```sql
-- INSTEAD OF trigger: intercepts the DELETE and archives the row instead.
CREATE TRIGGER trg_Customers_SoftDelete
ON Customers
INSTEAD OF DELETE
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE Customers
    SET IsDeleted = 1, DeletedAt = SYSUTCDATETIME()
    WHERE Id IN (SELECT Id FROM deleted);
END;
```

## The inserted and deleted Tables

Inside a trigger, SQL Server exposes two pseudo-tables: `inserted` holds the new versions of affected rows (for `INSERT` and `UPDATE`), and `deleted` holds the old versions (for `UPDATE` and `DELETE`). Both are empty for statements that affect no rows, and both can contain **many** rows — a trigger must always be written set-based, never assuming a single row. A trigger that reads `SELECT @id = Id FROM inserted` silently processes only one row of a multi-row insert and corrupts the rest.

```sql
-- Audit-log trigger: records every change to Products, set-based.
CREATE TABLE ProductAudit
(
    AuditId     INT IDENTITY(1,1) PRIMARY KEY,
    ProductId   INT NOT NULL,
    Action      CHAR(1) NOT NULL,   -- I = insert, U = update, D = delete
    OldPrice    DECIMAL(18,2) NULL,
    NewPrice    DECIMAL(18,2) NULL,
    ChangedBy   SYSNAME NOT NULL DEFAULT (SUSER_SNAME()),
    ChangedAt   DATETIME2 NOT NULL DEFAULT (SYSUTCDATETIME())
);

CREATE TRIGGER trg_Products_Audit
ON Products
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO ProductAudit (ProductId, Action, OldPrice, NewPrice)
    SELECT i.Id, 'I', NULL, i.Price FROM inserted i
    WHERE NOT EXISTS (SELECT 1 FROM deleted d WHERE d.Id = i.Id);

    INSERT INTO ProductAudit (ProductId, Action, OldPrice, NewPrice)
    SELECT i.Id, 'U', d.Price, i.Price
    FROM inserted i
    JOIN deleted d ON d.Id = i.Id;

    INSERT INTO ProductAudit (ProductId, Action, OldPrice, NewPrice)
    SELECT d.Id, 'D', d.Price, NULL FROM deleted d
    WHERE NOT EXISTS (SELECT 1 FROM inserted i WHERE i.Id = d.Id);
END;
```

## Recursion and Nesting

A trigger can fire another trigger: an `UPDATE` inside a trigger on `Products` can fire a trigger on `Orders`, and so on, up to 32 levels of nesting. Direct recursion — a trigger modifying its own table — is controlled by the database option `RECURSIVE_TRIGGERS` (off by default); indirect recursion is controlled by the server option `nested triggers`. Keep both off unless you have a specific, tested reason: a recursive trigger is the classic way to turn a single update into an infinite loop that only a transaction rollback stops.

```sql
-- Check the current settings:
SELECT name, is_recursive_triggers_on FROM sys.databases WHERE name = DB_NAME();
EXEC sp_configure 'nested triggers';  -- 1 = allowed (default)
```

## When to Avoid Triggers

- **Business logic** — rules that belong in the application layer are invisible and untestable when hidden in a trigger.
- **Cross-database or cross-server writes** — a trigger that reaches into another database couples availability and makes failures hard to diagnose.
- **Heavy work** — triggers run inside the triggering transaction, so a slow trigger blocks the original statement and can cause deadlocks and timeouts.
- **Cascading complexity** — chains of triggers firing triggers are nearly impossible to reason about; prefer explicit application code or, for auditing, a dedicated audit mechanism such as temporal tables or Change Data Capture.
- **Enforcing referential integrity** — foreign keys do this better, faster, and visibly in the schema.

Triggers earn their place for audit trails, soft deletes on legacy tables, and rules that must hold no matter which application touches the data. Everywhere else, application code is clearer.

## Key Takeaways

- `AFTER` triggers run after the change (and can roll it back); `INSTEAD OF` triggers replace the statement entirely.
- `inserted` and `deleted` are set-based pseudo-tables — always write triggers for multi-row statements, never assume one row.
- An audit-log trigger joins `inserted` and `deleted` to capture old and new values for inserts, updates, and deletes.
- Keep `RECURSIVE_TRIGGERS` off and nesting shallow; triggers run inside the triggering transaction, so a slow trigger blocks every write.
- Prefer application code, foreign keys, temporal tables, or CDC over triggers for business logic and heavy work.
