# ASP.NET Core Global Exception Handling

Unhandled exceptions must never leak stack traces or internal details to clients, and they must be logged exactly once. ASP.NET Core gives you three layers for this: developer exception page (development only), `UseExceptionHandler` middleware, and the `IExceptionHandler` interface (.NET 8+).

## UseExceptionHandler Middleware

`UseExceptionHandler` catches exceptions from everything downstream and re-executes a dedicated error endpoint, so the client gets a clean error response instead of a crashed connection.

```csharp
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
}
else
{
    app.UseDeveloperExceptionPage();
}

app.MapGet("/error", (HttpContext context) =>
{
    // ProblemDetails is the standard error body (RFC 7807)
    return Results.Problem(
        statusCode: StatusCodes.Status500InternalServerError,
        title: "An unexpected error occurred.");
});

app.Run();
```

Register `UseExceptionHandler` early, before routing and endpoints, so it can catch exceptions from all of them. It only handles exceptions thrown during request processing; exceptions during startup or background services are outside its scope.

## IExceptionHandler (.NET 8+)

`IExceptionHandler` is the modern approach: you write a handler class, register it, and ASP.NET Core invokes it for any unhandled exception. It integrates with `UseExceptionHandler` and with `Results.Problem`.

```csharp
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) =>
        _logger = logger;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception,
        CancellationToken cancellationToken)
    {
        // Log once, here, with full detail for the server logs
        _logger.LogError(exception,
            "Unhandled exception for {Method} {Path}",
            httpContext.Request.Method, httpContext.Request.Path);

        int statusCode = exception switch
        {
            KeyNotFoundException => StatusCodes.Status404NotFound,
            UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
            ArgumentException => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError
        };

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = statusCode == 500
                ? "An unexpected error occurred."
                : exception.Message,
            Type = $"https://httpstatuses.io/{statusCode}"
        };

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true; // true means "handled, do not rethrow"
    }
}
```

```csharp
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails(); // registers the ProblemDetails service

var app = builder.Build();
app.UseExceptionHandler(); // no path needed: IExceptionHandler does the work
app.Run();
```

Returning `false` from `TryHandleAsync` lets the next registered handler try, or the exception propagate if none handles it. Register multiple handlers for different exception families; they run in registration order.

## ProblemDetails Responses

`ProblemDetails` is the standard error format (RFC 7807). A consistent shape lets every client parse errors the same way:

```json
{
  "type": "https://httpstatuses.io/500",
  "title": "An unexpected error occurred.",
  "status": 500,
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00"
}
```

`AddProblemDetails()` automatically adds a `traceId` extension, which lets support correlate a client report with a server log entry. In minimal APIs, `Results.Problem(...)` produces the same shape without a handler class.

## Logging the Exception Once

The most common mistake is logging the same exception in the controller, in the middleware, and in the handler. Pick one place: the global handler. Controllers should not wrap business logic in try/catch just to log; let the exception bubble to the handler.

```csharp
// Bad: logging here AND in the handler duplicates the entry
[HttpGet("{id:int}")]
public async Task<ActionResult<Order>> Get(int id)
{
    try
    {
        return Ok(await _orders.GetAsync(id));
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to get order {Id}", id); // duplicate
        throw;
    }
}

// Good: no try/catch, the global handler logs it once
[HttpGet("{id:int}")]
public async Task<ActionResult<Order>> Get(int id) =>
    Ok(await _orders.GetAsync(id));
```

The one exception to the rule: catch locally only when you can actually recover or translate the exception into a meaningful domain result.

## What NOT to Leak to Clients

Never send these to a client in a production error response:

- `exception.Message` for unexpected exceptions (it can contain SQL, file paths, or connection details)
- `exception.StackTrace`
- Inner exception details
- Configuration values or connection strings

For expected, client-caused errors (validation failures, not-found resources) the message is safe to include, because it describes the client's own input. For 500s, return a generic title and a `traceId`; the details belong in the server log only.

## Key Takeaways

- Use `IExceptionHandler` (.NET 8+) with `UseExceptionHandler()` for global handling; it is cleaner than a hand-rolled middleware.
- Return `ProblemDetails` (RFC 7807) so every error has the same shape, including a `traceId`.
- Log each exception exactly once, in the global handler, with full detail for the server.
- Map known exception types to the right status code; default unknown exceptions to 500.
- Never leak stack traces, SQL, or internal messages to clients in production.
