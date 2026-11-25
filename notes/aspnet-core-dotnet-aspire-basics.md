# .NET Aspire Basics

.NET Aspire is an opinionated stack for building observable, production-ready distributed applications in .NET. It adds orchestration, service discovery, health checks, and telemetry on top of ordinary ASP.NET Core projects, so a solution with an API, a database, and a cache can be run and debugged as one system.

## The Pieces

An Aspire solution has three kinds of projects:

- **AppHost**: the orchestrator. It declares every resource (projects, containers, executables) and how they connect. It is the startup project.
- **ServiceDefaults**: a shared class library with `AddServiceDefaults`. It wires up OpenTelemetry, health checks, service discovery, and HTTP resilience once, and every service calls it.
- **Services**: ordinary ASP.NET Core APIs, workers, or frontends. They reference ServiceDefaults and read connection strings and endpoints from configuration.

## AppHost Example

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("cache");

var api = builder.AddProject<Projects.MyApi>("api")
    .WithReference(redis);

builder.AddProject<Projects.MyFrontend>("frontend")
    .WithReference(api);

builder.Build().Run();
```

`AddProject` registers a project resource. `WithReference` injects the dependency's connection string or endpoint into the consumer's configuration, and also creates a startup dependency so the API waits for Redis.

## ServiceDefaults

Every service calls `AddServiceDefaults` early in `Program.cs`:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddRedisClient("cache");

var app = builder.Build();
app.MapDefaultEndpoints();
app.Run();
```

`AddServiceDefaults` registers OpenTelemetry tracing, metrics, and logging exporters, adds a `/health` and `/alive` endpoint via `MapDefaultEndpoints`, and enables service discovery so `HttpClient` calls can use the logical name `https://api` instead of a hardcoded address.

## The Dashboard

When the AppHost runs, Aspire starts a dashboard (usually at `https://localhost:15888`). It shows every resource with its state, logs, endpoints, and environment variables, plus distributed traces and metrics across services. This is the main day-to-day benefit: instead of hunting through five console windows, one dashboard shows the whole system.

## The Resource Model

Resources are the units Aspire orchestrates:

- `AddProject`: a .NET project in the solution.
- `AddContainer`: a container image, for example Postgres or RabbitMQ.
- `AddRedis`, `AddPostgres`, `AddSqlServer`: typed helpers that add the container and expose a connection string.
- `AddExecutable`: an external process such as a Node frontend.

Resources can declare dependencies with `WaitFor`, so the API only starts after Postgres reports healthy. Parameters and secrets are passed with `builder.AddParameter("api-key", secret: true)` and read as configuration values.

## Service Discovery and Resilience

With service discovery enabled, an `HttpClient` can call a service by name:

```csharp
builder.Services.AddHttpClient("api", client =>
{
    client.BaseAddress = new Uri("https://api");
});
```

Aspire resolves `https://api` to the running API instance. `AddServiceDefaults` also adds standard HTTP resilience (retries with backoff and a circuit breaker) through `Microsoft.Extensions.Http.Resilience`, so a transient failure does not fail the request.

## When to Use It

- A solution with two or more services that must run together locally.
- Microservices where tracing a request across services matters.
- Teams that want health checks, metrics, and dashboards without hand-wiring each one.

Aspire is orchestration and observability, not a hosting platform. For production deployment, Aspire can publish to Azure Container Apps or produce a deployment manifest for other hosts.

## Key Takeaways

- The AppHost declares resources and wiring; ServiceDefaults wires observability and discovery into each service.
- `WithReference` passes connection strings and endpoints as configuration and orders startup.
- The dashboard gives one view of logs, traces, metrics, and resource health for the whole system.
- Service discovery lets code call services by logical name instead of hardcoded URLs.
- Aspire fits multi-service .NET solutions; a single API does not need it.
