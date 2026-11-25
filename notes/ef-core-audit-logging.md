# EF Core Audit Logging

Audit logging records who changed what and when. In EF Core there are two natural places to do it: the `ChangeTracker`, which already knows every pending change, and a `SaveChanges` interceptor, which runs around the save. Both work; the interceptor is cleaner for cross-cutting logging.

## What the ChangeTracker Already Knows

Before `SaveChanges` runs, every tracked entity has an `EntityEntry` with its state (`Added`, `Modified`, `Deleted`, `Unchanged`) and, for modified entities, the old and new value of each property:

```csharp
foreach (var entry in context.ChangeTracker.Entries())
{
    if (entry.State == EntityState.Unchanged) continue;
    foreach (var property in entry.Properties.Where(p => p.IsModified))
    {
        Console.WriteLine($"{entry.Entity.GetType().Name}.{property.Metadata.Name}: {property.OriginalValue} -> {property.CurrentValue}");
    }
}
```

`OriginalValue` is the value as loaded from the database, and `CurrentValue` is the value about to be saved. That pair is the core of an audit record. For `Added` entities there is no original value; for `Deleted` entities the current values are the ones being removed.

## Approach 1: Override SaveChanges

The simplest approach is to override `SaveChangesAsync` in the `DbContext` and build audit records before calling the base method:

```csharp
public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
{
    var auditEntries = BuildAuditEntries(); // reads ChangeTracker, skips AuditLog entities
    int result = await base.SaveChangesAsync(cancellationToken);
    await SaveAuditEntriesAsync(auditEntries); // separate call, or it would recurse
    return result;
}

private List<AuditEntry> BuildAuditEntries() =>
    ChangeTracker.Entries()
        .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
        .Select(e => new AuditEntry
        {
            EntityName = e.Entity.GetType().Name,
            Action = e.State.ToString(),
            ChangedAt = DateTime.UtcNow,
            ChangedBy = _currentUserService.UserId,
            Changes = DescribeChanges(e)
        })
        .ToList();
```

## Approach 2: SaveChanges Interceptor

An interceptor keeps the `DbContext` clean and applies the same logic to every context that registers it:

```csharp
public sealed class AuditInterceptor(ICurrentUserService currentUser) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is { } context)
        {
            foreach (var entry in context.ChangeTracker.Entries()
                         .Where(e => e.State != EntityState.Unchanged))
            {
                // Build and queue an AuditLog entity here.
            }
        }
        return base.SavingChanges(eventData, result);
    }
}
```

Register it in `OnConfiguring` or in `AddDbContext` with `options.AddInterceptors(new AuditInterceptor(currentUserService));`. Override `SavingChangesAsync` for the async path. Prefer the interceptor when several contexts share the rule, and the `SaveChanges` override when the logic is specific to one context.

## Capturing Old and New Values

Serialize the changed properties into a readable form. JSON is the common choice because it stores a variable set of fields in one column. Skip properties that never belong in an audit trail, such as passwords, tokens, and large binary columns; redact or hash sensitive values instead of storing them in plain text.

```csharp
private static string DescribeChanges(EntityEntry entry) =>
    JsonSerializer.Serialize(entry.Properties
        .Where(p => p.IsModified || entry.State == EntityState.Added)
        .ToDictionary(
            p => p.Metadata.Name,
            p => new
            {
                Old = entry.State == EntityState.Added ? null : p.OriginalValue,
                New = p.CurrentValue
            }));
```

## Who and When Columns

Every audited table benefits from four columns, and EF Core can fill them automatically. Shadow properties keep them out of the entity class while still persisting them:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    foreach (var entityType in modelBuilder.Model.GetEntityTypes())
    {
        var entity = modelBuilder.Entity(entityType.ClrType);
        entity.Property<DateTime>("CreatedAt").HasDefaultValueSql("SYSUTCDATETIME()");
        entity.Property<string>("CreatedBy").HasMaxLength(100);
        entity.Property<DateTime>("UpdatedAt");
        entity.Property<string>("UpdatedBy").HasMaxLength(100);
    }
}
```

Then set them in the interceptor or the `SaveChanges` override before the save:

```csharp
foreach (var entry in context.ChangeTracker.Entries())
{
    if (entry.State == EntityState.Added)
    {
        entry.Property("CreatedAt").CurrentValue = DateTime.UtcNow;
        entry.Property("CreatedBy").CurrentValue = _currentUser.UserId;
    }
    if (entry.State is EntityState.Added or EntityState.Modified)
    {
        entry.Property("UpdatedAt").CurrentValue = DateTime.UtcNow;
        entry.Property("UpdatedBy").CurrentValue = _currentUser.UserId;
    }
}
```

## Key Takeaways

- The `ChangeTracker` already holds old and new values for every modified property, so audit logging is mostly a matter of reading what EF Core knows.
- A `SaveChanges` interceptor keeps audit logic out of the `DbContext` and applies it to every registered context; a `SaveChanges` override is simpler for one context.
- Serialize changed properties to JSON for a flexible audit record, and redact passwords and tokens before storing them.
- Shadow properties add `CreatedAt`, `CreatedBy`, `UpdatedAt`, and `UpdatedBy` columns without polluting entity classes.
- Store all audit timestamps in UTC and take the user id from the ambient identity service, not from the entity.
