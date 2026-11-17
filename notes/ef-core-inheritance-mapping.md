# EF Core Inheritance Mapping (EF Core)

When a C# class hierarchy has to live in a relational database, EF Core offers three strategies for mapping it to tables: table-per-hierarchy (TPH), table-per-type (TPT), and table-per-class (TPC). They differ in how many tables are created, how much SQL a query needs, and how much storage is wasted — and the default is not always the right choice.

## Table-per-Hierarchy (TPH): One Table, One Discriminator

TPH is the EF Core default: the whole hierarchy lives in a single table, with a `Discriminator` column recording which .NET type each row is, and nullable columns for every property that belongs to only one subtype.

```csharp
public abstract class Payment { public int Id { get; set; } public decimal Amount { get; set; } }
public class CardPayment : Payment { public string CardNumber { get; set; } = ""; }
public class CashPayment : Payment { public string ReceiptNumber { get; set; } = ""; }

// TPH is the default — this configuration is only needed to rename the discriminator.
modelBuilder.Entity<Payment>()
    .HasDiscriminator<string>("PaymentType")
    .HasValue<CardPayment>("Card")
    .HasValue<CashPayment>("Cash");
```

The resulting table has columns `Id`, `Amount`, `PaymentType`, `CardNumber` (NULL for cash rows), and `ReceiptNumber` (NULL for card rows). Querying the base type is a single-table scan — `SELECT * FROM Payments` — and querying one subtype just adds `WHERE PaymentType = 'Card'`. The cost is wasted space and weak constraints: subtype columns must be nullable even when the property is required, so the database cannot enforce that a card payment always has a card number.

## Table-per-Type (TPT): One Table per Type

TPT gives every type in the hierarchy its own table: the base table holds the shared columns, and each subtype table holds its key (also a foreign key to the base table) plus its own columns.

```csharp
modelBuilder.Entity<Payment>().ToTable("Payments");
modelBuilder.Entity<CardPayment>().ToTable("CardPayments");
modelBuilder.Entity<CashPayment>().ToTable("CashPayments");
```

The schema is clean and normalized — no NULL columns, and subtype properties can be non-nullable. The price is paid at query time: reading a `CardPayment` requires a JOIN between `Payments` and `CardPayments`, and reading all `Payment`s requires a JOIN across every table in the hierarchy (or a `UNION ALL`). On wide hierarchies with many rows, TPT queries are measurably slower than TPH, and inserts must touch two tables. Use TPT when the hierarchy is small, the subtype tables are queried separately most of the time, or the database schema is shared with other tools that expect normalized tables.

## Table-per-Class (TPC): One Table per Concrete Type

TPC (available since EF Core 7) stores each *concrete* type in its own complete table, duplicating the base-class columns into every table. There is no table for the abstract base type at all.

```csharp
modelBuilder.Entity<Payment>().UseTpcMappingStrategy();
```

A `CardPayment` row lives entirely in `CardPayments` (`Id`, `Amount`, `CardNumber`); a `CashPayment` row entirely in `CashPayments`. Queries for a single subtype are single-table and fast — no JOIN, no discriminator filter — but querying the base type needs a `UNION ALL` over every concrete table, and identity values must be unique *across* tables, so EF Core uses sequences (or HiLo) for keys instead of per-table identity columns. TPC fits hierarchies where subtypes are queried independently and rarely together, and where the number of concrete types is small and stable.

## Choosing Between Them

| Aspect | TPH | TPT | TPC |
|--------|-----|-----|-----|
| Tables | 1 for the whole hierarchy | 1 per type | 1 per concrete type |
| Query base type | Single-table scan | JOIN across all tables | UNION ALL across all tables |
| Query one subtype | Single table + discriminator filter | JOIN of 2 tables | Single table |
| NULL columns | Yes, for subtype properties | No | No |
| Key generation | Simple identity | Simple identity | Sequence/HiLo (unique across tables) |
| Best for | Most cases; polymorphic queries | Normalized schema, subtype-heavy reads | Subtype-heavy reads, few concrete types |

The practical rule: start with TPH (the default) unless you have a concrete reason not to. Polymorphic queries — "all payments, whatever their type" — are the common case in application code, and TPH answers them with the simplest SQL. Move to TPT when the schema must be normalized or subtype columns must be non-nullable, and to TPC when subtypes are almost always queried alone and the hierarchy is small. Switching strategies later is a migration, not a rewrite, but it reshuffles the data — decide before the tables fill up.

## Key Takeaways

- TPH (the default) stores the whole hierarchy in one table with a `Discriminator` column — simplest SQL, but subtype columns are always nullable.
- TPT gives each type its own table with a shared key — normalized and clean, but base-type queries need JOINs across the hierarchy.
- TPC (EF Core 7+) gives each concrete type a complete table — fast subtype queries, but base-type queries need `UNION ALL` and keys must be unique across tables.
- Querying the base type is cheapest in TPH; querying one subtype is cheapest in TPC.
- Start with TPH unless a concrete requirement (normalization, non-nullable subtype columns, subtype-only reads) pushes you elsewhere.
- Changing strategy later is possible via migration, but it reshuffles the data — choose before the tables grow large.
