# ASP.NET Core Request Timeouts

.NET 8 added built-in request timeouts. Instead of a request running forever because a downstream call hangs, the framework cancels it after a configured limit and returns a `408 Request Timeout` by default.

## Registering Timeout Policies

Register the middleware and define a default policy plus named policies in `Program.cs`:

```csharp
builder.Services.AddRequestTimeouts(options =>
{
    options.DefaultPolicy = new RequestTimeoutPolicy
    {
        Timeout = TimeSpan.FromSeconds(30),
        TimeoutStatusCode = StatusCodes.Status408RequestTimeout
    };
    options.AddPolicy("LongRunning", TimeSpan.FromMinutes(5));
    options.AddPolicy("Fast", TimeSpan.FromSeconds(2));
});

var app = builder.Build();
app.UseRequestTimeouts();
```

`UseRequestTimeouts` must run early in the pipeline, before the endpoints that need protection.

## Applying Policies to Endpoints

Apply a named policy with the `[RequestTimeout]` attribute, or disable timeouts entirely for an endpoint that legitimately runs long:

```csharp
[RequestTimeout("LongRunning")]
[HttpGet("reports/monthly")]
public async Task<IActionResult> MonthlyReport(CancellationToken ct)
{
    var report = await _reports.BuildMonthlyAsync(ct);
    return Ok(report);
}

[DisableRequestTimeout]
[HttpGet("exports/full")]
public async Task<IActionResult> FullExport(CancellationToken ct)
{
    // Long export: no timeout applies here.
    var file = await _exports.BuildFullAsync(ct);
    return File(file, "text/csv");
}
```

Minimal APIs support the same attribute on the handler, or `WithRequestTimeout("Fast")` on the route.

## Handling a Timeout

When a timeout fires, the framework cancels the request's `CancellationToken` and, by default, returns 408. You can customize the response instead of sending a bare status code:

```csharp
builder.Services.AddRequestTimeouts(options =>
{
    options.DefaultPolicy = new RequestTimeoutPolicy
    {
        Timeout = TimeSpan.FromSeconds(30),
        WriteTimeoutResponse = async (HttpContext context) =>
        {
            context.Response.StatusCode = StatusCodes.Status408RequestTimeout;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "The request took too long. Please try again."
            });
        }
    };
});
```

Always pass the request `CancellationToken` into downstream calls (`HttpClient`, EF Core, database queries). Without it, the timeout cancels the request but the abandoned work keeps running in the background.

## Interaction with Cancellation Tokens

The timeout middleware links its timer to `HttpContext.RequestAborted`. Your handler sees the same token, so one `ct.ThrowIfCancellationRequested()` or a token-aware `await` is enough — you do not need a second timer. Note that client disconnects also cancel this token, so a timeout and a disconnect look identical to your handler; log accordingly if you need to tell them apart.

## Key Takeaways

- .NET 8 request timeouts stop a request from running forever when a downstream call hangs.
- Register policies with `AddRequestTimeouts` and enable them with `app.UseRequestTimeouts()`.
- Use `[RequestTimeout("PolicyName")]` per endpoint and `[DisableRequestTimeout]` for legitimately long work.
- Customize the timeout response with `WriteTimeoutResponse`; the default is a bare 408.
- Always pass the request `CancellationToken` to downstream calls, or timed-out work keeps running.
- Client disconnects and timeouts both cancel the same token — your handler cannot tell them apart.
