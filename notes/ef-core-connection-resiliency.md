# EF Core Connection Resiliency

Connection resiliency makes EF Core automatically retry a database command that fails with a transient error — a brief network blip, a failover, a throttled Azure SQL connection — instead of surfacing the failure to the caller immediately.

## EnableRetryOnFailure

Resiliency is configured on the provider options, per `DbContext`. For SQL Server it is `EnableRetryOnFailure`; other providers expose the same method with their own defaults.

```csharp
services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        configuration.GetConnectionString("Default"),
        sqlOptions => sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorNumbersToAdd: null)));
```

With no arguments, `EnableRetryOnFailure()` uses the SQL Server defaults: `maxRetryCount` 6 and `maxRetryDelay` 30 seconds. Retries use exponential backoff, so the wait grows between attempts and the total worst-case wait stays bounded by the delay cap.

## Execution Strategies

`EnableRetryOnFailure` installs `SqlServerRetryingExecutionStrategy`. The strategy decides, per exception, whether the failure is transient (worth retrying) or permanent (fail fast). SQL error numbers such as 4060 (cannot open database), 40197 (service busy), and 40501 (throttling) are treated as transient; a constraint violation or syntax error is not.

```csharp
// The strategy can also be used explicitly around a block of work:
var strategy = context.Database.CreateExecutionStrategy();

await strategy.ExecuteAsync(async () =>
{
    await using var transaction = await context.Database.BeginTransactionAsync();
    // ... work ...
    await transaction.CommitAsync();
});
```

Anything inside `ExecuteAsync` may run more than once, so the delegate must be idempotent or wrapped in a transaction the strategy controls.

## Retry Counts and Delays

The numbers that matter are attempts, not just retries: total attempts = 1 initial attempt + `maxRetryCount` retries.

```csharp
// maxRetryCount: 5  =>  up to 6 attempts total (1 + 5)
// maxRetryDelay: 10s => no single wait between attempts exceeds 10 seconds
sqlOptions.EnableRetryOnFailure(
    maxRetryCount: 5,
    maxRetryDelay: TimeSpan.FromSeconds(10),
    errorNumbersToAdd: new[] { 1205 }); // add deadlock victim (1205) as transient
```

Adding error 1205 (deadlock victim) is a common choice: a deadlock is transient in the sense that re-running the same work usually succeeds. Do not add error numbers for permanent failures — retrying them only delays the inevitable error.

## Transactions and Resiliency Interaction

This is the sharpest edge in the feature. The default execution strategy retries only single commands. If user code starts a transaction with `BeginTransaction` and then a command inside it fails transiently, the strategy cannot safely replay the whole transaction — the earlier commands in that transaction have already run and are not replayed.

```csharp
// WRONG: the retry strategy cannot replay this transaction if the
// second SaveChanges fails transiently — the first one is not re-run.
await using var tx = await context.Database.BeginTransactionAsync();
context.Orders.Add(order);
await context.SaveChangesAsync();          // succeeds
context.AuditLogs.Add(audit);
await context.SaveChangesAsync();          // transient failure here: no retry of the whole tx
await tx.CommitAsync();

// RIGHT: let the execution strategy own the transaction so the
// entire delegate (including BeginTransaction) is replayed on retry.
var strategy = context.Database.CreateExecutionStrategy();
await strategy.ExecuteAsync(async () =>
{
    await using var tx = await context.Database.BeginTransactionAsync();
    context.Orders.Add(order);
    await context.SaveChangesAsync();
    context.AuditLogs.Add(audit);
    await context.SaveChangesAsync();
    await tx.CommitAsync();
});
```

The same rule applies to `SaveChanges` with multiple statements: EF Core wraps a single `SaveChanges` call in its own transaction, which the strategy can replay safely. It is only an explicit, user-owned transaction outside `ExecuteAsync` that breaks.

## Worked Example: Counting Attempts by Hand

Configure `EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10))` and run one `SaveChangesAsync` against a database that drops the connection twice and then recovers:

- Attempt 1: fails with a transient error (retry 1 of 5 used).
- Attempt 2: fails with a transient error (retry 2 of 5 used).
- Attempt 3: succeeds.

Total attempts = **3**, total retries used = **2**, retries remaining = 5 − 2 = **3**. If the database had stayed down, the run would have stopped after attempt 1 + 5 = **6** attempts and then thrown the last exception to the caller. The arithmetic to remember is that the cap counts retries, so the maximum number of times the command actually executes is always `maxRetryCount + 1`.

## DbContext Configuration Example

A complete, minimal setup with resiliency and a command timeout:

```csharp
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options) { }

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
}

// Program.cs
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("Default"),
        sql => sql
            .EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: new[] { 1205 })
            .CommandTimeout(30)));
```

Keep `maxRetryCount` small (3–6). A large count turns a genuinely dead database into a request that hangs for minutes before failing, which is worse than a fast, honest error.

## Key Takeaways

- `EnableRetryOnFailure` retries only transient failures, with exponential backoff capped by `maxRetryDelay`; defaults are 6 retries and a 30-second cap.
- Total executions are always `maxRetryCount + 1`, because the cap counts retries, not attempts.
- Never open a transaction outside `CreateExecutionStrategy().ExecuteAsync` when resiliency is on — the strategy cannot replay a user-owned transaction.
- Add transient error numbers deliberately (for example, deadlock 1205); never retry permanent errors.
- Resiliency hides brief outages; it does not fix a dead database, so keep retry counts small and let monitoring see the failures.
