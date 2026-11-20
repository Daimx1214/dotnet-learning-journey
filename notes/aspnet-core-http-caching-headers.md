# ASP.NET Core HTTP Caching Headers

HTTP caching lets browsers and CDNs serve responses without hitting your server. The headers are simple; the rules about when a cached copy is still valid are where most bugs live.

## Cache-Control: Who May Cache and For How Long

`Cache-Control` is the main directive. `public` means shared caches (CDNs, proxies) may store it; `private` means only the end user's browser may. `max-age` is the freshness lifetime in seconds, and `no-store` forbids storing the response at all — use it for sensitive data.

```csharp
app.MapGet("/products", () =>
{
    var products = GetProducts();
    return Results.Ok(products);
})
.CacheOutput(p => p.Expire(TimeSpan.FromMinutes(5)));  // server-side output cache

// Or set the header directly for client/proxy caching:
app.MapGet("/catalog", (HttpContext http) =>
{
    http.Response.Headers.CacheControl = "public, max-age=300";
    return Results.Ok(GetCatalog());
});
```

For personalised responses, use `private` so a shared cache never serves one user's data to another. For data that changes rarely, a longer `max-age` plus a validation mechanism (below) gives the best of both worlds.

## ETag and Last-Modified: Validation

Instead of guessing freshness, the server can stamp a response with a validator. An `ETag` is an opaque version string (often a hash of the content); `Last-Modified` is the timestamp of the last change. The client sends the validator back on the next request, and the server answers `304 Not Modified` with an empty body if nothing changed.

```csharp
app.MapGet("/products/{id:int}", (int id, HttpContext http) =>
{
    var product = GetProduct(id);
    if (product is null) return Results.NotFound();

    var etag = $"\"{product.Version}\"";   // strong validator, quoted
    http.Response.Headers.ETag = etag;
    http.Response.Headers.LastModified = product.UpdatedAt.ToString("R");

    if (http.Request.Headers.IfNoneMatch == etag)
        return Results.StatusCode(StatusCodes.Status304NotModified);

    return Results.Ok(product);
});
```

A strong ETag must change whenever the bytes change. A weak ETag (prefixed `W/`) only promises semantic equivalence. `If-None-Match` takes precedence over `If-Modified-Since` when both are present.

## Conditional Requests and 304

The flow is: first request returns `200` with `ETag` and `Last-Modified`. The client caches the body plus the validators. The next request includes `If-None-Match: "<etag>"` (or `If-Modified-Since`). If the validator still matches, the server returns `304` with no body — the client reuses its cached copy, saving bandwidth and server work. If it does not match, the server returns a fresh `200`.

This is bandwidth saving, not latency elimination: the client still makes a round trip to revalidate. For zero round trips, use a long `max-age` with a versioned URL (e.g. `/app.v2.js`) so the URL itself changes when the content does.

## ResponseCaching vs Client Caching

ASP.NET Core's `ResponseCaching` middleware caches responses *on the server* (in memory), keyed by path and vary headers, so repeated requests never reach your endpoint code. Client caching (`Cache-Control` headers) caches on the browser or CDN. They solve different problems and can be combined: server-side caching reduces your work, client caching reduces network traffic.

```csharp
builder.Services.AddResponseCaching();
app.UseResponseCaching();

// ResponseCaching honours the ResponseCache attribute / Cache-Control headers:
app.MapGet("/reports/daily", () => Results.Ok(GetDailyReport()))
    .WithMetadata(new ResponseCacheAttribute
    {
        Duration = 600,              // seconds
        Location = ResponseCacheLocation.Any
    });
```

Note that `ResponseCaching` middleware only honours `Cache-Control` headers and does not support `ETag` revalidation by itself; for full control (including `Vary` by header and cache tags) prefer the newer `OutputCache` middleware in .NET 7+, which uses `.CacheOutput(...)` as shown above.

## Key Takeaways

- `Cache-Control: public, max-age=N` lets shared caches store a response; `private` restricts it to the browser; `no-store` forbids caching.
- `ETag` (content hash/version) and `Last-Modified` let clients revalidate instead of re-downloading.
- On `If-None-Match` matching the current ETag, return `304 Not Modified` with an empty body.
- Revalidation saves bandwidth but still costs a round trip; long `max-age` with versioned URLs avoids the round trip entirely.
- `ResponseCaching`/`OutputCache` cache on the server; `Cache-Control` headers cache on the client — use both where it fits.
- Never cache personalised responses as `public`; use `private` or `no-store` for user-specific data.
