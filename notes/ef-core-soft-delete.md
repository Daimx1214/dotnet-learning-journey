# Soft Delete in EF Core (EF Core)

Hard-deleting a row destroys data forever. Soft delete keeps the row and marks it deleted with a flag — usually `IsDeleted` plus a `DeletedAt` timestamp. The row stays in the database for auditing, recovery, and reporting, while the application pretends it is gone. Almost every production system ends up needing this, so it is worth building in early.

## The Entity and the Global Query Filter

```csharp
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}

protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // Every query on Products automatically excludes soft-deleted rows.
    modelBuilder.Entity<Product>().HasQueryFilter(p => !p.IsDeleted);
}
```

A global query filter is applied to every LINQ query, including `Find`, `Include` results, and navigation loading. That is the whole point: no developer can accidentally list deleted products by forgetting a `Where` clause.

## Intercepting Deletes in SaveChanges

Calling `context.Products.Remove(product)` still issues a SQL `DELETE`. To make deletes soft by default, override `SaveChanges` and convert them:

```csharp
public override int SaveChanges()
{
    foreach (var entry in ChangeTracker.Entries<Product>())
    {
        if (entry.State == EntityState.Deleted)
        {
            // Turn the delete into an update of the flag.
            entry.State = EntityState.Modified;
            entry.Entity.IsDeleted = true;
            entry.Entity.DeletedAt = DateTime.UtcNow;
        }
    }

    return base.SaveChanges();
}
```

Do the same in `SaveChangesAsync` (or have one call the other). For several soft-deletable entities, use a shared interface (`ISoftDelete`) and loop over `ChangeTracker.Entries<ISoftDelete>()` instead of one concrete type.

## Restoring and Admin Bypass

Restoring is just clearing the flag. But a query filter hides the row, so you must bypass the filter to find it:

```csharp
// Admin view: include deleted rows.
var allProducts = await context.Products
    .IgnoreQueryFilters()
    .ToListAsync();

// Restore a deleted product.
var deleted = await context.Products
    .IgnoreQueryFilters()
    .FirstAsync(p => p.Id == id);

deleted.IsDeleted = false;
deleted.DeletedAt = null;
await context.SaveChangesAsync();
```

`IgnoreQueryFilters()` removes all filters on that query — including any tenant filter, so only use it in admin code paths that check authorization first.

## Pitfalls

Soft delete interacts with unique constraints: a deleted product still occupies its unique `Name` or `SKU`, so creating a replacement with the same name fails. The fix is a filtered unique index that only applies to live rows:

```sql
CREATE UNIQUE INDEX IX_Products_Sku_Active
ON Products (Sku) WHERE IsDeleted = 0;
```

Also remember that soft-deleted rows still count in foreign keys and still occupy storage — a background job should hard-delete rows that have been soft-deleted for, say, 90 days. And cascade deletes no longer cascade: deleting a parent soft-deletes only the parent, so decide explicitly whether children are soft-deleted with it.

## Key takeaways

- Add `IsDeleted` + `DeletedAt`, hide deleted rows with a global query filter, and convert `Remove` into a flag update in `SaveChanges`.
- Use `IgnoreQueryFilters()` only in admin paths, and for restoring.
- Unique constraints need filtered indexes (`WHERE IsDeleted = 0`), and old soft-deleted rows need a periodic hard-delete cleanup.
