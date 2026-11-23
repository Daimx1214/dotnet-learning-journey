# HttpLogging Middleware in ASP.NET Core

The HttpLogging middleware (built in since .NET 8, package `Microsoft.AspNetCore.HttpLogging`) logs HTTP requests and responses as they pass through the pipeline: method, path, status code, headers, and optionally bodies. It is the structured, built-in replacement for hand-rolled request-logging middleware.

## AddHttpLogging

Register the middleware and choose which fields to capture. `UseHttpLogging` belongs early in the pipeline, before routing, so it sees the request as it arrived.

```csharp
// Program.cs
builder.Services.AddHttpLogging(options =>
{
    options.LoggingFields = HttpLoggingFields.RequestPath
        | HttpLoggingFields.RequestMethod
        | HttpLoggingFields.ResponseStatusCode
        | HttpLoggingFields.RequestHeaders
        | HttpLoggingFields.ResponseHeaders
        | HttpLoggingFields.RequestBody
        | HttpLoggingFields.ResponseBody;

    options.RequestBodyLogLimit = 4096;   // bytes of the body to log
    options.ResponseBodyLogLimit = 4096;
});

var app = builder.Build();

app.UseHttpLogging();   // early, before routing
app.UseRouting();
app.MapControllers();
app.Run();
```

`RequestBodyLogLimit` and `ResponseBodyLogLimit` both default to 4096 bytes; anything beyond the limit is truncated in the log. Bodies are only logged when their `Content-Type` is text-based; binary bodies are skipped.

## Logging Fields

`HttpLoggingFields` is a flags enum — combine the fields the team actually needs, and nothing more, because every field is log volume and a privacy surface.

| Field | What it logs |
|---|---|
| `RequestPath`, `RequestMethod` | `POST /api/orders` |
| `RequestQuery` | Query string (may contain IDs and tokens — see redaction) |
| `RequestHeaders`, `ResponseHeaders` | Header names and values |
| `RequestBody`, `ResponseBody` | Body text, up to the log limit |
| `ResponseStatusCode` | `200`, `404`, `500` |
| `RequestProtocol` | `HTTP/1.1`, `HTTP/2` |
| `Duration` | Total request time (available in .NET 8+) |

```csharp
// Minimal, low-volume setup: path, method, status, duration only.
builder.Services.AddHttpLogging(options =>
{
    options.LoggingFields = HttpLoggingFields.RequestPath
        | HttpLoggingFields.RequestMethod
        | HttpLoggingFields.ResponseStatusCode
        | HttpLoggingFields.Duration;
});
```

## Privacy and Redaction of Sensitive Headers

Logging headers and bodies without redaction leaks `Authorization` bearer tokens, cookies, and passwords into log storage, where they persist far longer than the request. Redact by name, and redact request bodies that carry credentials.

```csharp
builder.Services.AddHttpLogging(options =>
{
    options.LoggingFields = HttpLoggingFields.RequestPath
        | HttpLoggingFields.RequestHeaders
        | HttpLoggingFields.RequestBody;

    // These headers are replaced with "[Redacted]" in the log.
    options.RequestHeaders.Add("Authorization");
    options.RequestHeaders.Add("Cookie");
    options.RequestHeaders.Add("X-Api-Key");
    options.ResponseHeaders.Add("Set-Cookie");
});
```

Rules that prevent the common leaks: never log the `Authorization` header, never log `Cookie` or `Set-Cookie`, never log a login or token endpoint's request body, and treat the query string as sensitive — a `?token=` or `?password=` in `RequestQuery` lands in the log verbatim.

## Worked Example: Counting the Log Lines for One Request

With the full-fields configuration above, trace a single `POST /api/orders` that returns `201` with a 120-byte JSON body:

- Line 1 — request: `Request: POST /api/orders` plus the request headers block.
- Line 2 — request body: the 120-byte JSON body (under the 4096-byte limit, so it is logged in full: 120 ≤ 4096).
- Line 3 — response: `Response: 201` plus the response headers block.
- Line 4 — response body: the response JSON.

Total: **4 log entries** for one request. Now send the same request with a 5000-byte body: the body is truncated to the 4096-byte limit, so 5000 − 4096 = **904 bytes are not logged**, and the entry notes the truncation. The arithmetic to remember is that the limit is per body, in bytes, and anything above it is silently cut — a large request body never produces a second log line.

## Combining with Request Logging

HttpLogging records the HTTP exchange; it does not replace application logging. Combine it with `ILogger` inside handlers for the business story, and with Serilog or OpenTelemetry when logs must leave the process.

```csharp
app.MapPost("/api/orders", async (OrderDto dto, AppDbContext db, ILogger<Program> logger) =>
{
    logger.LogInformation("Creating order for customer {CustomerId}", dto.CustomerId);
    var order = new Order { CustomerId = dto.CustomerId, Total = dto.Total };
    db.Orders.Add(order);
    await db.SaveChangesAsync();
    return Results.Created($"/api/orders/{order.Id}", order);
});
```

A request then has two views in the log: the HttpLogging lines (method, path, status, duration) and the handler's line (what the request meant). Keep HttpLogging fields minimal in production and turn bodies on only while debugging a specific integration.

## Sample Log Output

```text
info: Microsoft.AspNetCore.HttpLogging.HttpLoggingMiddleware[1]
      Request:
      Protocol: HTTP/1.1
      Method: POST
      Scheme: https
      Path: /api/orders
      Host: api.example.com
      Authorization: [Redacted]
      Content-Type: application/json
      Content-Length: 120
info: Microsoft.AspNetCore.HttpLogging.HttpLoggingMiddleware[2]
      RequestBody: {"customerId": 7, "total": 49.99}
info: Microsoft.AspNetCore.HttpLogging.HttpLoggingMiddleware[3]
      Response:
      StatusCode: 201
      Content-Type: application/json; charset=utf-8
info: Microsoft.AspNetCore.HttpLogging.HttpLoggingMiddleware[4]
      ResponseBody: {"id": 101, "customerId": 7, "total": 49.99}
      Duration: 42.3ms
```

## Key Takeaways

- `AddHttpLogging` + `UseHttpLogging` (early in the pipeline) logs the HTTP exchange without hand-rolled middleware.
- Choose fields deliberately: path, method, status, and duration are cheap; headers and bodies are volume and a privacy risk.
- Always redact `Authorization`, `Cookie`, and `Set-Cookie`, and never log login/token request bodies or sensitive query strings.
- Body logging is capped at 4096 bytes by default; a 5000-byte body loses 904 bytes to truncation.
- HttpLogging shows the exchange; `ILogger` in handlers shows the meaning — use both, and keep body logging off in production unless debugging.
