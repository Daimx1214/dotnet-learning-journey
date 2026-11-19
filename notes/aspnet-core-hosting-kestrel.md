# ASP.NET Core Hosting Models: Kestrel

Every ASP.NET Core app runs on Kestrel, a cross-platform, event-driven web server built on .NET. The question is not whether to use Kestrel, but how it is hosted and what sits in front of it.

## Kestrel In-Process vs Out-of-Process

With **IIS in-process hosting** (Windows only), the app runs inside the IIS worker process (`w3wp.exe`). Requests flow directly from IIS's HTTP.sys driver into the app with no extra hop, which gives the best throughput on Windows and lets IIS manage the process lifecycle.

With **out-of-process hosting**, Kestrel runs as its own standalone process and a reverse proxy forwards requests to it. This is the model on Linux and macOS, and it is also how IIS works when in-process hosting is not used. The app is fully self-contained — `dotnet run` starts a working web server with no IIS installed.

```csharp
// Minimal Program.cs — Kestrel is the default server, no extra setup needed.
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => "Hello from Kestrel!");

app.Run(); // Listens on http://localhost:5000 by default in Development
```

## A Reverse Proxy in Front of Kestrel

In production, Kestrel almost always sits behind a reverse proxy — IIS, Nginx, or Apache. The proxy terminates TLS, serves static files, enforces request limits, and forwards only application traffic to Kestrel on localhost. Kestrel itself is a capable HTTP server, but it is not designed to be exposed directly to the internet: it has no built-in request filtering, no defense against slow-loris style attacks, and no process recycling.

A typical Nginx setup forwards to Kestrel on `http://127.0.0.1:5000`, and the app must be told to trust the proxy's forwarded headers so it sees the real client IP and scheme:

```csharp
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownProxies.Add(IPAddress.Parse("127.0.0.1"));
});
app.UseForwardedHeaders();
```

## Kestrel Limits

Kestrel exposes limits through `KestrelServerOptions`. Two of the most useful:

```csharp
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxConcurrentConnections = 100;      // default: unlimited
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2); // default: 2 minutes
    options.Limits.MaxRequestBodySize = 10 * 1024 * 1024;     // 10 MB; default: ~30 MB
});
```

`MaxConcurrentConnections` caps how many connections Kestrel accepts at once — extra connections are refused rather than queuing forever. `KeepAliveTimeout` controls how long an idle keep-alive connection stays open before Kestrel closes it; the default of 2 minutes matches common proxy behaviour.

## Key Takeaways

- Kestrel is the built-in cross-platform web server for ASP.NET Core; every app uses it.
- In-process IIS hosting runs the app inside the IIS worker process (Windows only); out-of-process runs Kestrel standalone behind a reverse proxy.
- In production, put IIS, Nginx, or Apache in front of Kestrel for TLS termination, static files, and request filtering.
- Use `UseForwardedHeaders()` so the app sees the real client IP and scheme when behind a proxy.
- Tune `KestrelServerOptions` limits — `MaxConcurrentConnections`, `KeepAliveTimeout`, `MaxRequestBodySize` — rather than accepting the defaults blindly.
