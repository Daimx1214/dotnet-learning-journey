# EF Core Value Generation

Value generation is how EF Core decides the value of a property — usually a primary key — when a new entity is saved. The value can be produced by the database, by the client, or by a pattern such as a sequence or HiLo block. Choosing the right strategy affects insert performance, key predictability, and how your code behaves before `SaveChanges` runs.

## ValueGeneratedOnAdd

The simplest marker. Applied to a key property, it tells EF Core the value is produced when the row is inserted:

```csharp
public sealed class Order
{
    public int Id { get; set; }          // ValueGeneratedOnAdd by convention
    public DateTime PlacedAt { get; set; }
}
```

For an `int` key on SQL Server, EF Core maps this to an `IDENTITY(1,1)` column by default. The entity's `Id` is 0 until `SaveChanges` runs, and only afterwards does it hold the database-assigned value. That means you cannot rely on the key before saving.

## Sequences

A sequence is a database object that hands out numbers on demand, independent of any table:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.HasSequence<int>("OrderNumberSequence")
        .StartsAt(1)
        .IncrementsBy(10);

    modelBuilder.Entity<Order>()
        .Property(o => o.Id)
        .HasDefaultValueSql("NEXT VALUE FOR OrderNumberSequence");
}
```

Unlike `IDENTITY`, a sequence can be shared by several tables, and a value can be fetched before the insert (`SELECT NEXT VALUE FOR ...`), so the entity can know its key in advance.

### Worked Example: Sequence Starting at 1, Increment 10

With `StartsAt(1)` and `IncrementsBy(10)`, the first three keys handed out are:

```text
1, 11, 21
```

Checked by hand: the first value is the start, 1. Each next value adds the increment: 1 + 10 = 11, and 11 + 10 = 21. The fourth would be 31. Note the gaps: a sequence does not promise gapless numbers — rolled-back transactions and server restarts can skip values, so never use a sequence for invoice numbers that must be consecutive.

## HiLo

HiLo is a client-side pattern built on a sequence. The database sequence hands out a "high" value, and the client generates a block of "low" values from it, so most inserts need no database round trip:

```csharp
modelBuilder.Entity<Order>()
    .Property(o => o.Id)
    .UseHiLo("OrderHiLoSequence");
```

With a block size of 10, the client fetches high = 1 once and generates keys 1–10 locally; the next block starts at 11. HiLo gives the performance of client-generated keys with the uniqueness of a database sequence. The cost is the same as sequences: keys have gaps, and they are not strictly sequential in insert order if several application instances draw blocks concurrently.

## GUID Keys

GUIDs are the classic client-generated key — globally unique, generated without touching the database:

```csharp
public sealed class Document
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
}
```

Or let EF Core generate one on add:

```csharp
modelBuilder.Entity<Document>()
    .Property(d => d.Id)
    .HasDefaultValueSql("NEWID()")      // SQL Server generates it
    .ValueGeneratedOnAdd();
```

Better still for SQL Server, use `NEWSEQUENTIALID()` instead of `NEWID()`: sequential GUIDs keep index inserts near the end of the B-tree, while random GUIDs scatter inserts and fragment the index. A `Guid` key is known before `SaveChanges` when generated on the client, which simplifies building related entities in memory.

## Client vs Database Generation

| Strategy | Where generated | Key known before SaveChanges? | Gaps? |
|---|---|---|---|
| `IDENTITY` | Database | No | Yes (rollbacks, restarts) |
| Sequence | Database | Yes, if fetched first | Yes |
| HiLo | Client, from a sequence | Yes | Yes |
| GUID (`Guid.NewGuid`) | Client | Yes | n/a (random) |
| `NEWSEQUENTIALID()` | Database | No | n/a |

Client generation lets you build a whole object graph with real keys before saving. Database generation keeps keys compact and sequential but forces a round trip per insert (EF Core batches identity retrieval, so this is usually cheap) and leaves keys unknown until after the save.

## Which to Choose

- Default `int` identity: fine for most tables; simple and compact.
- Sequence: when several tables share one numbering scheme, or keys must be known before insert.
- HiLo: high-throughput inserts where a database round trip per key would hurt.
- GUID: distributed systems or offline-created entities that sync later; prefer sequential GUIDs on SQL Server to protect the index.

## Key Takeaways

- `ValueGeneratedOnAdd` marks a property whose value is produced at insert time; `int` keys default to `IDENTITY(1,1)` on SQL Server.
- Sequences hand out values independently of tables and can be shared; starting at 1 with increment 10 gives 1, 11, 21.
- HiLo fetches a "high" value once and generates a block of keys client-side, cutting database round trips.
- GUID keys can be generated on the client with `Guid.NewGuid()` and are known before saving; prefer sequential GUIDs on SQL Server.
- No strategy guarantees gapless keys — never use generated keys as gapless business numbers.
