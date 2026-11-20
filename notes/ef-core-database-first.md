# EF Core Database-First

Database-first means the database schema already exists and EF Core generates the entity classes and `DbContext` from it. It fits brownfield projects, legacy databases, and teams where DBAs own the schema.

## Scaffolding the Model

Use `dotnet ef dbcontext scaffold` with the provider package installed (`Microsoft.EntityFrameworkCore.SqlServer` for SQL Server):

```bash
dotnet ef dbcontext scaffold "Server=.;Database=ShopDb;Trusted_Connection=True;" \
    Microsoft.EntityFrameworkCore.SqlServer \
    --output-dir Models \
    --context-dir Data \
    --context ShopDbContext \
    --no-onconfiguring
```

The `--no-onconfiguring` flag keeps the connection string out of the generated context; register it in `Program.cs` instead:

```csharp
builder.Services.AddDbContext<ShopDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("ShopDb")));
```

## Regenerating After Schema Changes

Scaffolding overwrites generated files, so re-run the same command whenever a table, column, or relationship changes. Keep the command in a script (for example `scaffold.sh`) so every regeneration uses identical options.

```bash
## Re-scaffold after adding the Discount column to Products
dotnet ef dbcontext scaffold "$CONNECTION_STRING" \
    Microsoft.EntityFrameworkCore.SqlServer \
    --output-dir Models --context-dir Data --context ShopDbContext \
    --no-onconfiguring --force
```

Use `--tables` and `--schema` to scaffold only part of a large database, and `--data-annotations` if you prefer attributes over the fluent API.

## Handling Scaffolding Limitations

Scaffolding is a starting point, not a finished model. Know its rough edges:

- **No pluralization by default.** A `Products` table becomes an entity named `Products`, not `Product`. Rename entities manually, or plug in a pluralizer such as `EntityFrameworkCore.Scaffolding.Handlebars` or the `Microsoft.EntityFrameworkCore.Design` pluralizer.
- **No value conversions.** Columns like `tinyint` flags or JSON strings map to plain CLR types; add conversions in `OnModelCreating` or a partial class.
- **No navigation fix-ups beyond FKs.** Missing or unconventional foreign keys produce no navigation properties; add them by hand.
- **Generated code gets overwritten.** Never edit scaffolded files directly. Put custom configuration in a partial class in a separate file:

```csharp
public partial class ShopDbContext
{
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Products>()
            .Property(p => p.Price)
            .HasColumnType("decimal(18,2)");
    }
}
```

The generated context calls `OnModelCreatingPartial`, so your tweaks survive the next scaffold run.

## Key Takeaways

- Database-first fits legacy databases and DBA-owned schemas; code-first fits greenfield apps.
- `dotnet ef dbcontext scaffold` generates entities and the context from a live database.
- Store the scaffold command in a script and re-run it with `--force` after every schema change.
- Keep connection strings in configuration, not in the generated context.
- Scaffolding does not pluralize entity names by default; rename or use a pluralizer.
- Never edit generated files directly — put custom tweaks in partial classes using `OnModelCreatingPartial`.
