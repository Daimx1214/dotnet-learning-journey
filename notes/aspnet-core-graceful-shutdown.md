# Graceful Shutdown in ASP.NET Core

A graceful shutdown lets an ASP.NET Core app finish in-flight work before the process exits. Without it, a deployment or container stop can kill requests mid-flight and lose queued background work.

## Why It Matters

Orchestrators send SIGTERM, then wait a grace period, then send SIGKILL. On Kubernetes the default grace period is 30 seconds. If the app ignores SIGTERM, every request in flight at that moment fails, and background jobs die mid-step.

## IHostApplicationLifetime

`IHostApplicationLifetime` exposes three cancellation tokens:

- `ApplicationStarted` — fired after the host has fully started.
- `ApplicationStopping` — fired when shutdown begins (SIGTERM / Ctrl+C / `StopAsync`).
- `ApplicationStopped` — fired when shutdown has completed.

Register callbacks on `ApplicationStopping` to start draining work.

```csharp
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();

lifetime.ApplicationStopping.Register(() =>
{
    Console.WriteLine("Shutdown requested: stop accepting new work.");
});

app.MapGet("/hello", () => "Hello World");
app.Run();
```

## Draining In-Flight Requests

Kestrel stops accepting new connections when shutdown starts, but it gives existing requests time to finish. The timeout is controlled by `HostOptions.ShutdownTimeout` (default 5 seconds) and Kestrel's own limits.

```csharp
builder.Services.Configure<HostOptions>(options =>
{
    // Wait up to 20 seconds for background services to stop.
    options.ShutdownTimeout = TimeSpan.FromSeconds(20);
});
```

Note the difference: Kestrel's request drain is bounded by its server options, while `ShutdownTimeout` bounds hosted-service shutdown. Set both to values that fit the longest request the app serves.

## Background Services and Cancellation

A `BackgroundService` receives a `CancellationToken` in `ExecuteAsync`. Respect it: check it in loops and pass it to awaits so the service stops promptly instead of hanging until the timeout expires.

```csharp
public class QueueWorker : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var item = await _queue.DequeueAsync(stoppingToken);
            await ProcessAsync(item, stoppingToken);
        }
    }
}
```

If `ProcessAsync` ignores the token, the host waits until `ShutdownTimeout` expires and then forces the process to exit. Long work should be split into token-aware chunks or made idempotent so a restart can safely retry it.

## SIGTERM in Containers

In a container, the process must run as PID 1 or forward signals correctly. The .NET runtime handles SIGTERM natively: it triggers `ApplicationStopping` the same way Ctrl+C does on a developer machine. A common mistake is wrapping the app in a shell script that swallows the signal — run `dotnet MyApp.dll` directly as the container entrypoint, or use `exec` in the script so the app becomes PID 1.

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY publish/ .
ENTRYPOINT ["dotnet", "MyApp.dll"]
```

## Health Checks and Load Balancers

During shutdown the app should report unhealthy so the load balancer stops sending traffic before the process dies. ASP.NET Core does not do this automatically; wire a check that flips when `ApplicationStopping` fires.

```csharp
public class ShutdownHealthCheck : IHealthCheck
{
    private volatile bool _shuttingDown;

    public ShutdownHealthCheck(IHostApplicationLifetime lifetime)
    {
        lifetime.ApplicationStopping.Register(() => _shuttingDown = true);
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
        => Task.FromResult(_shuttingDown
            ? HealthCheckResult.Unhealthy("Application is shutting down.")
            : HealthCheckResult.Healthy());
}
```

## Key Takeaways

- SIGTERM starts shutdown; SIGKILL ends it. The gap between them is the only time available to drain work.
- `IHostApplicationLifetime.ApplicationStopping` is the hook for cleanup and for flipping health checks to unhealthy.
- `HostOptions.ShutdownTimeout` defaults to 5 seconds — raise it deliberately, and keep it shorter than the orchestrator's grace period (30 seconds on Kubernetes by default).
- Background services must observe their cancellation token, or the host will force-exit them at the timeout.
- In containers, run the app as PID 1 (or via `exec`) so SIGTERM actually reaches the .NET process.
