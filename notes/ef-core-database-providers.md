# EF Core Database Providers

Entity Framework Core is database-agnostic at the model level: the same
`DbContext`, entities and LINQ queries can run against SQL Server, SQLite,
PostgreSQL, MySQL and more. What changes is the **provider** — the package
that translates EF Core's model and queries into a specific database's SQL
dialect and wire protocol.

## The Main Providers

| Provider | Package | Notes |
|----------|---------|-------|
| SQL Server | `Microsoft.EntityFrameworkCore.SqlServer` | The flagship; most features, best tooling |
| SQLite | `Microsoft.EntityFrameworkCore.Sqlite` | File-based, zero setup, great for tests and local dev |
| PostgreSQL | `Npgsql.EntityFrameworkCore.PostgreSQL` | Community provider, very feature-rich |
| MySQL / MariaDB | `Pomelo.EntityFrameworkCore.MySql` | Community provider (Pomelo) |
| Azure Cosmos DB | `Microsoft.EntityFrameworkCore.Cosmos` | NoSQL document store, different query model |
| InMemory | `Microsoft.EntityFrameworkCore.InMemory` | Not a real database — for tests only |

SQL Server and SQLite are maintained by Microsoft; PostgreSQL and MySQL
providers are community-maintained but first-class in practice.

## Switching Providers

The provider is chosen in `OnConfiguring` or, better, in
`AddDbContext` at startup. The model and queries stay the same:

```csharp
// SQL Server
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("Default")));

// SQLite — same context, one line changed
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("Local")));

// PostgreSQL
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("Postgres")));
```

Migrations are provider-specific: a migration generated for SQL Server cannot
be applied to PostgreSQL. Keep one migrations set per provider if you support
several, or generate migrations only for the production provider.

## Provider-Specific Features and Limits

Providers differ in what they support, and code that works on one can fail or
behave differently on another:

- **Identity / value generation:** SQL Server uses `IDENTITY`, PostgreSQL uses
  sequences/`GENERATED ALWAYS AS IDENTITY`, SQLite uses `AUTOINCREMENT`.
  `UseIdentityColumn()` in a migration only applies to SQL Server.
- **String comparison:** SQL Server is case-insensitive by default (collation),
  PostgreSQL is case-sensitive. The same `Where(p => p.Name == "x")` can return
  different rows on each.
- **Date/time types:** `DateOnly`/`TimeOnly` map cleanly on SQL Server and
  PostgreSQL, but SQLite stores them as text and loses some query translation.
- **Decimal precision:** SQL Server `decimal(18,2)` is the default; PostgreSQL
  has no fixed default precision and stores arbitrary precision.
- **Concurrency tokens:** `rowversion` exists only on SQL Server; on PostgreSQL
  use `xmin` or a `Guid`/`DateTime` token instead.
- **Cosmos DB:** no joins, no relational migrations, and LINQ support is a
  subset — partition keys and point reads matter more than indexes.

Rule of thumb: develop and test against the same provider you deploy to.
SQLite is fine for unit tests, but run integration tests on the real provider
before shipping.

## Connection Strings

```json
{
  "ConnectionStrings": {
    "Default": "Server=localhost;Database=ShopDb;Trusted_Connection=True;TrustServerCertificate=True",
    "Local": "Data Source=shop.db",
    "Postgres": "Host=localhost;Database=shopdb;Username=app;Password=secret"
  }
}
```

Keep secrets out of source control: use user secrets in development
(`dotnet user-secrets set "ConnectionStrings:Default" "..."`) and environment
variables or a vault in production. Never hard-code a password in
`OnConfiguring`.

## InMemory for Tests

The InMemory provider stores entities in plain .NET collections. It is fast
and needs no database server, but it is **not** a relational database:

```csharp
var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseInMemoryDatabase("TestDb")
    .Options;
```

- LINQ queries run in memory, so queries that fail translation on SQL Server
  silently "work" here — false confidence.
- No transactions, no constraints, no real SQL semantics.
- Useful for testing business logic on top of the context; for testing actual
  queries, prefer SQLite in-memory mode or Testcontainers with the real
  database.

## Key Takeaways

- A provider translates EF Core models and LINQ into one database's dialect;
  switching providers is usually a one-line change plus new migrations.
- SQL Server and SQLite are Microsoft-maintained; PostgreSQL (Npgsql) and
  MySQL (Pomelo) are strong community providers.
- Providers differ on identity columns, collations, date/time types, decimal
  precision and concurrency tokens — test on the deployment provider.
- Connection strings belong in configuration and secrets stores, never in code.
- InMemory is a test double, not a database: it hides query-translation bugs.
