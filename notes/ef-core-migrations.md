# EF Core Migrations

Date: 2026-10-29

## What a migration is

A migration is a code-based record of a change to the database schema. When the EF Core model changes (a new entity, a new property, a changed column type), a migration captures that change so it can be applied to the database in a controlled, repeatable way. The model snapshot keeps track of the current shape of the model.

## Basic workflow

1. Change the model — for example, add a property to an entity.
2. Create a migration from the change.
3. Review the generated migration before applying it.
4. Apply it to the database.

```bash
dotnet ef migrations add AddProductPrice
dotnet ef database update
```

## Example

Suppose a `Product` entity gains a price:

```csharp
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}
```

The generated migration's `Up` method adds the column, and its `Down` method removes it, so the change can be rolled back:

```bash
# Roll back to the previous migration by name
dotnet ef database update PreviousMigrationName
```

## Useful commands

- `dotnet ef migrations add <Name>` — create a migration for the latest model changes.
- `dotnet ef migrations remove` — remove the last migration, only if it has not been applied yet.
- `dotnet ef database update` — apply pending migrations.
- `dotnet ef migrations script` — generate a SQL script, which is the safer way to review and apply changes in production instead of updating a production database directly.

## Good habits

- One logical change per migration, with a descriptive name.
- Always read the generated `Up` and `Down` methods — generated code can drop a column or lose data if a rename is read as a drop plus an add.
- Keep migrations in source control with the code that uses them.
- Never edit a migration that has already been applied to a shared database; add a new migration instead.
