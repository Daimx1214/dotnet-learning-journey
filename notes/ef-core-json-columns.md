# EF Core JSON Columns

EF Core 7 introduced mapping .NET types to JSON columns, and EF Core 8 extended it to SQL Server. Instead of creating a separate table for a small nested object, or serializing it yourself into a string column, you can store an owned type or a POCO as JSON inside a single column and still query into it with LINQ. This is a good fit for data that is always read and written together with its parent row: an address on a customer, settings on a user, line-item details on an order.

This matters for .NET backends because it removes a whole class of one-to-one tables and manual serialization code, while keeping the data queryable in the database instead of forcing you to load the whole entity and filter in memory.

## What a JSON column mapping looks like

The most common shape is an owned type mapped to JSON. The owned type has no table of its own; its properties live inside a JSON document in the parent table's column.

```csharp
public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    // Owned type: stored as JSON in Customers.Address.
    public Address Address { get; set; } = new();
}

public class Address
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}

public class AppDbContext : DbContext
{
    public DbSet<Customer> Customers => Set<Customer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>().OwnsOne(
            c => c.Address,
            owned => owned.ToJson());
    }
}
```

The migration creates a single `nvarchar(max)` column named `Address` on the `Customers` table, and EF Core serializes the `Address` object into it. A collection of owned types works the same way with `OwnsMany(...).ToJson()`, producing a JSON array in one column.

You can also map a plain (non-owned) POCO property directly to a JSON column in EF Core 8 and later:

```csharp
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    // Arbitrary data stored as JSON, no owned-type configuration needed.
    public Dictionary<string, string> Attributes { get; set; } = new();
}

// In OnModelCreating:
// modelBuilder.Entity<Product>()
//     .Property(p => p.Attributes)
//     .HasColumnType("nvarchar(max)");
```

## Querying JSON properties

The real win over manual serialization is that LINQ queries against JSON-mapped properties are translated to SQL, using SQL Server's `JSON_VALUE` and `OPENJSON` under the hood. You write normal LINQ; EF Core does the JSON path work.

```csharp
// Find all customers in Berlin — translated to JSON_VALUE on the column.
var berlinCustomers = await context.Customers
    .Where(c => c.Address.City == "Berlin")
    .ToListAsync();

// Project just the city out of the JSON document.
var cities = await context.Customers
    .Select(c => new { c.Name, City = c.Address.City })
    .ToListAsync();

// Filter on a value inside a JSON collection.
var withExpressTag = await context.Orders
    .Where(o => o.Tags.Any(t => t.Name == "Express"))
    .ToListAsync();
```

Because the filter runs in SQL Server, you do not load every row into memory. Note that these queries cannot use a normal B-tree index on the JSON properties — filtering inside JSON is a scan of the column values (or an index on a computed column, see Limitations).

## Updating JSON data

Updates work like any other owned type: change the property and call `SaveChanges`. EF Core serializes the whole owned object and writes the column back.

```csharp
var customer = await context.Customers.FirstAsync(c => c.Id == 1);
customer.Address.City = "Munich";
customer.Address.PostalCode = "80331";
await context.SaveChangesAsync();  // rewrites the whole Address JSON column
```

That last point is important: EF Core rewrites the entire JSON document on every change, even if you only changed one property. For a small address object this is irrelevant; for a large document updated constantly, a separate table with per-column updates is more efficient.

## Migrations

JSON mappings appear in migrations like any other column change. `ToJson()` produces an `nvarchar(max)` column, and changing the owned type (adding a property) produces an `AlterColumn` in the next migration — the existing JSON data is left untouched, and EF Core simply deserializes old documents with the new property at its default value.

```csharp
// Generated migration for the Address mapping above:
migrationBuilder.AddColumn<string>(
    name: "Address",
    table: "Customers",
    type: "nvarchar(max)",
    nullable: false,
    defaultValue: "{}");
```

One practical tip: give the JSON column a sensible default (`"{}"`) so existing rows are valid JSON from the start. If you add a JSON column to a table that already has rows, EF Core requires a default or the column must be nullable.

## Limitations

JSON columns are a convenience, not a replacement for relational tables. Know the boundaries:

- **No referential integrity inside JSON.** You cannot put a foreign key on a value inside a JSON document, and there is no constraint that a nested value is unique.
- **No indexes on JSON properties directly.** To index a frequently filtered JSON property, create a persisted computed column over `JSON_VALUE` and index that:

```sql
ALTER TABLE dbo.Customers
ADD AddressCity AS JSON_VALUE(Address, '$.City') PERSISTED;

CREATE INDEX IX_Customers_AddressCity ON dbo.Customers (AddressCity);
```

- **Whole-document writes.** Every update rewrites the full JSON value, so large documents updated often are wasteful compared to a table.
- **No partial updates in LINQ.** You cannot update one nested property with `ExecuteUpdate`; you must load the entity, change the property, and save.
- **Size and shape.** The column is `nvarchar(max)`, so a document can grow large — keep JSON-mapped objects small and bounded. Deeply nested structures and large collections belong in real tables.
- **Provider differences.** JSON column support, the generated SQL, and the exact querying capabilities vary by provider (SQL Server, PostgreSQL, SQLite). Code that relies on provider-specific JSON functions is not portable.

## When to use JSON columns

Use them when the nested data is small, always used together with the parent, never queried across rows in complex joins, and never needs its own identity or foreign keys — addresses, settings, preferences, tags, metadata. Use a real table when the nested data grows independently, is queried on its own, needs constraints, or is updated field-by-field at high frequency.

## Key Takeaways

- EF Core 7+ maps owned types (and in EF Core 8+, plain POCOs) to JSON columns with `ToJson()`, storing the object as `nvarchar(max)` in the parent table.
- LINQ queries against JSON-mapped properties are translated to SQL (`JSON_VALUE` / `OPENJSON`), so filtering happens in the database, not in memory.
- Updates rewrite the whole JSON document, so keep mapped objects small and update them infrequently.
- Migrations treat JSON columns like any other column; add a `"{}"` default when adding one to a table with existing rows.
- You cannot index or constrain values inside JSON directly — use a persisted computed column over `JSON_VALUE` for a frequently filtered property.
- Choose JSON columns for small, parent-bound, loosely structured data; choose real tables when the data needs constraints, indexes, or independent querying.
