# EF Core Table Splitting

Table splitting maps two or more entity types to a single database table. The entities share the table's primary key, and each entity owns a subset of the columns. It is the right tool when a wide table should be read as smaller, focused entities — for example, an `Order` with core fields plus a rarely needed `OrderDetail` — without splitting the data across two tables and paying a join on every read.

## How It Works

Both entities are configured to the same table name with the same key. EF Core treats them as separate entities that happen to share a row: loading one does not load the other, and each is tracked independently. The relationship between them is one-to-one with a shared primary key.

```csharp
public sealed class Order
{
    public int Id { get; set; }
    public string Customer { get; set; } = "";
    public decimal Total { get; set; }
    public DateTimeOffset PlacedAt { get; set; }
    public OrderDetail? Detail { get; set; }
}

public sealed class OrderDetail
{
    public int Id { get; set; } // same value as Order.Id, shared PK
    public string ShippingAddress { get; set; } = "";
    public string? GiftMessage { get; set; }
    public string InternalNotes { get; set; } = "";
    public Order Order { get; set; } = null!;
}

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderDetail> OrderDetails => Set<OrderDetail>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(e =>
        {
            e.ToTable("Orders");
            e.HasOne(o => o.Detail)
                .WithOne(d => d.Order)
                .HasForeignKey<OrderDetail>(d => d.Id);
        });

        modelBuilder.Entity<OrderDetail>(e =>
        {
            e.ToTable("Orders"); // same table as Order
        });
    }
}
```

The migration creates a single `Orders` table with all columns: `Id`, `Customer`, `Total`, `PlacedAt`, `ShippingAddress`, `GiftMessage`, `InternalNotes`. There is no second table and no join.

## Reading and Writing

```csharp
// Loading an Order does NOT load its Detail — one row, partial columns.
Order? order = await db.Orders
    .FirstOrDefaultAsync(o => o.Id == 1);

// The Detail is loaded only when explicitly included.
Order? withDetail = await db.Orders
    .Include(o => o.Detail)
    .FirstOrDefaultAsync(o => o.Id == 1);

// Inserting both: EF Core inserts one row with all columns.
var newOrder = new Order
{
    Customer = "Daim",
    Total = 49.99m,
    PlacedAt = DateTimeOffset.UtcNow,
    Detail = new OrderDetail { ShippingAddress = "Multan, Pakistan" },
};
db.Orders.Add(newOrder);
await db.SaveChangesAsync(); // single INSERT into Orders
```

The performance point: without splitting, a query for a list of 1,000 orders selects all columns including the large `InternalNotes` text — if each note averages 2 KB, that is 2 MB of data transferred for a list view that never shows notes. With splitting, the list query selects only the four core columns (roughly 100 bytes per row, 100 KB total), a 20x reduction in data transferred. The detail is fetched only on the single-order page.

## When to Use It

Table splitting fits when: one entity has a natural hot/cold column split (frequently read core fields plus rarely read large fields), two bounded contexts share an aggregate root but need separate models, or a legacy wide table must be tamed without a schema migration. It does not fit when the two entities have independent lifecycles (use separate tables), when either side needs its own primary key values, or when the split is really two aggregates that should be separate tables.

## Limits and Rules

- Both entities must share the same primary key value; the dependent's key is also the foreign key to the principal.
- The dependent entity cannot exist without the principal — deleting the `Order` deletes the row, including the `Detail` columns.
- Concurrency tokens (`RowVersion`) can only live on one of the split entities, not both, because there is only one row.
- Required columns on the dependent entity make inserts fail unless the dependent is always created together with the principal; keep dependent columns nullable or always populate them.
- Table splitting is not the same as owned types: owned types are value objects without their own identity, while split entities have their own `DbSet` and can be queried independently.
- A split entity cannot be mapped to a table that already participates in table-per-hierarchy (TPH) inheritance for the same entity.

## Common Pitfalls

- Querying `db.OrderDetails` directly works, but it still reads the shared table — filtering details without the principal is a code smell that the model is wrong.
- Forgetting the `HasForeignKey<OrderDetail>(d => d.Id)` call produces two separate tables instead of one; the migration is the check — it must create exactly one table.
- Making a dependent column `NOT NULL` in the database while the application sometimes inserts the principal alone causes constraint violations on insert.
- Using table splitting to avoid a join that the query actually needs: if the detail is always loaded with the order, splitting adds complexity for no gain.

## Key Takeaways

- Table splitting maps two entities to one table with a shared primary key; each entity owns a subset of the columns and is tracked independently.
- The migration must create exactly one table — if it creates two, the `HasForeignKey` configuration is missing.
- The win is I/O: a list query that skips 2 KB of rarely used text per row transfers ~20x less data for 1,000 rows.
- The dependent cannot exist without the principal, and concurrency tokens can only live on one side.
- Use it for hot/cold column splits on one aggregate; use separate tables when the entities have independent lifecycles.
