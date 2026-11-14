# The MERGE Statement in SQL Server (SQL)

`MERGE` performs an upsert — insert, update, and delete — in a single statement. You give it a target table and a source (a table, a table variable, or a `VALUES` list), match rows on a key, and declare what happens when rows match, when they exist only in the source, and when they exist only in the target. It is the standard tool for synchronizing a table with an incoming feed.

## The Upsert Pattern

```sql
-- Sync the Products table with tonight's feed.
MERGE INTO Products AS Target
USING (VALUES
    (1, 'Keyboard', 29.99),
    (2, 'Mouse',    19.99),
    (3, 'Monitor', 149.99)
) AS Source (Id, Name, Price)
ON Target.Id = Source.Id

WHEN MATCHED THEN
    UPDATE SET
        Target.Name  = Source.Name,
        Target.Price = Source.Price

WHEN NOT MATCHED BY TARGET THEN
    INSERT (Id, Name, Price)
    VALUES (Source.Id, Source.Name, Source.Price)

WHEN NOT MATCHED BY SOURCE THEN
    DELETE;
```

`WHEN MATCHED` updates rows present in both. `WHEN NOT MATCHED BY TARGET` inserts rows that exist only in the source. `WHEN NOT MATCHED BY SOURCE` deletes rows that exist only in the target — use it only when the source is a complete picture of what the table should hold, or a partial feed will wipe real data.

## The OUTPUT Clause

`MERGE` supports `OUTPUT`, which returns one row per action taken — useful for auditing or for capturing the new identity values:

```sql
DECLARE @Changes TABLE (ActionTaken NVARCHAR(10), ProductId INT);

MERGE INTO Products AS Target
USING StagingProducts AS Source
ON Target.Id = Source.Id
WHEN MATCHED THEN
    UPDATE SET Target.Price = Source.Price
WHEN NOT MATCHED BY TARGET THEN
    INSERT (Id, Name, Price)
    VALUES (Source.Id, Source.Name, Source.Price)
OUTPUT $action, inserted.Id
INTO @Changes (ActionTaken, ProductId);

SELECT ActionTaken, COUNT(*) AS RowsAffected
FROM @Changes
GROUP BY ActionTaken;
```

`$action` is `INSERT`, `UPDATE`, or `DELETE`. The `inserted` pseudo-table holds the post-change row; `deleted` holds the pre-change row. This is the cleanest way to log exactly what a sync did.

## The Race-Condition Warning

`MERGE` is not atomic against concurrent writers by default. Two sessions running the same upsert at once can both decide a row does not exist and both try to insert it, causing a primary-key violation — or worse, both update and lose a change. The fix is to take a stronger lock on the target for the duration of the statement:

```sql
MERGE INTO Products WITH (HOLDLOCK) AS Target
USING StagingProducts AS Source
ON Target.Id = Source.Id
WHEN MATCHED THEN
    UPDATE SET Target.Price = Source.Price
WHEN NOT MATCHED BY TARGET THEN
    INSERT (Id, Name, Price)
    VALUES (Source.Id, Source.Name, Source.Price);
```

`WITH (HOLDLOCK)` is equivalent to serializable isolation for the target table: it holds the locks until the statement completes, so no other session can insert a matching key in between. Always add it to `MERGE` statements that run concurrently. Without it, an upsert is a race waiting to happen.

## When to Avoid MERGE

`MERGE` has a history of bugs and surprising behavior — for example, a source row matching two target rows raises an error instead of updating twice, and `WHEN MATCHED` with an additional `AND` condition is easy to get subtly wrong. For a simple "insert if missing, else update" on a single row, a plain `IF EXISTS ... UPDATE ... ELSE INSERT` inside a transaction (or `INSERT ... ON CONFLICT` in other databases) is easier to read and debug. Use `MERGE` when you genuinely need all three actions in one set-based statement, such as a nightly sync.

## Key takeaways

- `MERGE` combines insert, update, and delete in one statement, driven by `WHEN MATCHED` / `WHEN NOT MATCHED` clauses.
- `OUTPUT $action` with `inserted`/`deleted` records exactly what the statement did.
- Always add `WITH (HOLDLOCK)` to the target, or concurrent upserts can collide on the same key.
- For single-row upserts, a simple `IF EXISTS` pattern is clearer; reserve `MERGE` for true set-based syncs.
