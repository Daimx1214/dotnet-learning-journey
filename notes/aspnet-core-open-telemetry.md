# OpenTelemetry in ASP.NET Core (ASP.NET Core)

OpenTelemetry (OTel) is the vendor-neutral standard for collecting the three pillars of observability — traces, metrics, and logs — from your application and exporting them to any backend (Jaeger, Prometheus, Grafana, Azure Monitor, Datadog). In ASP.NET Core the instrumentation is built on `System.Diagnostics`: `ActivitySource` for traces, `Meter` for metrics, and the standard logging pipeline for logs. You add the OpenTelemetry SDK, register which sources to listen to, and pick exporters — the application code itself stays free of vendor-specific calls.

## Traces: ActivitySource and Spans

A trace is a tree of spans; each span represents one unit of work (an HTTP request, a database call, a method). ASP.NET Core and HttpClient already emit spans automatically when the OTel SDK is configured. For your own code, create an `ActivitySource` and start an `Activity` — if no listener is registered the `StartActivity` call returns `null`, so the null-check is part of the pattern.

```csharp
using System.Diagnostics;

// One source per service or per logical area, created once and reused.
public static class Telemetry
{
    public static readonly ActivitySource ActivitySource = new("MyApp.Orders");
    public static readonly Meter Meter = new("MyApp.Orders");
}

public class OrderService
{
    public async Task<Order> PlaceOrderAsync(OrderRequest request)
    {
        // StartActivity returns null when nothing is listening — cheap no-op.
        using Activity? activity = Telemetry.ActivitySource.StartActivity("PlaceOrder");
        activity?.SetTag("order.customer_id", request.CustomerId);
        activity?.SetTag("order.item_count", request.Items.Count);

        Order order = await _repository.SaveAsync(request);
        activity?.SetTag("order.id", order.Id);
        return order;
    }
}
```

`Activity` is the .NET representation of a span. Tags become span attributes; `activity?.AddEvent("Payment charged")` adds a timestamped event inside the span. Exceptions should be recorded explicitly with `activity?.SetStatus(ActivityStatusCode.Error)` and `activity?.RecordException(ex)` — otherwise a failed span looks successful in the trace viewer.

## Metrics: Meters, Counters, Histograms

Metrics are aggregated numbers: how many requests, how slow, how many in flight. Define a `Meter` once, then create instruments from it. The three instruments you will use most are `Counter` (monotonically increasing, e.g. orders placed), `Histogram` (distributions, e.g. request duration), and `UpDownCounter` (values that go up and down, e.g. active connections).

```csharp
public class OrderMetrics
{
    private readonly Counter<long> _ordersPlaced;
    private readonly Histogram<double> _orderDuration;

    public OrderMetrics()
    {
        _ordersPlaced = Telemetry.Meter.CreateCounter<long>(
            "orders.placed", "orders", "Total orders placed");
        _orderDuration = Telemetry.Meter.CreateHistogram<double>(
            "orders.duration", "ms", "Time to place an order");
    }

    public void RecordOrderPlaced(TimeSpan duration, string region)
    {
        var tags = new KeyValuePair<string, object?>("region", region);
        _ordersPlaced.Add(1, tags);
        _orderDuration.Record(duration.TotalMilliseconds, tags);
    }
}
```

Keep tag cardinality low: a tag like `region` with a handful of values is fine, but tagging by `order_id` or `user_id` creates a new time series per value and will overwhelm the metrics backend.

## Minimal Setup in Program.cs

The SDK wires everything together. Register tracing and metrics, add the built-in ASP.NET Core and HttpClient instrumentation, and choose exporters. The console exporter is ideal while developing; Jaeger or OTLP exporters point at a real backend.

```csharp
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName: "MyApp.Orders", serviceVersion: "1.0.0"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()   // incoming HTTP requests as spans
        .AddHttpClientInstrumentation()   // outgoing HTTP calls as child spans
        .AddSource(Telemetry.ActivitySource.Name) // your own ActivitySource
        .AddConsoleExporter()             // dev only: spans printed to stdout
        .AddOtlpExporter(options =>       // production: send to a collector
            options.Endpoint = new Uri("http://localhost:4317")))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()   // request count/duration out of the box
        .AddMeter(Telemetry.Meter.Name)   // your own instruments
        .AddConsoleExporter()
        .AddOtlpExporter());

var app = builder.Build();
app.Run();
```

Packages needed: `OpenTelemetry`, `OpenTelemetry.Exporter.Console`, `OpenTelemetry.Exporter.OpenTelemetryProtocol`, `OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Instrumentation.AspNetCore`, and `OpenTelemetry.Instrumentation.Http`. The OTLP exporter speaks the standard protocol, so the same code works with Jaeger (via its OTLP endpoint), Grafana Tempo, or any OTel collector — swapping backends is a configuration change, not a code change.

## Logs and Correlation

Logs gain their value when they carry the trace context. With OpenTelemetry logging enabled, every log record is enriched with the current `TraceId` and `SpanId`, so a log line can be jumped to its exact trace in the backend:

```csharp
builder.Logging.AddOpenTelemetry(logging => logging
    .AddConsoleExporter()
    .AddOtlpExporter());
```

Inside a request handler, `ILogger` calls automatically pick up the ambient activity, so no manual plumbing is needed. The practical payoff: when an alert fires on a slow trace, you can open that trace, see the exact spans, and find the log lines that belong to the same request — the three pillars joined by one ID.

## Key Takeaways

- OpenTelemetry standardises traces, metrics, and logs behind one SDK; the backend (Jaeger, Prometheus, Grafana) is a configuration choice.
- Traces use `ActivitySource`/`Activity`; ASP.NET Core and HttpClient instrumentation give you request spans for free, and your own spans need only a null-checked `StartActivity`.
- Metrics use `Meter` instruments — `Counter`, `Histogram`, `UpDownCounter` — with low-cardinality tags; never tag by unbounded IDs.
- The OTLP exporter is the production default; the console exporter is for local development.
- Enable OpenTelemetry logging so log records carry `TraceId`/`SpanId` and correlate with traces.
