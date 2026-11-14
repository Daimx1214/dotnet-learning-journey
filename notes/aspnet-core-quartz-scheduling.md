# Scheduling Background Jobs with Quartz.NET in ASP.NET Core (ASP.NET Core)

ASP.NET Core has no built-in cron scheduler. `IHostedService` and `BackgroundService` can run a loop with `Task.Delay`, but they cannot express "run at 2 AM every day" or "run every 15 minutes, skipping missed runs". Quartz.NET is the standard library for that: a mature job scheduler with cron expressions, durable triggers, and persistence options.

## The Core Concepts

A **job** is a unit of work — a class implementing `IJob` with a single `Execute` method. A **trigger** decides *when* the job runs: once, on an interval, or on a cron schedule. The **scheduler** ties jobs and triggers together and runs them, optionally in parallel.

```csharp
using Quartz;

// A job is just a class with one method.
public class CleanupJob : IJob
{
    private readonly ILogger<CleanupJob> _logger;

    public CleanupJob(ILogger<CleanupJob> logger) => _logger = logger;

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("Cleanup started at {Time}", DateTimeOffset.UtcNow);
        // Delete expired sessions, old temp files, stale tokens...
        await Task.Delay(100); // Replace with real work.
        _logger.LogInformation("Cleanup finished");
    }
}
```

Jobs are resolved from the DI container, so constructor injection works exactly like in a controller. A job instance is created fresh for each execution, so do not store state in fields.

## Registering Quartz in Program.cs

```csharp
using Quartz;

builder.Services.AddQuartz(q =>
{
    q.UseMicrosoftDependencyInjectionJobFactory();

    // Register the job with an identity.
    q.AddJob<CleanupJob>(opts => opts.WithIdentity("cleanup-job"));

    // Trigger it daily at 02:00 UTC with a cron expression.
    q.AddTrigger(opts => opts
        .ForJob("cleanup-job")
        .WithIdentity("cleanup-trigger")
        .WithCronSchedule("0 0 2 * * ?"));
});

// Starts the scheduler with the host and shuts it down gracefully.
builder.Services.AddQuartzHostedService(q =>
{
    q.WaitForJobsToComplete = true;
});
```

`AddQuartzHostedService` wires the scheduler into the host lifetime. `WaitForJobsToComplete = true` means a graceful shutdown waits for a running job to finish instead of killing it mid-work.

## Cron Expressions in Quartz

Quartz cron has seven fields: `seconds minutes hours day-of-month month day-of-week [year]`. The `?` means "no specific value" and is required in one of the two day fields.

```csharp
"0 0 2 * * ?"      // Every day at 02:00:00
"0 0/15 * * * ?"   // Every 15 minutes
"0 30 9 ? * MON-FRI" // Weekdays at 09:30
"0 0 0 1 * ?"      // First day of every month at midnight
```

A common mistake is copying a Unix cron expression (five fields, no seconds) into Quartz — it will throw a format exception. Always include the seconds field first.

## Interval Triggers and Misfires

For simple intervals, a `SimpleTrigger` is clearer than cron:

```csharp
q.AddTrigger(opts => opts
    .ForJob("cleanup-job")
    .WithIdentity("cleanup-interval-trigger")
    .StartNow()
    .WithSimpleSchedule(x => x
        .WithIntervalInMinutes(30)
        .RepeatForever()));
```

If the app is down when a trigger should have fired, Quartz applies a **misfire instruction**. The default for cron triggers is "fire once now, then continue the schedule" — usually what you want. For a job that must never run twice at once, add `[DisallowConcurrentExecution]` to the job class; a second trigger fire then waits (or misfires) instead of overlapping.

## When to Use Quartz

Use Quartz when jobs need a real schedule: nightly cleanup, weekly reports, polling an external API on a timetable. For a single fire-and-forget task inside a request, `BackgroundService` or a simple hosted service is lighter. For durable, at-least-once jobs that must survive restarts, configure Quartz with a database job store (`UsePersistentStore`) instead of the default in-memory store — in-memory triggers are lost when the app restarts.

## Key takeaways

- A job is an `IJob`; a trigger is the schedule; `AddQuartz` + `AddQuartzHostedService` wire both into the app.
- Quartz cron starts with seconds: `"0 0 2 * * ?"` runs daily at 2 AM.
- Jobs are resolved from DI, so inject services normally, and mark jobs `[DisallowConcurrentExecution]` when overlapping runs would corrupt data.
