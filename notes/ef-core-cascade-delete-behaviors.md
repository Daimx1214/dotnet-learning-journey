# EF Core Cascade Delete Behaviors

Deleting a principal entity, such as an `Order`, raises a question: what
happens to its dependents, such as `OrderLine` rows? EF Core answers with a
delete behavior, configured per relationship.

## The DeleteBehavior Enum

`DeleteBehavior` has seven members, in two families:

```csharp
public enum DeleteBehavior
{
    Cascade,        // Delete dependents, in the database or in memory
    Restrict,       // Block the delete if dependents exist
    SetNull,        // Set the dependent foreign key to NULL
    NoAction,       // Do nothing; the database decides
    ClientCascade,  // Delete dependents in memory only (EF Core side)
    ClientSetNull,  // Set the FK to NULL in memory only
    ClientNoAction  // Do nothing, in memory or in the database
}
```

The `Cascade`, `Restrict`, `SetNull`, and `NoAction` values are enforced by
the database through foreign key actions. The `Client*` values are enforced
only by EF Core's change tracker when entities are loaded.

## Required vs Optional Relationships and Their Defaults

The default depends on whether the foreign key is required or optional:

- Required relationship: the default is `Cascade`. Deleting the principal
  deletes the dependents.
- Optional relationship: the default is `ClientSetNull`. Deleting the
  principal sets the loaded dependents' foreign keys to `null`.

```csharp
public class Order
{
    public int Id { get; set; }
    public List<OrderLine> Lines { get; set; } = new();
}
public class OrderLine
{
    public int Id { get; set; }
    public int OrderId { get; set; }        // Required FK: default Cascade
    public int? DiscountId { get; set; }    // Optional FK: default ClientSetNull
}
```

If you delete an `Order` while its `OrderLine` rows are loaded, EF Core
marks the lines for deletion. If they are not loaded, the database cascade
handles it. For the optional `DiscountId`, deleting a `Discount` only nulls
the key on loaded lines.

## Configuring With OnDelete

Use `OnDelete` in `OnModelCreating` to override the default:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Order>()
        .HasMany(o => o.Lines)
        .WithOne(l => l.Order)
        .HasForeignKey(l => l.OrderId)
        .OnDelete(DeleteBehavior.Restrict);
    modelBuilder.Entity<OrderLine>()
        .HasOne(l => l.Discount)
        .WithMany()
        .HasForeignKey(l => l.DiscountId)
        .OnDelete(DeleteBehavior.SetNull);
}
```

With `Restrict`, deleting an `Order` that still has lines fails with a
foreign key violation. This is the safe choice for data you must never lose silently, such as invoices linked to payments.

## Cycles and Multiple Cascade Paths in SQL Server

SQL Server refuses a foreign key with `ON DELETE CASCADE` when it creates a
cycle or multiple cascade paths to the same table. A common example is a
`User` who is both author and reviewer of a `Document`:

```csharp
modelBuilder.Entity<Document>()
    .HasOne(d => d.Author)
    .WithMany()
    .OnDelete(DeleteBehavior.Cascade);
modelBuilder.Entity<Document>()
    .HasOne(d => d.Reviewer)
    .WithMany()
    .OnDelete(DeleteBehavior.Cascade);
```

SQL Server rejects this migration with the multiple cascade paths error,
because deleting one user could reach the same `Document` row twice. EF Core
detects this for SQL Server and demotes one cascade to `Restrict`, but relying
on that silent change is fragile. Fix it explicitly: keep one path as `Cascade`
and set the other to `Restrict`, or make one relationship optional.

## Client Behaviors in Practice

`ClientCascade` and `ClientSetNull` only affect entities the change tracker
knows about. If you delete a principal without loading its dependents, the
delete either fails on the foreign key constraint or leaves orphans. Prefer
database-enforced behaviors for data integrity, and use client behaviors only
when the database cannot express what you need.

## Deleting Without Loading Dependents

When dependents are not loaded and the database cascade is in place, a
single `Remove` plus `SaveChanges` deletes everything in one round trip.
`ExecuteDeleteAsync` bypasses the change tracker, so only the database cascade runs.

## Key Takeaways

- `DeleteBehavior` splits into database-enforced values (`Cascade`, `Restrict`, `SetNull`, `NoAction`) and client-only values (`ClientCascade`, `ClientSetNull`, `ClientNoAction`).
- Required relationships default to `Cascade`; optional ones default to `ClientSetNull`.
- SQL Server rejects multiple cascade paths to the same table, so configure one path explicitly as `Restrict` or make it optional.
- Use `Restrict` for data that must never be deleted silently, and `Cascade` only where dependents are truly owned by the principal.
- Client behaviors only affect loaded entities; database behaviors protect data even when dependents are not loaded.
