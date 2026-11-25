# Hangfire Background Jobs

Hangfire runs background jobs inside an ASP.NET Core application with persistent storage, so a job survives an app restart and retries on failure. It is the usual choice when a task should not block the HTTP request that triggered it: sending an email, generating a report, or syncing data on a schedule.

## Setup with SQL Server Storage

Install `Hangfire.AspNetCore` and `Hangfire.SqlServer`, then register Hangfire and the dashboard:

```csharp
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(builder.Configuration.GetConnectionString("Hangfire")));

builder.Services.AddHangfireServer();

var app = builder.Build();
app.UseHangfireDashboard("/hangfire");
```

`AddHangfireServer` starts the background processing server in the same process. The dashboard at `/hangfire` shows enqueued, processing, succeeded, and failed jobs. Protect it with authorization in production: it can retry and delete jobs.

## Fire-and-Forget

A job that runs once, as soon as a worker is free:

```csharp
BackgroundJob.Enqueue(() => emailService.SendWelcomeEmail(user.Id));
```

The call returns immediately and the HTTP request continues. Hangfire serializes the method call (type, method, arguments) into storage, so the arguments must be serializable. Pass IDs, not entity objects: the job should reload the entity when it runs, because the object may be stale by then.

## Delayed Jobs

A job that runs once, after a delay:

```csharp
BackgroundJob.Schedule(
    () => reportService.GenerateMonthlyReport(user.Id),
    TimeSpan.FromHours(2));
```

## Recurring Jobs

A job that runs on a schedule, expressed as a Cron expression:

```csharp
RecurringJob.AddOrUpdate(
    "nightly-cleanup",
    () => cleanupService.DeleteExpiredSessions(),
    Cron.Daily(2, 0));
```

`AddOrUpdate` is idempotent: calling it again with the same ID updates the schedule instead of creating a duplicate. Common schedules are `Cron.Daily`, `Cron.Hourly`, `Cron.Weekly`, and `Cron.Monthly`. Hangfire keeps the schedule in storage, so the job survives restarts.

## Continuations

A job that runs after another job finishes:

```csharp
var parentId = BackgroundJob.Enqueue(() => orderService.ProcessOrder(order.Id));

BackgroundJob.ContinueJobWith(parentId,
    () => emailService.SendOrderConfirmation(order.Id));
```

The continuation only runs if the parent job succeeded. This chains steps without writing a state machine.

## Retries and Failures

A failed job is retried automatically with exponential backoff, by default up to 10 attempts over about 24 hours. Control this per job with an attribute:

```csharp
[AutomaticRetry(Attempts = 3)]
public void SendWelcomeEmail(int userId) { ... }
```

Jobs must be idempotent: a retry must be safe if the first attempt partly succeeded. Sending the same email twice is the classic failure, so record that the email was sent before sending, or use a unique idempotency key.

After the final failed attempt, the job moves to the Failed state and stays visible on the dashboard, where it can be retried manually.

## When to Use Hangfire vs a Hosted Service

A `BackgroundService` fits work that is always running, like consuming a queue in a loop. Hangfire fits discrete jobs triggered by requests or schedules, especially when retries, a dashboard, and persistence matter. For multiple servers, Hangfire's storage acts as the queue, so any server instance can pick up a job.

## Key Takeaways

- Hangfire persists jobs in storage (SQL Server here), so they survive restarts and retry automatically.
- `Enqueue` is fire-and-forget, `Schedule` delays a job, `AddOrUpdate` creates a recurring job, and `ContinueJobWith` chains jobs.
- Pass IDs into jobs, not entity objects, and reload data when the job runs.
- Jobs must be idempotent because retries can run a partly completed job again.
- Protect the dashboard with authorization; it can retry and delete jobs.
