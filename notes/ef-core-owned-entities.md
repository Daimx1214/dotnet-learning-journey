# EF Core Owned Entity Types (EF Core)

An owned entity type is a class that has no identity of its own — it only exists as part of its owner. The classic example is a value object like `Address`: an order *has* an address, but the address has no key and no meaning without the order. EF Core maps owned types into the owner's table by default (table splitting), so the database stays flat while the C# model stays expressive.

## Configuring an Owned Type

```csharp
public class Order
{
    public int Id { get; set; }
    public Address ShippingAddress { get; set; } = null!;
    public List<OrderLine> Lines { get; set; } = new();
}

// A value object: no Id, immutable-ish, compared by value.
public class Address
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}

protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Order>().OwnsOne(o => o.ShippingAddress, a =>
    {
        a.Property(x => x.Street).HasMaxLength(200).IsRequired();
        a.Property(x => x.City).HasMaxLength(100);
        // Rename columns so they don't collide with other owned types.
        a.Property(x => x.PostalCode).HasColumnName("ShippingPostalCode");
    });
}
```

With this configuration, `Orders` gets columns `ShippingAddress_Street`, `ShippingAddress_City`, `ShippingPostalCode`, and so on — one table, no join. `OwnsMany` works the same way for a collection of owned objects, except a collection is mapped to its own table, because one row cannot hold many values.

## Table Splitting

Table splitting is what `OwnsOne` does by default: two entity types share one table. You can also split two regular entities across one table with `ToTable`, but owned types are the idiomatic way — the ownership makes the relationship explicit, and EF Core handles the shared primary key for you.

```csharp
// The owned type can even live in its own table if you prefer.
modelBuilder.Entity<Order>().OwnsOne(o => o.ShippingAddress)
    .ToTable("OrderAddresses");
```

This costs a join on every query but keeps the `Orders` table narrow. Default to the shared table unless the owned data is wide and rarely needed.

## Querying Owned Types

Owned properties are queried like any navigation, and EF Core translates them to column references — no join is generated for a shared-table owned type.

```csharp
var orders = await context.Orders
    .Where(o => o.ShippingAddress.City == "Lahore")
    .ToListAsync();

// Owned values are tracked with the owner; changing one marks the owner modified.
var order = await context.Orders.FindAsync(1);
order!.ShippingAddress.City = "Multan";
await context.SaveChangesAsync();
```

## Limits to Know

An owned type cannot have its own `DbSet` and cannot be queried on its own — it is always reached through its owner. It has no key, so two owners can never share the same owned instance, and you cannot load an `Address` without loading its `Order`. An owned type also cannot own another owned type's collection in some edge cases, and inheritance on owned types is not supported. If an object needs an identity, a standalone query, or to be shared between owners, make it a regular entity with a foreign key instead.

## Key takeaways

- `OwnsOne` / `OwnsMany` map value objects like `Address` into the owner's table by default.
- Owned types have no key and no `DbSet`; they are always loaded, tracked, and saved with their owner.
- Use owned types for value objects that only make sense inside an owner; use a regular entity with a foreign key when the object needs its own identity or to be shared.
