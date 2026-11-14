# Shadow Properties in EF Core (EF Core)

A shadow property is a column that exists in the database and in the EF Core model, but has no matching property on the entity class. It is useful for data the application needs to track but the domain model should not carry — audit columns like `CreatedAt` and `UpdatedAt`, a foreign key you never want to expose, or a concurrency token managed by the infrastructure.

## Defining a Shadow Property

```csharp
public class Post
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    // No CreatedAt or UpdatedAt here — they live only in the model.
}

protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Post>()
        .Property<DateTime>("CreatedAt")
        .HasDefaultValueSql("GETUTCDATE()");

    modelBuilder.Entity<Post>()
        .Property<DateTime>("UpdatedAt")
        .HasDefaultValueSql("GETUTCDATE()");
}
```

The migration will contain `CreatedAt` and `UpdatedAt` columns, and EF Core will include them in INSERTs and UPDATEs, even though `Post` has no such properties. `HasDefaultValueSql("GETUTCDATE()")` lets the database fill the value on insert.

## Reading and Writing Shadow Properties

Because there is no CLR property, you access shadow values through the `ChangeTracker` entry or the static `EF.Property` method:

```csharp
// Read in a query — EF.Property translates to a column reference.
var recent = await context.Posts
    .Where(p => EF.Property<DateTime>(p, "CreatedAt") > DateTime.UtcNow.AddDays(-7))
    .ToListAsync();

// Read from a tracked entity.
var entry = context.Entry(post);
var createdAt = entry.Property<DateTime>("CreatedAt").CurrentValue;

// Write to a tracked entity.
entry.Property("UpdatedAt").CurrentValue = DateTime.UtcNow;
```

`EF.Property<T>` works inside LINQ queries because EF Core translates it to SQL. Outside a query, use the entry API shown above.

## Automatic Audit Columns

The most common use is stamping `CreatedAt` on insert and `UpdatedAt` on every change, inside `SaveChanges`:

```csharp
public override int SaveChanges()
{
    var now = DateTime.UtcNow;

    foreach (var entry in ChangeTracker.Entries<Post>())
    {
        if (entry.State == EntityState.Added)
        {
            entry.Property("CreatedAt").CurrentValue = now;
            entry.Property("UpdatedAt").CurrentValue = now;
        }
        else if (entry.State == EntityState.Modified)
        {
            entry.Property("UpdatedAt").CurrentValue = now;
        }
    }

    return base.SaveChanges();
}
```

For many entity types, define a marker interface or loop over all entries and check `entry.Metadata.FindProperty("CreatedAt") != null` before stamping, so entities without audit columns are skipped.

## When Not to Use Them

Shadow properties are invisible to the rest of the code, which is both their strength and their weakness. If application code regularly reads or filters by the value — a `LastLoginAt` shown in the UI, for example — make it a real property; hiding it forces every consumer through the awkward `EF.Property` syntax. Also, shadow properties do not appear in serialization, so an API that returns the entity will never include them. Use shadow properties for infrastructure concerns (auditing, soft-delete flags, hidden foreign keys), not for business data.

## Key takeaways

- Shadow properties are model-only columns, configured with `modelBuilder.Entity<T>().Property<Type>("Name")`.
- Read them in queries with `EF.Property<T>(entity, "Name")` and on tracked entities via `context.Entry(e).Property("Name")`.
- They are ideal for audit columns stamped in `SaveChanges`; make anything the business logic needs a real property instead.
