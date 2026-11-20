# EF Core Migrations: Advanced Topics

Basic migrations handle schema changes. The advanced cases — custom operations, data migrations, idempotent deployment scripts, and safe rollbacks — need a few extra techniques.

## Custom Migration Operations

When the built-in operations are not enough (for example creating a full-text index or a database trigger), drop to raw SQL inside a migration:

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.Sql(
        "CREATE FULLTEXT CATALOG ProductCatalog AS DEFAULT;");
    migrationBuilder.Sql(
        "CREATE FULLTEXT INDEX ON Products(Name, Description) " +
        "KEY INDEX PK_Products ON ProductCatalog;");
}

protected override void Down(MigrationBuilder migrationBuilder)
{
    migrationBuilder.Sql("DROP FULLTEXT INDEX ON Products;");
    migrationBuilder.Sql("DROP FULLTEXT CATALOG ProductCatalog;");
}
```

Every custom `Up` needs a matching `Down`; otherwise rollback leaves the database in an unknown state.

## Idempotent SQL Scripts

For deployments, generate a script that can run against a database at any migration level. The `-Idempotent` flag wraps each migration in a check against `__EFMigrationsHistory`:

```bash
dotnet ef migrations script --idempotent --output deploy.sql
```

The script is safe to re-run: migrations already applied are skipped. Generate it from the build artifact, not from a developer machine, so the deployed script matches the deployed code exactly. Review the script before running it — it is the real deployment, and a bad `ALTER COLUMN` there is a production incident.

## Seeding and Data Migrations

Schema changes and data changes belong in the same migration when the data change is required by the schema change. Use `migrationBuilder.Sql` for bulk data fixes, and `InsertData` / `UpdateData` for small, reviewable seed rows:

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    // Backfill the new column before making it non-nullable.
    migrationBuilder.Sql(
        "UPDATE Orders SET Status = 'Pending' WHERE Status IS NULL;");

    migrationBuilder.AlterColumn<string>(
        name: "Status",
        table: "Orders",
        nullable: false);

    migrationBuilder.InsertData(
        table: "Roles",
        columns: new[] { "Id", "Name" },
        values: new object[,] { { 1, "Admin" }, { 2, "Customer" } });
}
```

Order matters: backfill first, then tighten the constraint. Prefer `HasData` in `OnModelCreating` for static lookup data; use migrations for one-off data fixes tied to a schema change.

## Rolling Back Safely

Roll back to a named migration, never by deleting rows from `__EFMigrationsHistory`:

```bash
dotnet ef database update AddProductDiscount
```

Rules for safe rollbacks:

- Never roll back a migration that dropped a column or table in production — the data is gone.
- Prefer a forward fix (a new migration that restores what you need) over a rollback.
- Test the `Down` path on a staging copy before you need it in production; an untested `Down` is a hope, not a plan.

## Key Takeaways

- Use `migrationBuilder.Sql` for operations EF Core has no built-in for, and always write the matching `Down`.
- Generate idempotent scripts with `dotnet ef migrations script --idempotent` for deployments.
- Backfill data before tightening a column constraint, in the same migration.
- Use `HasData` for static lookup data and migrations for one-off data fixes.
- Roll back with `dotnet ef database update <MigrationName>`; never edit `__EFMigrationsHistory` by hand.
- In production, prefer a forward-fix migration over rolling back a destructive change.
