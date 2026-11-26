# Background Jobs in ASP.NET Core: Comparing the Options

A web request should finish fast. Work that takes longer, sending emails, generating reports, syncing data, belongs in the background. ASP.NET Core offers four main ways to run it, and the right choice depends on durability, scheduling, and retries, not on which one is easiest to start with.

## Option 1: IHostedService and BackgroundService

`IHostedService` is the built-in interface for work that runs with the application lifetime. `BackgroundService` is the abstract base class that implements it for long-running loops:

```csharp
public sealed class EmailWorker : BackgroundService
{
    private readonly Channel<EmailMessage> _queue;

    public EmailWorker(Channel<EmailMessage> queue) => _queue = queue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var email in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            Console.WriteLine($"Sending email to {email.To}");
        }
    }
}
```

Register it with `builder.Services.AddHostedService<EmailWorker>()`. The worker starts with the app and stops gracefully when the app shuts down, because `ExecuteAsync` receives the stopping token.- **Durability**: none. Jobs live in memory; a restart or a crash loses everything in the queue.
- **Retries**: you write them yourself.
- **Scheduling**: you write the loop and the delay, usually with `Task.Delay` or `PeriodicTimer`.
- **Best for**: lightweight in-process work that can be lost safely, such as cache refreshes or fire-and-forget notifications.## Option 2: Channels

`System.Threading.Channels` gives a producer/consumer queue that pairs naturally with a `BackgroundService`:

```csharp
var channel = Channel.CreateBounded<EmailMessage>(new BoundedChannelOptions(100)
{
    FullMode = BoundedChannelFullMode.Wait
});
builder.Services.AddSingleton(channel);
```

Controllers write to the channel, the worker reads from it:

```csharp
await channel.Writer.WriteAsync(new EmailMessage("user@example.com", "Welcome!"));
```

- **Durability**: none, the queue is in memory.
- **Backpressure**: built in. A bounded channel with `FullMode = Wait` makes producers wait when the queue is full, instead of growing without limit.
- **Best for**: decoupling a fast producer (HTTP requests) from a slower consumer (email sending) inside one process.

## Option 3: Hangfire

Hangfire stores jobs in a database (SQL Server, PostgreSQL, Redis) and runs them in a background server inside the app:

```csharp
builder.Services.AddHangfire(config => config
    .UseSqlServerStorage(builder.Configuration.GetConnectionString("Hangfire")));
builder.Services.AddHangfireServer();
```

Enqueue a job from a controller, or schedule a recurring one with a cron expression:

```csharp
BackgroundJob.Enqueue<IEmailSender>(sender => sender.SendWelcomeEmail("user@example.com"));

RecurringJob.AddOrUpdate<IReportGenerator>(
    "daily-report",
    generator => generator.GenerateDailyReport(),
    Cron.Daily);
```

- **Durability**: high. Jobs survive restarts because they live in storage.
- **Retries**: automatic, with exponential backoff, and a dashboard shows failed jobs.
- **Scheduling**: built in, with cron expressions.
- **Best for**: most apps that need jobs to survive restarts, with retries and a visible dashboard, without running extra infrastructure.

## Option 4: Quartz.NET

Quartz.NET is a full job scheduler with triggers, job stores, and clustering. A job is a class implementing `IJob`:

```csharp
public sealed class CleanupJob : IJob
{
    public Task Execute(IJobExecutionContext context)
    {
        Console.WriteLine("Cleaning up old records.");
        return Task.CompletedTask;
    }
}
```

- **Durability**: high with the persistent job store, including clustering across several app instances.
- **Retries**: configurable per job, with misfire handling for missed triggers.
- **Scheduling**: the strongest of the four: cron triggers, intervals, calendars, and job chaining.
- **Cost**: the most setup and the most concepts to learn.
- **Best for**: complex schedules, clustered deployments where only one instance may run a job, and jobs that need calendars or misfire policies.

## Decision Table

| Need | Pick |
| --- | --- |
| Fire-and-forget work, loss is fine | `BackgroundService` |
| Producer/consumer queue in one process | `BackgroundService` + `Channel<T>` |
| Jobs must survive restarts, retries, dashboard | Hangfire |
| Complex schedules, clustering, misfire rules | Quartz.NET |
| Recurring work, loss is fine | `BackgroundService` with `PeriodicTimer` |

## Common Mistakes

- Using a `BackgroundService` for payment emails and discovering a deploy wiped the queue.
- Using Hangfire for a cache refresh that could simply rerun on the next request.
- Running a recurring job on every instance of a scaled-out app without a lock or a clustered store, so the job runs three times.
- Blocking the worker loop with synchronous I/O, which stalls every job behind it.

## Key Takeaways

- `BackgroundService` is the built-in option: simple, in-process, and not durable.
- Channels add backpressure and decouple producers from consumers inside one process.
- Hangfire is the default pick when jobs must survive restarts, with retries and a dashboard included.
- Quartz.NET is the pick for complex scheduling and clustered deployments.
- Choose by durability and scheduling needs first; ease of setup is a tiebreaker, not the reason.
