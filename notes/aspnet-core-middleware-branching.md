# Middleware Branching in ASP.NET Core

Not every request needs the same pipeline. `Map`, `MapWhen`, and
`UseWhen` let you branch the middleware pipeline by path or by any
condition, so one application can serve an API, an admin area, and a
static site with different behaviour for each.

## Map: Branch by Path

`Map` matches a request path prefix and runs a separate pipeline for it.
If the path matches, the branch runs and the main pipeline stops; if it
does not match, the main pipeline continues.

```csharp
var app = WebApplication.CreateBuilder(args).Build();

// Runs only for requests starting with /api.
app.Map("/api", api =>
{
    api.Use(async (context, next) =>
    {
        context.Response.Headers.Append("X-Api-Branch", "true");
        await next();
    });
    api.Run(async context =>
        await context.Response.WriteAsync("API branch"));
});

// Runs only for requests starting with /admin.
app.Map("/admin", admin =>
{
    admin.UseAuthentication();
    admin.UseAuthorization();
    admin.Run(async context =>
        await context.Response.WriteAsync("Admin branch"));
});

app.Run(async context =>
    await context.Response.WriteAsync("Main pipeline"));
```

A request to `/api/users` hits the API branch; `/admin/users` hits the
admin branch; `/home` falls through to the main pipeline. Note that
inside a branch, the matched prefix is stripped from
`HttpContext.Request.Path` and moved to `PathBase`.

## MapWhen: Branch by Any Condition

`MapWhen` branches on any predicate over the `HttpContext`, not just the
path — headers, query strings, user agents, anything.

```csharp
// Branch for requests carrying a specific header.
app.MapWhen(
    context => context.Request.Headers.ContainsKey("X-Mobile-App"),
    mobile =>
    {
        mobile.Run(async context =>
            await context.Response.WriteAsync("Mobile client branch"));
    });

// Branch for requests with ?preview=true.
app.MapWhen(
    context => context.Request.Query.ContainsKey("preview"),
    preview =>
    {
        preview.Run(async context =>
            await context.Response.WriteAsync("Preview branch"));
    });
```

## UseWhen: Branch and Come Back

`UseWhen` is the subtle one: it runs the branch middleware and then
**rejoins** the main pipeline. Use it when a subset of requests needs
extra processing but should still flow through the rest of the app.

```csharp
// Add a timing header for /api requests, then continue normally.
app.UseWhen(
    context => context.Request.Path.StartsWithSegments("/api"),
    api =>
    {
        api.Use(async (context, next) =>
        {
            var stopwatch = Stopwatch.StartNew();
            await next();
            stopwatch.Stop();
            context.Response.Headers.Append(
                "X-Elapsed-Ms", stopwatch.ElapsedMilliseconds.ToString());
        });
    });

// This still runs for /api requests, after the branch above.
app.MapGet("/api/users", () => new[] { "Alice", "Bob" });
```

## Choosing Between Them

| | Map | MapWhen | UseWhen |
| --- | --- | --- | --- |
| Condition | Path prefix | Any predicate | Any predicate |
| After the branch | Main pipeline stops | Main pipeline stops | Rejoins the main pipeline |
| Use for | Separate sub-apps | Header/query-based routing | Conditional extras (logging, headers) |

A common mistake is using `Map` when the request should still reach
later middleware — for example, mapping `/api` and then wondering why a
global exception handler registered after it never runs for API errors.
If the branch is an addition rather than a diversion, `UseWhen` is the
right tool.

## Key Takeaways

- `Map` branches by path prefix and stops the main pipeline for matches.
- `MapWhen` branches on any condition, and also stops the main pipeline.
- `UseWhen` runs extra middleware for matching requests, then rejoins
  the main pipeline.
- Inside a `Map` branch, the matched prefix moves from `Path` to
  `PathBase`.
- Use `Map`/`MapWhen` for separate sub-applications (API vs admin) and
  `UseWhen` for conditional additions such as timing headers.
