# Computed Columns in SQL Server

A computed column is a column whose value is calculated from other columns in the same row. The value is never stored by the application — SQL Server derives it every time the row is read (or once at write time, if the column is `PERSISTED`).

## The Basic Idea

```sql
CREATE TABLE OrderDetails (
    OrderDetailId INT IDENTITY(1,1) PRIMARY KEY,
    Quantity      INT NOT NULL,
    UnitPrice     DECIMAL(10,2) NOT NULL,
    LineTotal     AS (Quantity * UnitPrice)
);
```

`LineTotal` is not a real stored value; it is an expression evaluated from `Quantity` and `UnitPrice`. Insert only the base columns:

```sql
INSERT INTO OrderDetails (Quantity, UnitPrice)
VALUES (3, 19.99);

SELECT Quantity, UnitPrice, LineTotal
FROM OrderDetails;
```

## Worked Example

For the inserted row, SQL Server computes:

```text
Quantity = 3, UnitPrice = 19.99
LineTotal = 3 * 19.99 = 59.97
```

Checked by hand: 19.99 × 3 = 19.99 × 2 + 19.99 = 39.98 + 19.99 = 59.97. The query returns:

```text
Quantity  UnitPrice  LineTotal
--------  ---------  ---------
3         19.99      59.97
```

The application can never store a wrong `LineTotal` — there is no `LineTotal` value to get wrong. Change `Quantity` to 4 and the total becomes 79.96 automatically (4 × 19.99 = 79.96).

## PERSISTED vs Non-Persisted

```sql
-- Calculated on every read; costs CPU per query, no storage.
LineTotal AS (Quantity * UnitPrice)

-- Calculated once at insert/update and stored on disk.
LineTotal AS (Quantity * UnitPrice) PERSISTED
```

| Aspect | Non-persisted | PERSISTED |
|---|---|---|
| Storage | None — computed on read | Stored in the row |
| Read cost | Recomputed per query | Read like a normal column |
| Write cost | Nothing extra | Computed at insert/update |
| Can be indexed | Only via a view (or with conditions) | Yes, directly |
| Required for | Most expressions | Deterministic expressions only |

Use non-persisted when the expression is cheap and the column is read rarely. Use `PERSISTED` when the column is read often, filtered on, or indexed.

## Determinism

A computed column can only be `PERSISTED` (or indexed) if its expression is **deterministic** — it must return the same result every time for the same inputs. Arithmetic on columns is deterministic. These are not:

```sql
-- NOT deterministic: GETDATE() changes between calls.
CreatedAge AS (DATEDIFF(day, CreatedAt, GETDATE()))

-- Deterministic: same inputs always give the same output.
FullName AS (FirstName + ' ' + LastName) PERSISTED
```

Calling a user-defined function in the expression requires the function to be schema-bound (`WITH SCHEMABINDING`) for the column to count as deterministic.

## Indexing a Persisted Computed Column

Because a persisted column is physically stored, it behaves like any other column for indexing:

```sql
CREATE TABLE OrderDetails (
    OrderDetailId INT IDENTITY(1,1) PRIMARY KEY,
    Quantity      INT NOT NULL,
    UnitPrice     DECIMAL(10,2) NOT NULL,
    LineTotal     AS (Quantity * UnitPrice) PERSISTED
);

CREATE INDEX IX_OrderDetails_LineTotal
    ON OrderDetails (LineTotal);
```

Now a query such as `WHERE LineTotal > 100` can seek on the index instead of computing the expression for every row. The index is maintained automatically as `Quantity` or `UnitPrice` change.

## Limits

- A computed column **cannot reference another computed column** in the same table in some cases — SQL Server does allow referencing another computed column only if that column is itself deterministic and the chain is resolvable, but the safe practice is to repeat the base expression or use a view. Do not build long chains of computed columns.
- **No subqueries** and no references to other tables or other rows. The expression sees only columns of its own row.
- A computed column cannot be the target of an `INSERT` or `UPDATE` — you write the base columns and the expression follows.
- The expression cannot call non-deterministic functions (`GETDATE()`, `NEWID()`, `RAND()`) if the column must be persisted or indexed.
- Data type matters: `Quantity * UnitPrice` inherits the precision of the expression. With `DECIMAL(10,2)` inputs the result stays `DECIMAL`, but mixing `INT` and `FLOAT` can produce rounding surprises — check the resulting type with `sp_help` or by selecting into a temp table.

## Computed Columns vs Doing It in the App

Computing in the application is flexible but every reader must repeat the formula identically — and someone will get it wrong or forget to update it. A computed column puts the rule in exactly one place, the database, where every query, report, and application sees the same value. The price is that the logic now lives in the schema, so it should be used for simple, stable derivations like totals and concatenations, not for business rules that change often.

## Key Takeaways

- A computed column derives its value from other columns in the same row, e.g. `LineTotal AS (Quantity * UnitPrice)`.
- Worked example: Quantity 3 × UnitPrice 19.99 gives LineTotal 59.97 (19.99 × 3 = 59.97).
- `PERSISTED` stores the value at write time; non-persisted recomputes it on every read.
- Only deterministic expressions can be persisted or indexed; `GETDATE()` and `NEWID()` are not deterministic.
- Limits: no subqueries, no references to other tables, cannot insert into a computed column, and avoid chaining computed columns on each other.
