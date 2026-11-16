# EF Core Data Seeding (EF Core)

Data seeding means putting reference or lookup data into the database as part of the model, so every fresh database — a developer's machine, a test run, a new environment — starts with the same baseline rows. Typical seed data is small and stable: roles, countries, product categories, default settings. EF Core's built-in mechanism is `HasData` in `OnModelCreating`, and it is driven by migrations rather than by runtime code.

## HasData: Model Seeding

`HasData` declares seed entities as part of the model configuration. When you add a migration, EF Core generates `InsertData` calls for the seeded rows; applying the migration inserts them.

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Role>().HasData(
        new Role { Id = 1, Name = "Admin" },
        new Role { Id = 2, Name = "Editor" },
        new Role { Id = 3, Name = "Reader" });

    modelBuilder.Entity<Category>().HasData(
        new Category { Id = 1, Name = "Books" },
        new Category { Id = 2, Name = "Electronics" });
}
```

Every seeded entity needs an explicit primary key — EF Core cannot generate one for seed data, because the migration must know the exact values to insert. Seeded rows are also tracked by the migration snapshot, so changing a seed value produces a migration that updates the row.

## Seeding Related Data

Related entities are seeded by setting their foreign key properties explicitly, not by assigning navigation properties. EF Core ignores navigation properties in `HasData` entirely.

```csharp
modelBuilder.Entity<Product>().HasData(
    new Product { Id = 1, Name = "Clean Code", Price = 34.99m, CategoryId = 1 },
    new Product { Id = 2, Name = "USB-C Hub", Price = 49.00m, CategoryId = 2 });

// For many-to-many join entities, seed the join rows directly:
modelBuilder.Entity<ProductTag>().HasData(
    new ProductTag { ProductId = 1, TagId = 1 },
    new ProductTag { ProductId = 1, TagId = 2 });
```

The order of the `HasData` calls does not matter — EF Core sorts the inserts by dependency — but the foreign key values must match seeded (or already existing) principal rows, or the migration will fail on the constraint.

## Updating Seeded Data

To change a seeded row, edit the `HasData` call and add a migration. EF Core compares the new seed values with the snapshot and emits `UpdateData` for changed columns. To remove a row, delete the `HasData` entry and add a migration, which emits `DeleteData`.

```bash
dotnet ef migrations add UpdateRoleNames
dotnet ef database update
```

One trap: if a user or an admin edits a seeded row in production, the next migration that touches that seed data will overwrite the edit back to the seeded value. Seed data is *owned by the model*, so it should only hold values the application itself controls. User-editable lookup data belongs in a separate mechanism.

## Model Seeding vs Runtime Seeding

`HasData` is not the only option. Runtime seeding — inserting rows in code at startup when the database is empty — fits data that is generated, environment-specific, or user-editable. The two approaches answer different questions.

| Aspect | HasData (model seeding) | Runtime seeding |
|--------|------------------------|-----------------|
| When it runs | At migration time | At application startup |
| Source of truth | The model and migration snapshot | Code that checks and inserts |
| Good for | Fixed lookup data: roles, categories | Generated data, dev/test fixtures, environment-specific rows |
| Updating | Edit `HasData`, add a migration | Change the code; it runs on next start |
| Risk | Overwrites manual edits on the next migration | Runs on every start — must be idempotent |

A common pattern is to use both: `HasData` for the truly fixed reference data, and a small idempotent seeder (guarded by `if (!db.Roles.Any())`) for development fixtures and demo content.

## Limitations

- **Explicit keys are mandatory.** Seeded entities cannot use database-generated keys, identity columns, or GUIDs generated at insert time.
- **No navigation properties.** Only scalar and foreign key properties are read; assigning a navigation object is silently ignored.
- **No generated values.** Computed columns, `ValueGeneratedOnAdd` properties, and shadow properties cannot be seeded.
- **Large data sets are a poor fit.** Thousands of rows bloat the migration snapshot and slow down `dotnet ef` commands; use a bulk insert or a SQL script instead.
- **Seeding happens per migration.** The seed rows live in the migration history, so a database created with `EnsureCreated()` (no migrations) will not contain them unless the seeder runs separately.

## Key Takeaways

- `HasData` in `OnModelCreating` declares seed data as part of the model; migrations insert, update, and delete the rows.
- Seeded entities need explicit primary keys, and related data is seeded through foreign key values — navigation properties are ignored.
- Changing a seed value means editing `HasData` and adding a migration; the next migration overwrites any manual edits to seeded rows.
- Use model seeding for fixed lookup data and runtime seeding (idempotent, checked at startup) for generated or environment-specific data.
- Keep seed sets small: large data belongs in bulk inserts or SQL scripts, not in the migration snapshot.
